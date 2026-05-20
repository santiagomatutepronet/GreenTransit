using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.HeatMaps.DTOs;
using GreenTransit.Application.Features.Reporting.HeatMaps.Services;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.HeatMaps.Queries;

/// <summary>
/// Dashboard HM-C — Vista de Mapas de Calor para Entidades Públicas.
/// Accesible para PUBLIC_ENT, DISPATCH_OFFICE y ADMIN.
/// </summary>
public sealed record GetPublicEntityHeatMapQuery(
    int     Year,
    int?    Month        = null,
    string? LerCodeFilter = null,
    string? WasteStream   = null,
    Guid?   IdScrap       = null
) : IRequest<PublicEntityHeatMapDto>;

public sealed class GetPublicEntityHeatMapQueryHandler
    : IRequestHandler<GetPublicEntityHeatMapQuery, PublicEntityHeatMapDto>
{
    private readonly IApplicationDbContext     _db;
    private readonly ICurrentUserService       _currentUser;
    private readonly HeatMapAggregationService _aggregator;

    public GetPublicEntityHeatMapQueryHandler(
        IApplicationDbContext     db,
        ICurrentUserService       currentUser,
        HeatMapAggregationService aggregator)
    {
        _db          = db;
        _currentUser = currentUser;
        _aggregator  = aggregator;
    }

    public async Task<PublicEntityHeatMapDto> Handle(
        GetPublicEntityHeatMapQuery request, CancellationToken ct)
    {
        var ownerId  = _currentUser.OwnerId;
        var dateFrom = request.Month.HasValue
            ? new DateTime(request.Year, request.Month.Value, 1)
            : new DateTime(request.Year, 1, 1);
        var dateTo = request.Month.HasValue ? dateFrom.AddMonths(1) : dateFrom.AddYears(1);

        var wmQuery = _db.WasteMoves
            .AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                && ((wm.ActualPickupStart  >= dateFrom && wm.ActualPickupStart  < dateTo)
                  || (wm.PlannedPickupStart >= dateFrom && wm.PlannedPickupStart < dateTo)));

        // Filtrado por perfil PUBLIC_ENT: puntos de recogida del municipio + SOs emitidas por la entidad
        if (_currentUser.IsInAnyProfile("PUBLIC_ENT"))
        {
            var linkedEntityId = _currentUser.LinkedEntityId;

            // Obtener el MunicipalityCode de la entidad pública
            var municipalityCode = await _db.BusinessEntities.AsNoTracking()
                .Where(e => e.Id == linkedEntityId)
                .Select(e => e.MunicipalityCode)
                .FirstOrDefaultAsync(ct);

            // Entidades (puntos de recogida) del mismo municipio
            var entityIdsInMunicipality = !string.IsNullOrEmpty(municipalityCode)
                ? await _db.BusinessEntities.AsNoTracking()
                    .Where(e => e.MunicipalityCode == municipalityCode)
                    .Select(e => e.Id)
                    .ToListAsync(ct)
                : new List<Guid>();

            // SOs emitidas directamente por la entidad pública
            var soIds = await _db.ServiceOrders.AsNoTracking()
                .Where(so => so.IdIssuedBy == linkedEntityId
                          && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id)
                .ToListAsync(ct);

            wmQuery = wmQuery.Where(wm =>
                (wm.IdSource != null && entityIdsInMunicipality.Contains(wm.IdSource.Value))
                || (wm.ServiceOrderId != null && soIds.Contains(wm.ServiceOrderId.Value)));
        }
        else if (request.IdScrap.HasValue && _currentUser.IsInAnyProfile("ADMIN", "DISPATCH_OFFICE"))
        {
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap || wm.IdScrap2 == request.IdScrap);
        }

        var rawMoves = await wmQuery
            .Include(wm => wm.WasteMoveResidues)
                .ThenInclude(wmr => wmr.Residue)
                    .ThenInclude(r => r!.LerCode)
            .Include(wm => wm.ServiceOrder)
                .ThenInclude(so => so!.PickupPoint)
            .ToListAsync(ct);

        // Filtro LER
        List<Guid> lerFilterIds = [];
        if (!string.IsNullOrEmpty(request.LerCodeFilter))
        {
            lerFilterIds = await _db.LerCodes.AsNoTracking()
                .Where(l => l.Code.StartsWith(request.LerCodeFilter))
                .Select(l => l.Id)
                .ToListAsync(ct);
        }

        var rows = (
            from wm  in rawMoves
            let  pp  = wm.ServiceOrder?.PickupPoint
            from wmr in wm.WasteMoveResidues
            let  res = wmr.Residue
            let  ler = res?.LerCode
            where lerFilterIds.Count == 0 || (res?.IdLERCode != null && lerFilterIds.Contains(res.IdLERCode.Value))
            where string.IsNullOrEmpty(request.WasteStream) || wm.ServiceOrder?.WasteStream == request.WasteStream
            select new
            {
                WasteMove  = wm,
                Residue    = res,
                LerCode    = ler,
                PickupPoint= pp,
                ScrapId    = wm.IdScrap,
                Weight     = wmr.Weight ?? 0m,
                PickupDate = wm.ActualPickupStart ?? wm.PlannedPickupStart
            }
        ).ToList();

        // ── KPIs ejecutivos ──────────────────────────────────────────────────
        var totalKg      = rows.Sum(r => r.Weight);
        var activePoints = rows.Where(r => r.PickupPoint != null)
                               .Select(r => r.PickupPoint!.Id).Distinct().Count();
        var months       = request.Month.HasValue ? 1.0 : 12.0;
        var totalPickups = rawMoves.Select(wm => wm.Id).Distinct().Count();
        var avgFreq      = activePoints > 0 ? totalPickups / (activePoints * months) : 0;

        var dominant = rows.Where(r => r.LerCode != null)
            .GroupBy(r => r.LerCode!.Id)
            .OrderByDescending(g => g.Sum(r => r.Weight))
            .Select(g => g.First().LerCode)
            .FirstOrDefault();

        // Variación vs periodo anterior
        var prevFrom  = request.Month.HasValue ? dateFrom.AddMonths(-1) : dateFrom.AddYears(-1);
        var prevTo    = dateFrom;
        var prevTotal = await _db.WasteMoves.AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                && ((wm.ActualPickupStart  >= prevFrom && wm.ActualPickupStart  < prevTo)
                  || (wm.PlannedPickupStart >= prevFrom && wm.PlannedPickupStart < prevTo)))
            .SelectMany(wm => wm.WasteMoveResidues)
            .SumAsync(wmr => wmr.Weight ?? 0m, ct);
        var kgVarPct = prevTotal > 0 ? Math.Round((double)((totalKg - prevTotal) / prevTotal * 100), 1) : 0;

        // ── Puntos georreferenciados ─────────────────────────────────────────
        var heatMapPoints = rows
            .Where(r => r.PickupPoint != null
                && !string.IsNullOrEmpty(r.PickupPoint.Latitude)
                && !string.IsNullOrEmpty(r.PickupPoint.Longitude))
            .GroupBy(r => r.PickupPoint!.Id)
            .Select(g =>
            {
                var pp     = g.First().PickupPoint!;
                var tKg    = g.Sum(r => r.Weight);
                var picks  = g.Select(r => r.WasteMove.Id).Distinct().Count();
                var last   = g.Max(r => r.PickupDate);
                var pCode  = g.Where(r => r.LerCode != null)
                              .GroupBy(r => r.LerCode!.Id)
                              .OrderByDescending(lg => lg.Sum(r => r.Weight))
                              .Select(lg => lg.First().LerCode)
                              .FirstOrDefault();
                double lat = double.TryParse(pp.Latitude,  System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var la) ? la : 0;
                double lng = double.TryParse(pp.Longitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lo) ? lo : 0;
                return new HeatMapPointDto(pp.Id, pp.Name, pp.Address, lat, lng, tKg, picks, pCode?.Code, pCode?.Description, last);
            })
            .ToList();

        // ── Tipología ────────────────────────────────────────────────────────
        var wasteTypology = rows
            .Where(r => r.LerCode != null)
            .GroupBy(r => r.LerCode!.Id)
            .Select(g =>
            {
                var tKg = g.Sum(r => r.Weight);
                return new WasteTypologyDto(
                    g.First().LerCode!.Code, g.First().LerCode!.Description,
                    g.First().LerCode!.IsDangerous, tKg,
                    totalKg > 0 ? (double)(tKg / totalKg * 100) : 0);
            })
            .OrderByDescending(t => t.TotalKg)
            .ToList();

        // ── Tendencia mensual por SCRAP ───────────────────────────────────────
        var scrapIds   = rows.Where(r => r.ScrapId.HasValue).Select(r => r.ScrapId!.Value).Distinct().ToList();
        var scrapNames = await _db.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var monthlyTrends = scrapIds.Select(scrapId =>
        {
            var monthly = Enumerable.Range(1, 12)
                .Select(m => rows
                    .Where(r => r.ScrapId == scrapId && r.PickupDate?.Month == m)
                    .Sum(r => r.Weight))
                .ToList();
            IReadOnlyList<decimal> ma3 = HeatMapAggregationService.ComputeMovingAverage3M(monthly);
            var name = scrapNames.TryGetValue(scrapId, out var n) ? n : scrapId.ToString();
            return new MonthlyTrendSeriesDto(scrapId.ToString(), name, monthly, ma3);
        }).ToList();

        // ── Detalle puntos de recogida ────────────────────────────────────────
        var provinceCodes = rows
            .Where(r => r.PickupPoint?.ProvinceCode != null)
            .Select(r => r.PickupPoint!.ProvinceCode!)
            .Distinct()
            .ToList();
        var provinceNameByCode = await _db.Provinces.AsNoTracking()
            .Where(p => provinceCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, p => p.Name, ct);

        var allKgs       = heatMapPoints.Select(p => p.TotalKg).ToList();
        var pointDetails = heatMapPoints.OrderByDescending(p => p.TotalKg).Select(p =>
        {
            var pp = rows.FirstOrDefault(r => r.PickupPoint?.Id == p.EntityId)?.PickupPoint;
            var provinceDisplay = pp?.ProvinceCode != null && provinceNameByCode.TryGetValue(pp.ProvinceCode, out var pName)
                ? pName : pp?.ProvinceCode;
            return new TopPickupPointDto(
                p.EntityId, p.EntityName, pp?.MunicipalityCode, provinceDisplay,
                p.TotalKg, p.PickupCount,
                p.PickupCount > 0 ? Math.Round(p.TotalKg / p.PickupCount, 2) : 0,
                p.PredominantLerCode,
                HeatMapAggregationService.ComputeTrafficLight(p.TotalKg, allKgs));
        }).ToList();

        // ── Zonas sensibles (cruce con DumZones) ──────────────────────────────
        var dumZones = await _db.DumZones.AsNoTracking()
            .Where(z => ownerId == Guid.Empty || z.OwnerId == ownerId)
            .ToListAsync(ct);

        var sensitiveIndicators = heatMapPoints
            .Select(p =>
            {
                var zone = dumZones.FirstOrDefault(z => !string.IsNullOrEmpty(z.GeometryJson));
                var exceeds = p.TotalKg > 5000m;
                return new SensitiveZoneIndicatorDto(
                    p.EntityName, null, zone?.ZoneCode, p.TotalKg,
                    exceeds, exceeds ? "Red" : "Green");
            })
            .Where(s => s.ExceedsThreshold)
            .ToList();

        // ── Exportación ──────────────────────────────────────────────────────
        var exportRows = rows
            .GroupBy(r => new { EntityId = r.PickupPoint?.Id, LerCodeId = r.LerCode?.Id })
            .Select(g => new HeatMapExportRowDto(
                g.First().PickupPoint?.Name ?? "",
                g.First().PickupPoint?.MunicipalityCode,
                g.First().PickupPoint?.ProvinceCode,
                g.First().LerCode?.Code,
                g.First().LerCode?.Description,
                g.First().LerCode?.IsDangerous ?? false,
                g.Sum(r => r.Weight),
                g.Select(r => r.WasteMove.Id).Distinct().Count(),
                g.Max(r => r.PickupDate)))
            .OrderByDescending(r => r.TotalKg)
            .ToList();

        return new PublicEntityHeatMapDto(
            Year                       : request.Year,
            Month                      : request.Month,
            TotalKg                    : totalKg,
            ActivePickupPoints         : activePoints,
            PredominantLerCode         : dominant?.Code,
            PredominantLerDescription  : dominant?.Description,
            AvgPickupsPerPointPerMonth : Math.Round(avgFreq, 2),
            TotalKgVariationPct        : kgVarPct,
            PickupPointsVariationPct   : 0,
            FrequencyVariationPct      : 0,
            HeatMapPoints              : heatMapPoints,
            WasteTypology              : wasteTypology,
            MonthlyTrends              : monthlyTrends,
            PickupPointDetails         : pointDetails,
            SensitiveZoneIndicators    : sensitiveIndicators,
            ExportRows                 : exportRows);
    }
}
