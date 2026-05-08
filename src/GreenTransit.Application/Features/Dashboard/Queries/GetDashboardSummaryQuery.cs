using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Dashboard.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Dashboard.Queries;

/// <summary>
/// Devuelve el resumen operativo completo para el dashboard.
/// Filtra todo por OwnerId del usuario activo. Adapta los datos según UserProfile:
///   CARRIER    → WasteMovesByStatus solo traslados con transporte asignado; UpcomingPickups solo SOs con IdCarrier.
///   PLANT_OP   → KPIs de tratamiento solo de su OwnerId (estándar multi-tenant).
///   ADMIN/SCRAP → todos los datos del OwnerId.
/// Las consultas se ejecutan de forma secuencial sobre el mismo DbContext (EF Core no admite
/// concurrencia sobre una sola instancia de contexto).
/// </summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    private static readonly string[] CollectedStatuses =
        ["RECOGIDO", "EN CAC", "EN PLANTA", "CLASIFICADO"];

    public GetDashboardSummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var now              = DateTime.UtcNow;
        var firstOfMonth     = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstOfNextMonth = firstOfMonth.AddMonths(1);
        var firstOfPrevMonth = firstOfMonth.AddMonths(-1);
        var in7Days          = now.AddDays(7);
        var first6MonthsAgo  = firstOfMonth.AddMonths(-5);
        var yearStart        = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd          = yearStart.AddYears(1);

        var ownerId        = _currentUser.OwnerId;
        var userProfile    = _currentUser.UserProfile;
        var isCarrier      = string.Equals(userProfile, "CARRIER",  StringComparison.OrdinalIgnoreCase);
        var isProducer     = string.Equals(userProfile, "PRODUCER", StringComparison.OrdinalIgnoreCase);
        var isScrap        = string.Equals(userProfile, "SCRAP",    StringComparison.OrdinalIgnoreCase);
        var linkedEntityId = _currentUser.LinkedEntityId;

        // ── Helpers para filtro de tenant ─────────────────────────────────────
        bool HasOwner(Guid? entityOwnerId) => ownerId == Guid.Empty || entityOwnerId == ownerId;

        // ── 1. WasteMoves by status ───────────────────────────────────────────
        var wmQuery = _context.WasteMoves
            .AsNoTracking()
            .Where(wm => ownerId == Guid.Empty || wm.OwnerId == ownerId);

        if (isCarrier)
            wmQuery = wmQuery.Where(wm =>
                wm.WasteMoveResidues.Any(r => r.IdCarrier != null));

        // PRODUCER: solo traslados cuya SO fue emitida por su entidad
        if (isProducer && linkedEntityId.HasValue)
            wmQuery = wmQuery.Where(wm =>
                wm.ServiceOrderId != null &&
                wm.ServiceOrder != null &&
                wm.ServiceOrder.IdIssuedBy == linkedEntityId.Value);

        // SCRAP: solo traslados donde figure como IdScrap o IdScrap2
        if (isScrap && linkedEntityId.HasValue)
            wmQuery = wmQuery.Where(wm =>
                wm.IdScrap == linkedEntityId.Value ||
                wm.IdScrap2 == linkedEntityId.Value);

        var wasteMovesByStatus = (await wmQuery
            .GroupBy(wm => wm.ServiceStatus ?? "DESCONOCIDO")
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.Status, x => x.Count);

        // ── 2. Kg recogidos este mes ──────────────────────────────────────────
        var wmrBase = _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => ownerId == Guid.Empty || r.WasteMove.OwnerId == ownerId);

        // PRODUCER: solo residuos de traslados vinculados a sus SOs
        if (isProducer && linkedEntityId.HasValue)
            wmrBase = wmrBase.Where(r =>
                r.WasteMove.ServiceOrderId != null &&
                r.WasteMove.ServiceOrder != null &&
                r.WasteMove.ServiceOrder.IdIssuedBy == linkedEntityId.Value);

        // SCRAP: solo residuos de traslados donde figure como IdScrap o IdScrap2
        if (isScrap && linkedEntityId.HasValue)
            wmrBase = wmrBase.Where(r =>
                r.WasteMove.IdScrap == linkedEntityId.Value ||
                r.WasteMove.IdScrap2 == linkedEntityId.Value);

        // ── Base de residuos de tratamiento (secciones 3, 4 y 9) ──────────────
        var tpResiduesBase = _context.TreatmentPlantResidues
            .AsNoTracking()
            .Where(r => ownerId == Guid.Empty || r.TreatmentPlant.OwnerId == ownerId);

        if (isProducer && linkedEntityId.HasValue)
            tpResiduesBase = tpResiduesBase.Where(r =>
                r.TreatmentPlant.WasteMove != null &&
                r.TreatmentPlant.WasteMove.ServiceOrder != null &&
                r.TreatmentPlant.WasteMove.ServiceOrder.IdIssuedBy == linkedEntityId.Value);

        if (isScrap && linkedEntityId.HasValue)
            tpResiduesBase = tpResiduesBase.Where(r =>
                r.TreatmentPlant.WasteMove != null &&
                (r.TreatmentPlant.WasteMove.IdScrap == linkedEntityId.Value ||
                 r.TreatmentPlant.WasteMove.IdScrap2 == linkedEntityId.Value));

        var kgCollectedThisMonth = await wmrBase
            .Where(r =>
                CollectedStatuses.Contains(r.WasteMove.ServiceStatus) &&
                r.WasteMove.ActualPickupStart >= firstOfMonth &&
                r.WasteMove.ActualPickupStart < firstOfNextMonth)
            .SumAsync(r => r.Weight ?? 0m, ct);

        // ── 3. Kg tratados este mes ───────────────────────────────────────────
        var kgTreatedThisMonth = await tpResiduesBase
            .Where(r =>
                r.TreatmentPlant.PlantTreatmentDate >= firstOfMonth &&
                r.TreatmentPlant.PlantTreatmentDate < firstOfNextMonth)
            .SumAsync(r => r.WeightTotal ?? 0m, ct);

        // ── 4. Tasas de tratamiento (mes en curso) ────────────────────────────
        var treatmentData = await tpResiduesBase
            .Where(r =>
                r.TreatmentPlant.PlantTreatmentDate >= firstOfMonth &&
                r.TreatmentPlant.PlantTreatmentDate < firstOfNextMonth &&
                r.TreatmentPlant.TreatmentOperation != null)
            .Select(r => new
            {
                r.WeightTotal,
                r.WeightValued,
                r.WeightReused,
                r.TreatmentPlant.TreatmentOperation!.IsRecycling,
                r.TreatmentPlant.TreatmentOperation!.IsEnergyRecovery,
                r.TreatmentPlant.TreatmentOperation!.IsPreparationForReuse
            })
            .ToListAsync(ct);

        var totalWeight          = treatmentData.Sum(d => d.WeightTotal    ?? 0m);
        var recycledWeight       = treatmentData.Where(d => d.IsRecycling)           .Sum(d => d.WeightValued ?? 0m);
        var energyWeight         = treatmentData.Where(d => d.IsEnergyRecovery)      .Sum(d => d.WeightValued ?? 0m);
        var reusedWeight         = treatmentData.Where(d => d.IsPreparationForReuse) .Sum(d => d.WeightReused ?? 0m);

        double SafeRate(decimal numerator) =>
            totalWeight > 0 ? Math.Round((double)(numerator / totalWeight) * 100, 2) : 0d;

        var recyclingRate  = SafeRate(recycledWeight);
        var energyRate     = SafeRate(energyWeight);
        var reuseRate      = SafeRate(reusedWeight);

        // ── 5. Huella CO₂ mes actual y mes anterior ───────────────────────────
        var totalCo2ThisMonth = await wmrBase
            .Where(r =>
                r.WasteMove.ActualPickupStart >= firstOfMonth &&
                r.WasteMove.ActualPickupStart < firstOfNextMonth)
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0m, ct);

        var co2PrevMonth = await wmrBase
            .Where(r =>
                r.WasteMove.ActualPickupStart >= firstOfPrevMonth &&
                r.WasteMove.ActualPickupStart < firstOfMonth)
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0m, ct);

        // ── 6. Incidencias abiertas por severidad ─────────────────────────────
        var incidentBase = _context.Incidents
            .AsNoTracking()
            .Where(i =>
                (ownerId == Guid.Empty || i.OwnerId == ownerId) &&
                i.ClosedAt == null);

        // PRODUCER: solo incidencias de sus SOs
        if (isProducer && linkedEntityId.HasValue)
            incidentBase = incidentBase.Where(i =>
                i.ServiceOrderId != null &&
                i.ServiceOrder != null &&
                i.ServiceOrder.IdIssuedBy == linkedEntityId.Value);

        // SCRAP: solo incidencias de traslados donde figure como IdScrap o IdScrap2
        if (isScrap && linkedEntityId.HasValue)
            incidentBase = incidentBase.Where(i =>
                i.WasteMoveReference != null &&
                _context.WasteMoves.Any(wm =>
                    wm.WasteMoveReference == i.WasteMoveReference &&
                    (wm.IdScrap == linkedEntityId.Value || wm.IdScrap2 == linkedEntityId.Value)));

        var openIncidentsBySeverity = (await incidentBase
            .GroupBy(i => i.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.Severity, x => x.Count);

        // ── 7. Cumplimiento MarketShares (año en curso) ───────────────────────
        var marketShareTargets = await _context.MarketShares
            .AsNoTracking()
            .Include(ms => ms.Scrap)
            .Where(ms =>
                (ownerId == Guid.Empty || ms.OwnerId == ownerId) &&
                ms.Year == now.Year)
            .ToListAsync(ct);

        // Peso real: WasteMoveResidues del año, agrupados por (IdScrap, ProductCategory)
        // Solo traslados no cancelados con fecha de recogida en el año actual
        var actualByScrapCategory = await _context.WasteMoveResidues
            .AsNoTracking()
            .Where(wmr =>
                wmr.WasteMove != null &&
                wmr.WasteMove.ServiceStatus != GreenTransit.Domain.Constants.WasteMoveStatuses.Cancelado &&
                wmr.WasteMove.ActualPickupStart.HasValue &&
                wmr.WasteMove.ActualPickupStart!.Value >= yearStart &&
                wmr.WasteMove.ActualPickupStart!.Value < yearEnd &&
                (ownerId == Guid.Empty || wmr.WasteMove.OwnerId == ownerId) &&
                wmr.Weight.HasValue &&
                wmr.Residue != null)
            .GroupBy(wmr => new
            {
                IdScrap  = wmr.WasteMove!.IdScrap,
                Category = wmr.Residue!.ProductCategory
            })
            .Select(g => new
            {
                g.Key.IdScrap,
                g.Key.Category,
                TotalWeight = g.Sum(x => x.Weight!.Value)
            })
            .ToListAsync(ct);

        var compliance = marketShareTargets
            .GroupBy(ms => new { ms.IdScrap, ms.Category })
            .Select(g =>
            {
                var first  = g.First();
                var target = g.Sum(ms => ms.Weight);
                var actual = actualByScrapCategory
                    .Where(k => k.IdScrap == g.Key.IdScrap && k.Category == g.Key.Category)
                    .Sum(k => k.TotalWeight);
                return new MarketShareComplianceDto(
                    g.Key.Category,
                    first.AutonomousCommunity,
                    target,
                    actual,
                    target > 0 ? Math.Round((double)(actual / target) * 100, 1) : 0d,
                    g.Key.IdScrap,
                    first.Scrap?.Name);
            })
            .OrderBy(x => x.ScrapName)
            .ThenBy(x => x.Category)
            .ToList();

        // ── 8. Próximas recogidas (próximos 7 días) ───────────────────────────
        var soQuery = _context.ServiceOrders
            .AsNoTracking()
            .Where(so =>
                (ownerId == Guid.Empty || so.OwnerId == ownerId) &&
                so.PlannedPickupStart >= now &&
                so.PlannedPickupStart <= in7Days &&
                so.Status != "Completed" &&
                so.Status != "Cancelled");

        if (isCarrier)
            soQuery = soQuery.Where(so => so.IdCarrier != null);

        // PRODUCER: solo sus propias SOs
        if (isProducer && linkedEntityId.HasValue)
            soQuery = soQuery.Where(so => so.IdIssuedBy == linkedEntityId.Value);

        // SCRAP: solo SOs vinculadas a traslados donde figure como IdScrap o IdScrap2
        if (isScrap && linkedEntityId.HasValue)
            soQuery = soQuery.Where(so =>
                so.WasteMoveReference != null &&
                _context.WasteMoves.Any(wm =>
                    wm.WasteMoveReference == so.WasteMoveReference &&
                    (wm.IdScrap == linkedEntityId.Value || wm.IdScrap2 == linkedEntityId.Value)));

        var upcomingPickups = (await soQuery
            .OrderBy(so => so.PlannedPickupStart)
            .Take(5)
            .Select(so => new
            {
                so.Id,
                so.ServiceOrderNumber,
                so.PlannedPickupStart,
                so.Priority,
                PickupPointName = so.PickupPoint != null ? so.PickupPoint.Name : (string?)null
            })
            .ToListAsync(ct))
            .Select(x => new UpcomingPickupDto(
                x.Id,
                x.ServiceOrderNumber,
                x.PickupPointName,
                x.PlannedPickupStart,
                x.Priority))
            .ToList();

        // ── 9. Últimos 6 meses: kg recogidos vs tratados ──────────────────────
        var collectedByMonth = await wmrBase
            .Where(r =>
                CollectedStatuses.Contains(r.WasteMove.ServiceStatus) &&
                r.WasteMove.ActualPickupStart >= first6MonthsAgo &&
                r.WasteMove.ActualPickupStart < firstOfNextMonth)
            .GroupBy(r => new
            {
                Year  = r.WasteMove.ActualPickupStart!.Value.Year,
                Month = r.WasteMove.ActualPickupStart!.Value.Month
            })
            .Select(g => new { g.Key.Year, g.Key.Month, Weight = g.Sum(r => r.Weight ?? 0m) })
            .ToListAsync(ct);

        var treatedByMonth = await tpResiduesBase
            .Where(r =>
                r.TreatmentPlant.PlantTreatmentDate >= first6MonthsAgo &&
                r.TreatmentPlant.PlantTreatmentDate < firstOfNextMonth)
            .GroupBy(r => new
            {
                Year  = r.TreatmentPlant.PlantTreatmentDate!.Value.Year,
                Month = r.TreatmentPlant.PlantTreatmentDate!.Value.Month
            })
            .Select(g => new { g.Key.Year, g.Key.Month, Weight = g.Sum(r => r.WeightTotal ?? 0m) })
            .ToListAsync(ct);

        var treatedDict = treatedByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Weight);

        var monthlyData = Enumerable.Range(0, 6)
            .Select(i => firstOfMonth.AddMonths(-5 + i))
            .Select(d =>
            {
                var collected = collectedByMonth
                    .FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month)?.Weight ?? 0m;
                var treated = treatedDict.TryGetValue((d.Year, d.Month), out var t) ? t : 0m;
                return new MonthlyKgDto(d.Year, d.Month, collected, treated);
            })
            .ToList();

        // ── 10. Puntos de entidades para el mapa ──────────────────────────────
        var entityMapPoints = (await _context.BusinessEntities
            .AsNoTracking()
            .Where(e => e.IsActive && e.Latitude != null && e.Longitude != null)
            .Select(e => new { e.Name, e.EntityRole, e.Latitude, e.Longitude })
            .Take(300)
            .ToListAsync(ct))
            .Where(e =>
                double.TryParse(e.Latitude, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _) &&
                double.TryParse(e.Longitude, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(e => new EntityMapPointDto(
                e.Name,
                e.EntityRole,
                double.Parse(e.Latitude!, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(e.Longitude!, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        // ── 11. Zonas DUM activas para el mapa ───────────────────────────────
        var dumZones = (await _context.DumZones
            .AsNoTracking()
            .Where(z =>
                (ownerId == Guid.Empty || z.OwnerId == ownerId) &&
                z.Status == "Active")
            .Select(z => new { z.ZoneCode, z.Name, z.GeometryJson })
            .ToListAsync(ct))
            .Select(z => new DumZoneMapDto(z.ZoneCode, z.Name, z.GeometryJson))
            .ToList();

        return new DashboardSummaryDto(
            wasteMovesByStatus,
            kgCollectedThisMonth,
            kgTreatedThisMonth,
            recyclingRate,
            energyRate,
            reuseRate,
            totalCo2ThisMonth,
            co2PrevMonth,
            openIncidentsBySeverity,
            compliance,
            upcomingPickups,
            monthlyData,
            entityMapPoints,
            dumZones);
    }
}
