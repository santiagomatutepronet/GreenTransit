using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.HeatMaps.DTOs;
using GreenTransit.Application.Features.Reporting.HeatMaps.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.HeatMaps.Queries;

/// <summary>
/// Dashboard HM-A — Mapa de Calor de Densidad de Residuos (SCRAP).
/// Accesible para SCRAP, DISPATCH_OFFICE y ADMIN.
/// </summary>
public sealed record GetWasteDensityHeatMapQuery(
    int     Year,
    int?    Month,
    string? AutonomousCommunity  = null,
    string? ProvinceCode         = null,
    string? MunicipalityCode     = null,
    string? LerCodeFilter        = null,
    string? WasteStream          = null,
    Guid?   IdScrap              = null,
    int?    CompareYear          = null,
    int?    CompareMonth         = null
) : IRequest<WasteDensityHeatMapDto>;

public sealed class GetWasteDensityHeatMapQueryHandler
    : IRequestHandler<GetWasteDensityHeatMapQuery, WasteDensityHeatMapDto>
{
    private readonly IApplicationDbContext     _db;
    private readonly ICurrentUserService       _currentUser;
    private readonly HeatMapAggregationService _aggregator;

    public GetWasteDensityHeatMapQueryHandler(
        IApplicationDbContext     db,
        ICurrentUserService       currentUser,
        HeatMapAggregationService aggregator)
    {
        _db          = db;
        _currentUser = currentUser;
        _aggregator  = aggregator;
    }

    public async Task<WasteDensityHeatMapDto> Handle(
        GetWasteDensityHeatMapQuery request, CancellationToken ct)
    {
        var ownerId  = _currentUser.OwnerId;
        var dateFrom = request.Month.HasValue
            ? new DateTime(request.Year, request.Month.Value, 1)
            : new DateTime(request.Year, 1, 1);
        var dateTo = request.Month.HasValue ? dateFrom.AddMonths(1) : dateFrom.AddYears(1);

        // ── Filtro LER por prefijo ────────────────────────────────────────────
        List<Guid> lerFilterIds = [];
        if (!string.IsNullOrEmpty(request.LerCodeFilter))
        {
            lerFilterIds = await _db.LerCodes.AsNoTracking()
                .Where(l => l.Code.StartsWith(request.LerCodeFilter))
                .Select(l => l.Id)
                .ToListAsync(ct);
        }

        // ── Consulta base: WasteMoves con residuos y punto de recogida ────────
        var wmQuery = _db.WasteMoves
            .AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                && ((wm.ActualPickupStart  >= dateFrom && wm.ActualPickupStart  < dateTo)
                  || (wm.PlannedPickupStart >= dateFrom && wm.PlannedPickupStart < dateTo)));

        if (_currentUser.IsInAnyProfile("SCRAP"))
        {
            var eid = _currentUser.LinkedEntityId;
            wmQuery = wmQuery.Where(wm => wm.IdScrap == eid || wm.IdScrap2 == eid);
        }
        else if (request.IdScrap.HasValue && _currentUser.IsInAnyProfile("ADMIN", "DISPATCH_OFFICE"))
        {
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap || wm.IdScrap2 == request.IdScrap);
        }

        // Materializar con residuos + SO + LER
        var rawData = await wmQuery
            .Include(wm => wm.WasteMoveResidues)
                .ThenInclude(wmr => wmr.Residue)
                    .ThenInclude(r => r!.LerCode)
            .Include(wm => wm.ServiceOrder)
                .ThenInclude(so => so!.PickupPoint)
            .ToListAsync(ct);

        // ── Aplanar a filas ───────────────────────────────────────────────────
        var rows = (
            from wm  in rawData
            where wm.ServiceOrder?.PickupPoint != null
            let  pp  = wm.ServiceOrder!.PickupPoint!
            where !string.IsNullOrEmpty(pp.Latitude) && !string.IsNullOrEmpty(pp.Longitude)
            from wmr in wm.WasteMoveResidues
            let  res = wmr.Residue
            let  ler = res?.LerCode
            where lerFilterIds.Count == 0 || (res?.IdLERCode != null && lerFilterIds.Contains(res.IdLERCode.Value))
            where string.IsNullOrEmpty(request.WasteStream) || wm.ServiceOrder!.WasteStream == request.WasteStream
            where string.IsNullOrEmpty(request.ProvinceCode) || pp.ProvinceCode == request.ProvinceCode
            where string.IsNullOrEmpty(request.MunicipalityCode) || pp.MunicipalityCode == request.MunicipalityCode
            select new
            {
                WasteMove  = wm,
                Residue    = res,
                LerCode    = ler,
                PickupPoint= pp,
                Weight     = wmr.Weight ?? 0m,
                PickupDate = wm.ActualPickupStart ?? wm.PlannedPickupStart
            }
        ).ToList();

        // ── Diccionarios código → nombre (sólo los códigos presentes en los datos) ──
        var provinceCodes = rows.Select(r => r.PickupPoint.ProvinceCode).Where(c => c != null).Distinct().ToList();
        var municipalityCodes = rows.Select(r => r.PickupPoint.MunicipalityCode).Where(c => c != null).Distinct().ToList();

        var provinceNames = await _db.Provinces.AsNoTracking()
            .Where(p => provinceCodes.Contains(p.Code))
            .GroupBy(p => p.Code)
            .Select(g => new { Code = g.Key, Name = g.First().Name ?? g.First().Ref })
            .ToDictionaryAsync(p => p.Code, p => p.Name, ct);

        var municipalityNames = await _db.Municipalities.AsNoTracking()
            .Where(m => municipalityCodes.Contains(m.Code))
            .GroupBy(m => m.Code)
            .Select(g => new { Code = g.Key, Name = g.First().Name })
            .ToDictionaryAsync(m => m.Code, m => m.Name, ct);

        // ── Puntos georreferenciados ─────────────────────────────────────────
        var byEntity = rows
            .GroupBy(r => r.PickupPoint.Id)
            .Select(g =>
            {
                var pp         = g.First().PickupPoint;
                var totalKg    = g.Sum(r => r.Weight);
                var pickups    = g.Select(r => r.WasteMove.Id).Distinct().Count();
                var lastPickup = g.Max(r => r.PickupDate);
                var dominant   = g.Where(r => r.LerCode != null)
                                  .GroupBy(r => r.LerCode!.Id)
                                  .OrderByDescending(lg => lg.Sum(r => r.Weight))
                                  .Select(lg => lg.First().LerCode)
                                  .FirstOrDefault();
                double lat = double.TryParse(pp.Latitude,  System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var la) ? la : 0;
                double lng = double.TryParse(pp.Longitude, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lo) ? lo : 0;
                return new HeatMapPointDto(pp.Id, pp.Name, pp.Address, lat, lng, totalKg, pickups, dominant?.Code, dominant?.Description, lastPickup);
            })
            .ToList();

        // ── Densidad por zona ────────────────────────────────────────────────
        var densityByZone = rows
            .GroupBy(r => r.PickupPoint.ProvinceCode ?? "")
            .Select(g =>
            {
                var totalKg  = g.Sum(r => r.Weight);
                var byTypo   = g.Where(r => r.LerCode != null)
                                .GroupBy(r => r.LerCode!.Id)
                                .Select(tg =>
                                {
                                    var tKg  = tg.Sum(r => r.Weight);
                                    return new WasteTypologyDto(
                                        tg.First().LerCode!.Code,
                                        tg.First().LerCode!.Description,
                                        tg.First().LerCode!.IsDangerous,
                                        tKg,
                                        totalKg > 0 ? (double)(tKg / totalKg * 100) : 0);
                                })
                                .OrderByDescending(t => t.TotalKg)
                                .ToList();
                return new DensityByZoneDto(g.Key, provinceNames.TryGetValue(g.Key, out var pn) ? pn : g.Key, "Province", totalKg,
                    g.Select(r => r.WasteMove.Id).Distinct().Count(), byTypo);
            })
            .OrderByDescending(d => d.TotalKg)
            .ToList();

        // ── Tipología global ─────────────────────────────────────────────────
        var totalKgAll    = rows.Sum(r => r.Weight);
        var wasteTypology = rows
            .Where(r => r.LerCode != null)
            .GroupBy(r => r.LerCode!.Id)
            .Select(g =>
            {
                var tKg = g.Sum(r => r.Weight);
                return new WasteTypologyDto(
                    g.First().LerCode!.Code, g.First().LerCode!.Description,
                    g.First().LerCode!.IsDangerous, tKg,
                    totalKgAll > 0 ? (double)(tKg / totalKgAll * 100) : 0);
            })
            .OrderByDescending(t => t.TotalKg)
            .ToList();

        // ── Top 20 puntos ────────────────────────────────────────────────────
        var allKgs = byEntity.Select(e => e.TotalKg).ToList();
        var top20  = byEntity
            .OrderByDescending(e => e.TotalKg)
            .Take(20)
            .Select(e =>
            {
                var pp = rows.First(r => r.PickupPoint.Id == e.EntityId).PickupPoint;
                return new TopPickupPointDto(
                    e.EntityId, e.EntityName,
                    pp.MunicipalityCode != null && municipalityNames.TryGetValue(pp.MunicipalityCode, out var mn) ? mn : pp.MunicipalityCode,
                    pp.ProvinceCode     != null && provinceNames.TryGetValue(pp.ProvinceCode, out var pn2)       ? pn2 : pp.ProvinceCode,
                    e.TotalKg, e.PickupCount,
                    e.PickupCount > 0 ? Math.Round(e.TotalKg / e.PickupCount, 2) : 0,
                    e.PredominantLerCode,
                    HeatMapAggregationService.ComputeTrafficLight(e.TotalKg, allKgs));
            })
            .ToList();

        // ── Frecuencia de recogida ────────────────────────────────────────────
        var months  = request.Month.HasValue ? 1.0 : 12.0;
        var avgFreq = byEntity.Count > 0 ? byEntity.Average(e => e.PickupCount / months) : 0;
        var freqList = byEntity.Select(e =>
        {
            var perMonth    = e.PickupCount / months;
            var isAnomalous = avgFreq > 0 && perMonth > avgFreq * 2;
            IReadOnlyList<double> sparkline = Enumerable.Repeat(perMonth, 12).ToList();
            return new PickupFrequencyDto(e.EntityId, e.EntityName, null, perMonth, isAnomalous, sparkline);
        }).ToList();

        // ── Comparativa de periodos ───────────────────────────────────────────
        var periodComparison = new List<PeriodComparisonDto>();
        if (request.CompareYear.HasValue)
        {
            var compFrom = request.CompareMonth.HasValue
                ? new DateTime(request.CompareYear.Value, request.CompareMonth.Value, 1)
                : new DateTime(request.CompareYear.Value, 1, 1);
            var compTo = request.CompareMonth.HasValue ? compFrom.AddMonths(1) : compFrom.AddYears(1);

            var compWms = await _db.WasteMoves.AsNoTracking()
                .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                    && ((wm.ActualPickupStart  >= compFrom && wm.ActualPickupStart  < compTo)
                     || (wm.PlannedPickupStart >= compFrom && wm.PlannedPickupStart < compTo)))
                .Include(wm => wm.WasteMoveResidues)
                .Include(wm => wm.ServiceOrder)
                    .ThenInclude(so => so!.PickupPoint)
                .ToListAsync(ct);

            var compByZone = compWms
                .Where(wm => wm.ServiceOrder?.PickupPoint?.ProvinceCode != null)
                .GroupBy(wm => wm.ServiceOrder!.PickupPoint!.ProvinceCode!)
                .ToDictionary(
                    g => g.Key,
                    g => (Kg: g.SelectMany(wm => wm.WasteMoveResidues).Sum(r => r.Weight ?? 0m),
                          Count: g.Count()));

            periodComparison = densityByZone.Select(z =>
            {
                compByZone.TryGetValue(z.ZoneCode, out var comp);
                var kgVar  = comp.Kg   > 0 ? (double)((z.TotalKg - comp.Kg)   / comp.Kg   * 100) : 0;
                var cVar   = comp.Count > 0 ? (double)((z.PickupCount - comp.Count) * 100.0 / comp.Count) : 0;
                return new PeriodComparisonDto(
                    z.ZoneCode, z.ZoneName, comp.Kg, comp.Count,
                    z.TotalKg, z.PickupCount,
                    Math.Round(kgVar, 1), Math.Round(cVar, 1));
            }).ToList();
        }

        // ── Exportación ──────────────────────────────────────────────────────
        var exportRows = rows
            .GroupBy(r => new { EntityId = r.PickupPoint.Id, LerCodeId = r.LerCode?.Id })
            .Select(g => new HeatMapExportRowDto(
                g.First().PickupPoint.Name,
                g.First().PickupPoint.MunicipalityCode,
                g.First().PickupPoint.ProvinceCode,
                g.First().LerCode?.Code,
                g.First().LerCode?.Description,
                g.First().LerCode?.IsDangerous ?? false,
                g.Sum(r => r.Weight),
                g.Select(r => r.WasteMove.Id).Distinct().Count(),
                g.Max(r => r.PickupDate)))
            .OrderByDescending(r => r.TotalKg)
            .ToList();

        return new WasteDensityHeatMapDto(
            Year                       : request.Year,
            Month                      : request.Month,
            HeatMapPoints              : byEntity,
            DensityByZone              : densityByZone,
            WasteTypology              : wasteTypology,
            TopPickupPoints            : top20,
            PickupFrequency            : freqList,
            AvgPickupsPerPointPerMonth : Math.Round(avgFreq, 2),
            AnomalousFrequencyPoints   : freqList.Count(f => f.IsAnomalous),
            PeriodComparison           : periodComparison,
            ExportRows                 : exportRows);
    }
}
