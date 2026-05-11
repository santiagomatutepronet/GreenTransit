using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Logistics.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Logistics.Queries;

/// <summary>
/// Devuelve todos los KPIs del Dashboard 1 — Panel de Optimización Logística SCRAP.
/// Filtrado por OwnerId (multi-tenant). Si el perfil es SCRAP, restringe a sus propios traslados.
/// Las queries que usan GroupBy proyectan primero a tipos anónimos y luego mapean en memoria
/// para evitar que EF Core intente traducir constructores de records posicionales.
/// </summary>
public sealed record GetLogisticsOptimizationQuery(
    int     Year,
    int?    Month        = null,
    Guid?   IdScrap      = null,
    string? WasteStream  = null,
    string? ProvinceCode = null
) : IRequest<LogisticsOptimizationDto>;

public sealed class GetLogisticsOptimizationQueryHandler
    : IRequestHandler<GetLogisticsOptimizationQuery, LogisticsOptimizationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetLogisticsOptimizationQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<LogisticsOptimizationDto> Handle(
        GetLogisticsOptimizationQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;
        var isAdmin        = _currentUser.IsInProfile(ProfileConstants.Admin);
        var isScrap        = !isAdmin && _currentUser.IsInProfile(ProfileConstants.Scrap);

        // ── Rango temporal ────────────────────────────────────────────────────
        DateTime dateFrom, dateTo;
        if (request.Month.HasValue)
        {
            dateFrom = new DateTime(request.Year, request.Month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            dateTo   = dateFrom.AddMonths(1);
        }
        else
        {
            dateFrom = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dateTo   = dateFrom.AddYears(1);
        }

        var prevFrom = request.Month.HasValue ? dateFrom.AddMonths(-1) : dateFrom.AddYears(-1);
        var prevTo   = dateFrom;
        var now      = DateTime.UtcNow;

        // ── Parámetros de scope para uso en lambdas ───────────────────────────
        // Se capturan como variables locales para que EF Core los trate como parámetros SQL.
        var scopeOwnerId        = ownerId;
        var scopeLinkedEntityId = linkedEntityId;
        var scopeIsScrap        = isScrap;
        var scopeFilterScrapId  = request.IdScrap;

        // ── 1. Eficiencia de rutas — proyección anónima → mapeo en memoria ────
        var currentRaw = await _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => (scopeOwnerId == Guid.Empty || r.WasteMove.OwnerId == scopeOwnerId)
                     && (!scopeIsScrap || !scopeLinkedEntityId.HasValue
                         || r.WasteMove.IdScrap  == scopeLinkedEntityId.Value
                         || r.WasteMove.IdScrap2 == scopeLinkedEntityId.Value)
                     && (scopeIsScrap || !scopeFilterScrapId.HasValue
                         || r.WasteMove.IdScrap  == scopeFilterScrapId.Value
                         || r.WasteMove.IdScrap2 == scopeFilterScrapId.Value)
                     && r.WasteMove.GatheredDate >= dateFrom
                     && r.WasteMove.GatheredDate <  dateTo
                     && r.TransportInfo_TransportCarbonEmissions != null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCO2e   = g.Sum(r => r.TransportInfo_TransportCarbonEmissions ?? 0m),
                TotalDistKm = g.Sum(r => r.TransportInfo_TransportDistance        ?? 0m),
                TotalKg     = g.Sum(r => r.Weight ?? 0m),
                Count       = g.Count()
            })
            .FirstOrDefaultAsync(ct);

        var prevCO2e = await _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => (scopeOwnerId == Guid.Empty || r.WasteMove.OwnerId == scopeOwnerId)
                     && (!scopeIsScrap || !scopeLinkedEntityId.HasValue
                         || r.WasteMove.IdScrap  == scopeLinkedEntityId.Value
                         || r.WasteMove.IdScrap2 == scopeLinkedEntityId.Value)
                     && r.WasteMove.GatheredDate >= prevFrom
                     && r.WasteMove.GatheredDate <  prevTo
                     && r.TransportInfo_TransportCarbonEmissions != null)
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0m, ct);

        var co2e    = currentRaw?.TotalCO2e   ?? 0m;
        var distKm  = currentRaw?.TotalDistKm ?? 0m;
        var totalKg = currentRaw?.TotalKg     ?? 0m;
        var pickups = currentRaw?.Count       ?? 0;

        double? trendPct = prevCO2e > 0
            ? (double)Math.Round((co2e - prevCO2e) / prevCO2e * 100m, 1)
            : null;

        var routeEfficiency = new RouteEfficiencyDto(
            AvgDistanceKmPerPickup: pickups > 0 ? Math.Round(distKm  / pickups, 2) : 0m,
            AvgCO2eKgPerPickup:     pickups > 0 ? Math.Round(co2e    / pickups, 2) : 0m,
            CO2eKgPerTonne:         totalKg > 0 ? Math.Round(co2e    / (totalKg / 1000m), 2) : 0m,
            TotalCO2eKg:            Math.Round(co2e, 2),
            TotalDistanceKm:        Math.Round(distKm, 2),
            TotalPickups:           pickups,
            CO2eTrendPercent:       trendPct);

        // ── 2. Volumen por zona ───────────────────────────────────────────────
        var soQuery = _context.ServiceOrders
            .AsNoTracking()
            .Where(so => (scopeOwnerId == Guid.Empty || so.OwnerId == scopeOwnerId)
                      && so.PlannedPickupStart >= dateFrom
                      && so.PlannedPickupStart <  dateTo
                      && so.IdPickupPoint != null);

        if (!string.IsNullOrEmpty(request.WasteStream))
            soQuery = soQuery.Where(so => so.WasteStream == request.WasteStream);

        var soPickupData = await soQuery
            .Select(so => new { so.IdPickupPoint, so.EstimatedWeight })
            .ToListAsync(ct);

        List<VolumeByZoneDto> volumeByZone = [];

        if (soPickupData.Count > 0)
        {
            var pickupIds = soPickupData
                .Select(x => x.IdPickupPoint!.Value)
                .Distinct()
                .ToList();

            var pickupEntities = await _context.BusinessEntities
                .AsNoTracking()
                .Where(e => pickupIds.Contains(e.Id) && e.ProvinceCode != null)
                .Select(e => new { e.Id, e.ProvinceCode })
                .ToListAsync(ct);

            var provinceByEntity = pickupEntities.ToDictionary(e => e.Id, e => e.ProvinceCode!);

            volumeByZone = soPickupData
                .Where(x => x.IdPickupPoint.HasValue
                         && provinceByEntity.ContainsKey(x.IdPickupPoint.Value))
                .GroupBy(x => provinceByEntity[x.IdPickupPoint!.Value])
                .Select(g => new VolumeByZoneDto(
                    g.Key,
                    null,
                    Math.Round(g.Sum(x => x.EstimatedWeight ?? 0m), 2),
                    g.Count()))
                .OrderByDescending(v => v.TotalKg)
                .ToList();

            if (!string.IsNullOrEmpty(request.ProvinceCode))
                volumeByZone = volumeByZone
                    .Where(v => v.ProvinceCode == request.ProvinceCode)
                    .ToList();
        }

        // ── 3. Puntos del mapa ────────────────────────────────────────────────
        var validRoles = new[] { "Producer", "CAC", "PublicEntity", "Plant" };
        var next30Days = now.AddDays(30);

        var entities = await _context.BusinessEntities
            .AsNoTracking()
            .Where(e => validRoles.Contains(e.EntityRole) && e.IsActive)
            .Select(e => new { e.Id, e.Name, e.EntityRole, e.Latitude, e.Longitude, e.Address })
            .ToListAsync(ct);

        List<(Guid Id, decimal AccumKg)>  kgList       = [];
        List<(Guid Id, int UpcomingCount)> upcomingList = [];

        if (entities.Count > 0)
        {
            var entityIds = entities.Select(e => e.Id).ToList();

            var kgRaw = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => (scopeOwnerId == Guid.Empty || so.OwnerId == scopeOwnerId)
                          && so.IdPickupPoint != null
                          && entityIds.Contains(so.IdPickupPoint.Value)
                          && so.PlannedPickupStart >= dateFrom
                          && so.PlannedPickupStart <  dateTo)
                .GroupBy(so => so.IdPickupPoint!.Value)
                .Select(g => new { Id = g.Key, AccumKg = g.Sum(so => so.EstimatedWeight ?? 0m) })
                .ToListAsync(ct);

            kgList = kgRaw.Select(x => (x.Id, x.AccumKg)).ToList();

            var upRaw = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => (scopeOwnerId == Guid.Empty || so.OwnerId == scopeOwnerId)
                          && so.IdPickupPoint != null
                          && entityIds.Contains(so.IdPickupPoint.Value)
                          && so.PlannedPickupStart >= now
                          && so.PlannedPickupStart <= next30Days)
                .GroupBy(so => so.IdPickupPoint!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            upcomingList = upRaw.Select(x => (x.Id, x.Count)).ToList();
        }

        var kgDict       = kgList.ToDictionary(x => x.Id, x => x.AccumKg);
        var upcomingDict = upcomingList.ToDictionary(x => x.Id, x => x.UpcomingCount);

        var mapPoints = entities
            .Select(e =>
            {
                double? lat = double.TryParse(e.Latitude,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var la) ? la : null;
                double? lng = double.TryParse(e.Longitude,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lo) ? lo : null;
                return new LogisticsMapPointDto(
                    e.Id,
                    e.Name       ?? string.Empty,
                    e.EntityRole ?? string.Empty,
                    lat, lng,
                    e.Address,
                    kgDict.GetValueOrDefault(e.Id),
                    upcomingDict.GetValueOrDefault(e.Id));
            })
            .ToList();

        // ── 4. Zonas DUM activas ──────────────────────────────────────────────
        var dumZonesRaw = await _context.DumZones
            .AsNoTracking()
            .Where(z => z.Status == "Active")
            .Select(z => new
            {
                z.Id,
                z.ZoneCode,
                z.GeometryJson,
                PrimaryAction = z.DumRestrictionRules
                    .Where(r => r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo >= now))
                    .OrderBy(r => r.RuleCode)
                    .Select(r => r.ActionType)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var dumZones = dumZonesRaw
            .Select(z => new DumZoneLayerDto(
                z.Id,
                z.ZoneCode    ?? string.Empty,
                z.GeometryJson,
                z.PrimaryAction ?? "Allow",
                ActionToColor(z.PrimaryAction)))
            .ToList();

        // ── 5. Cumplimiento DUM (simplificado) ────────────────────────────────
        var totalSosPeriod = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => (scopeOwnerId == Guid.Empty || so.OwnerId == scopeOwnerId)
                      && so.PlannedPickupStart >= dateFrom
                      && so.PlannedPickupStart <  dateTo)
            .CountAsync(ct);

        var dumCompliance = new DumComplianceDto(
            WithinWindow:     0,
            OutsideWindow:    0,
            NoZoneApplicable: totalSosPeriod);

        // ── 6. Heatmap de llegadas a planta ───────────────────────────────────
        var arrivalDates = await _context.EntryPlants
            .AsNoTracking()
            .Where(ep => (scopeOwnerId == Guid.Empty || ep.OwnerId == scopeOwnerId)
                      && ep.PlantEntryDate >= dateFrom
                      && ep.PlantEntryDate <  dateTo)
            .Select(ep => ep.PlantEntryDate)
            .ToListAsync(ct);

        var heatmap = arrivalDates
            .Where(d => d.HasValue)
            .GroupBy(d => new { DayOfWeek = (int)d!.Value.DayOfWeek, d.Value.Hour })
            .Select(g => new PlantArrivalHeatmapDto(g.Key.DayOfWeek, g.Key.Hour, g.Count()))
            .OrderBy(h => h.DayOfWeek).ThenBy(h => h.Hour)
            .ToList();

        // ── 7. Incidencias logísticas abiertas ────────────────────────────────
        var logisticTypes = new[] { "Retraso", "AveriVehiculo", "DescuadrePeso" };

        var incidentsRaw = await _context.Incidents
            .AsNoTracking()
            .Where(i => (scopeOwnerId == Guid.Empty || i.OwnerId == scopeOwnerId)
                     && i.ClosedAt == null
                     && logisticTypes.Contains(i.Type))
            .OrderByDescending(i => i.OpenedAt)
            .Take(50)
            .Select(i => new { i.Id, i.WasteMoveReference, i.Type, i.Severity, i.OpenedAt })
            .ToListAsync(ct);

        var openIncidents = incidentsRaw
            .Select(i => new OpenLogisticsIncidentDto(
                i.Id,
                i.WasteMoveReference,
                i.Type,
                i.Severity,
                i.OpenedAt,
                (int)(now - i.OpenedAt).TotalDays))
            .ToList();

        // ── 8. Utilización por tipo de vehículo — proyección anónima → mapeo ─
        var vehicleRaw = await _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => (scopeOwnerId == Guid.Empty || r.WasteMove.OwnerId == scopeOwnerId)
                     && (!scopeIsScrap || !scopeLinkedEntityId.HasValue
                         || r.WasteMove.IdScrap  == scopeLinkedEntityId.Value
                         || r.WasteMove.IdScrap2 == scopeLinkedEntityId.Value)
                     && (scopeIsScrap || !scopeFilterScrapId.HasValue
                         || r.WasteMove.IdScrap  == scopeFilterScrapId.Value
                         || r.WasteMove.IdScrap2 == scopeFilterScrapId.Value)
                     && r.WasteMove.GatheredDate >= dateFrom
                     && r.WasteMove.GatheredDate <  dateTo)
            .GroupBy(r => r.VehicleType)
            .Select(g => new
            {
                VehicleType = g.Key,
                TripCount   = g.Count(),
                TotalKg     = g.Sum(r => r.Weight ?? 0m),
                TotalDistKm = g.Sum(r => r.TransportInfo_TransportDistance ?? 0m)
            })
            .OrderByDescending(v => v.TotalKg)
            .ToListAsync(ct);

        var vehicleUtil = vehicleRaw
            .Select(v => new VehicleUtilizationDto(v.VehicleType, v.TripCount, v.TotalKg, v.TotalDistKm))
            .ToList();

        return new LogisticsOptimizationDto(
            RouteEfficiency:     routeEfficiency,
            VolumeByZone:        volumeByZone,
            MapPoints:           mapPoints,
            DumZones:            dumZones,
            DumCompliance:       dumCompliance,
            PlantArrivalHeatmap: heatmap,
            OpenIncidents:       openIncidents,
            VehicleUtilization:  vehicleUtil,
            Year:                request.Year,
            Month:               request.Month);
    }

    private static string ActionToColor(string? actionType) => actionType switch
    {
        "Block"    => "#dc3545",
        "Restrict" => "#fd7e14",
        "Allow"    => "#198754",
        "Notify"   => "#0d6efd",
        _          => "#6c757d"
    };
}
