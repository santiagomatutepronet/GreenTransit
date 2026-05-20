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

        // ── Proyección directa en SQL — evita cargar grafos completos en memoria ──
        var rowsQuery = wmQuery
            .Where(wm => wm.ServiceOrder != null && wm.ServiceOrder.PickupPoint != null)
            .Where(wm => wm.ServiceOrder!.PickupPoint!.Latitude != null
                      && wm.ServiceOrder!.PickupPoint!.Longitude != null)
            .SelectMany(wm => wm.WasteMoveResidues, (wm, wmr) => new
            {
                WasteMoveId       = wm.Id,
                WasteStream       = wm.ServiceOrder!.WasteStream,
                PickupPointId     = wm.ServiceOrder!.PickupPoint!.Id,
                PickupPointName   = wm.ServiceOrder!.PickupPoint!.Name,
                PickupPointAddr   = wm.ServiceOrder!.PickupPoint!.Address,
                PickupPointLat    = wm.ServiceOrder!.PickupPoint!.Latitude,
                PickupPointLon    = wm.ServiceOrder!.PickupPoint!.Longitude,
                ProvinceCode      = wm.ServiceOrder!.PickupPoint!.ProvinceCode,
                MunicipalityCode  = wm.ServiceOrder!.PickupPoint!.MunicipalityCode,
                LerCodeId         = wmr.Residue != null ? wmr.Residue.IdLERCode : (Guid?)null,
                LerCode           = wmr.Residue != null && wmr.Residue.LerCode != null ? wmr.Residue.LerCode.Code        : null,
                LerDescription    = wmr.Residue != null && wmr.Residue.LerCode != null ? wmr.Residue.LerCode.Description : null,
                LerIsDangerous    = wmr.Residue != null && wmr.Residue.LerCode != null && wmr.Residue.LerCode.IsDangerous,
                Weight            = wmr.Weight ?? 0m,
                PickupDate        = wm.ActualPickupStart ?? wm.PlannedPickupStart
            });

        // Aplicar filtros opcionales en SQL
        if (lerFilterIds.Count > 0)
            rowsQuery = rowsQuery.Where(r => r.LerCodeId != null && lerFilterIds.Contains(r.LerCodeId.Value));
        if (!string.IsNullOrEmpty(request.WasteStream))
            rowsQuery = rowsQuery.Where(r => r.WasteStream == request.WasteStream);
        if (!string.IsNullOrEmpty(request.ProvinceCode))
            rowsQuery = rowsQuery.Where(r => r.ProvinceCode == request.ProvinceCode);
        if (!string.IsNullOrEmpty(request.MunicipalityCode))
            rowsQuery = rowsQuery.Where(r => r.MunicipalityCode == request.MunicipalityCode);

        var rows = await rowsQuery.ToListAsync(ct);

        // ── Diccionarios código → nombre (sólo los códigos presentes en los datos) ──
        var provinceCodes = rows.Select(r => r.ProvinceCode).Where(c => c != null).Distinct().ToList();
        var municipalityCodes = rows.Select(r => r.MunicipalityCode).Where(c => c != null).Distinct().ToList();

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
            .GroupBy(r => r.PickupPointId)
            .Select(g =>
            {
                var first      = g.First();
                var totalKg    = g.Sum(r => r.Weight);
                var pickups    = g.Select(r => r.WasteMoveId).Distinct().Count();
                var lastPickup = g.Max(r => r.PickupDate);
                var dominant   = g.Where(r => r.LerCodeId != null)
                                  .GroupBy(r => r.LerCodeId)
                                  .OrderByDescending(lg => lg.Sum(r => r.Weight))
                                  .Select(lg => lg.First())
                                  .FirstOrDefault();
                double lat = double.TryParse(first.PickupPointLat, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var la) ? la : 0;
                double lng = double.TryParse(first.PickupPointLon, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lo) ? lo : 0;
                return new HeatMapPointDto(first.PickupPointId, first.PickupPointName, first.PickupPointAddr, lat, lng, totalKg, pickups, dominant?.LerCode, dominant?.LerDescription, lastPickup);
            })
            .ToList();

        // ── Densidad por zona ────────────────────────────────────────────────
        var densityByZone = rows
            .GroupBy(r => r.ProvinceCode ?? "")
            .Select(g =>
            {
                var totalKg  = g.Sum(r => r.Weight);
                var byTypo   = g.Where(r => r.LerCodeId != null)
                                .GroupBy(r => r.LerCodeId)
                                .Select(tg =>
                                {
                                    var tKg  = tg.Sum(r => r.Weight);
                                    var tf   = tg.First();
                                    return new WasteTypologyDto(
                                        tf.LerCode!,
                                        tf.LerDescription,
                                        tf.LerIsDangerous,
                                        tKg,
                                        totalKg > 0 ? (double)(tKg / totalKg * 100) : 0);
                                })
                                .OrderByDescending(t => t.TotalKg)
                                .ToList();
                return new DensityByZoneDto(g.Key, provinceNames.TryGetValue(g.Key, out var pn) ? pn : g.Key, "Province", totalKg,
                    g.Select(r => r.WasteMoveId).Distinct().Count(), byTypo);
            })
            .OrderByDescending(d => d.TotalKg)
            .ToList();

        // ── Tipología global ─────────────────────────────────────────────────
        var totalKgAll    = rows.Sum(r => r.Weight);
        var wasteTypology = rows
            .Where(r => r.LerCodeId != null)
            .GroupBy(r => r.LerCodeId)
            .Select(g =>
            {
                var f   = g.First();
                var tKg = g.Sum(r => r.Weight);
                return new WasteTypologyDto(
                    f.LerCode!, f.LerDescription, f.LerIsDangerous, tKg,
                    totalKgAll > 0 ? (double)(tKg / totalKgAll * 100) : 0);
            })
            .OrderByDescending(t => t.TotalKg)
            .ToList();

        // ── Top 20 puntos ────────────────────────────────────────────────────
        var allKgs = byEntity.Select(e => e.TotalKg).ToList();
        // Lookup rápido de municipio/provincia por PickupPointId
        var pickupMeta = rows
            .GroupBy(r => r.PickupPointId)
            .ToDictionary(g => g.Key, g => (MunicipalityCode: g.First().MunicipalityCode, ProvinceCode: g.First().ProvinceCode));

        var top20  = byEntity
            .OrderByDescending(e => e.TotalKg)
            .Take(20)
            .Select(e =>
            {
                pickupMeta.TryGetValue(e.EntityId, out var meta);
                return new TopPickupPointDto(
                    e.EntityId, e.EntityName,
                    meta.MunicipalityCode != null && municipalityNames.TryGetValue(meta.MunicipalityCode, out var mn) ? mn : meta.MunicipalityCode,
                    meta.ProvinceCode     != null && provinceNames.TryGetValue(meta.ProvinceCode, out var pn2)       ? pn2 : meta.ProvinceCode,
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

            // Proyección directa — sin Include masivo
            var compByZone = await _db.WasteMoves.AsNoTracking()
                .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                    && ((wm.ActualPickupStart  >= compFrom && wm.ActualPickupStart  < compTo)
                     || (wm.PlannedPickupStart >= compFrom && wm.PlannedPickupStart < compTo))
                    && wm.ServiceOrder != null
                    && wm.ServiceOrder.PickupPoint != null
                    && wm.ServiceOrder.PickupPoint.ProvinceCode != null)
                .GroupBy(wm => wm.ServiceOrder!.PickupPoint!.ProvinceCode!)
                .Select(g => new
                {
                    ProvinceCode = g.Key,
                    Kg           = g.SelectMany(wm => wm.WasteMoveResidues).Sum(r => (decimal?)r.Weight) ?? 0m,
                    Count        = g.Count()
                })
                .ToDictionaryAsync(x => x.ProvinceCode, x => (Kg: x.Kg, Count: x.Count), ct);

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
            .GroupBy(r => new { r.PickupPointId, r.LerCodeId })
            .Select(g => new HeatMapExportRowDto(
                g.First().PickupPointName,
                g.First().MunicipalityCode,
                g.First().ProvinceCode,
                g.First().LerCode,
                g.First().LerDescription,
                g.First().LerIsDangerous,
                g.Sum(r => r.Weight),
                g.Select(r => r.WasteMoveId).Distinct().Count(),
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
