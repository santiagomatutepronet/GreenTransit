using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.HeatMaps.DTOs;
using GreenTransit.Application.Features.Reporting.HeatMaps.Services;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.HeatMaps.Queries;

/// <summary>
/// Dashboard HM-B — Análisis de Patrones y Estacionalidad (SCRAP).
/// Accesible para SCRAP, DISPATCH_OFFICE y ADMIN.
/// </summary>
public sealed record GetWastePatternAnalysisQuery(
    int     Year,
    string? AutonomousCommunity = null,
    string? ProvinceCode        = null,
    string? MunicipalityCode    = null,
    string? LerCodeFilter       = null,
    string? WasteStream         = null,
    Guid?   IdScrap             = null
) : IRequest<WastePatternAnalysisDto>;

public sealed class GetWastePatternAnalysisQueryHandler
    : IRequestHandler<GetWastePatternAnalysisQuery, WastePatternAnalysisDto>
{
    private readonly IApplicationDbContext     _db;
    private readonly ICurrentUserService       _currentUser;
    private readonly HeatMapAggregationService _aggregator;

    public GetWastePatternAnalysisQueryHandler(
        IApplicationDbContext     db,
        ICurrentUserService       currentUser,
        HeatMapAggregationService aggregator)
    {
        _db          = db;
        _currentUser = currentUser;
        _aggregator  = aggregator;
    }

    public async Task<WastePatternAnalysisDto> Handle(
        GetWastePatternAnalysisQuery request, CancellationToken ct)
    {
        var ownerId  = _currentUser.OwnerId;
        var dateFrom = new DateTime(request.Year, 1, 1);
        var dateTo   = new DateTime(request.Year + 1, 1, 1);

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

        var rawMoves = await wmQuery
            .Include(wm => wm.WasteMoveResidues)
                .ThenInclude(wmr => wmr.Residue)
                    .ThenInclude(r => r!.LerCode)
            .Include(wm => wm.ServiceOrder)
                .ThenInclude(so => so!.PickupPoint)
            .ToListAsync(ct);

        var rows = (
            from wm  in rawMoves
            let  pp  = wm.ServiceOrder?.PickupPoint
            from wmr in wm.WasteMoveResidues
            let  res = wmr.Residue
            let  ler = res?.LerCode
            where string.IsNullOrEmpty(request.ProvinceCode) || pp?.ProvinceCode == request.ProvinceCode
            where string.IsNullOrEmpty(request.MunicipalityCode) || pp?.MunicipalityCode == request.MunicipalityCode
            where string.IsNullOrEmpty(request.WasteStream) || wm.ServiceOrder?.WasteStream == request.WasteStream
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

        // ── Heatmap temporal 12 meses × capítulo LER ─────────────────────────
        var temporalHeatMap = rows
            .Where(r => r.PickupDate.HasValue && r.LerCode != null)
            .GroupBy(r => new { Month = r.PickupDate!.Value.Month, Chapter = r.LerCode!.Chapter ?? "—", ChapterDesc = r.LerCode!.Description })
            .Select(g => new TemporalHeatMapCellDto(g.Key.Month, g.Key.Chapter, g.Key.ChapterDesc, g.Sum(r => r.Weight)))
            .OrderBy(c => c.Month)
            .ToList();

        // ── Tendencia mensual — Top 5 tipologías ──────────────────────────────
        var top5 = rows
            .Where(r => r.LerCode != null)
            .GroupBy(r => new { r.LerCode!.Code, r.LerCode.Description })
            .OrderByDescending(g => g.Sum(r => r.Weight))
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var monthlyTrends = top5.Select(code =>
        {
            var monthly = Enumerable.Range(1, 12)
                .Select(m => rows
                    .Where(r => r.LerCode?.Code == code.Code && r.PickupDate?.Month == m)
                    .Sum(r => r.Weight))
                .ToList();
            IReadOnlyList<decimal> ma3 = HeatMapAggregationService.ComputeMovingAverage3M(monthly);
            return new MonthlyTrendSeriesDto(code.Code, code.Description, monthly, ma3);
        }).ToList();

        // ── Heatmap semanal 7×24 ─────────────────────────────────────────────
        var weeklyFrequency = rawMoves
            .Where(wm => (wm.ActualPickupStart ?? wm.PlannedPickupStart).HasValue)
            .GroupBy(wm =>
            {
                var dt = (wm.ActualPickupStart ?? wm.PlannedPickupStart)!.Value;
                return new { DayOfWeek = ((int)dt.DayOfWeek + 6) % 7, Hour = dt.Hour };  // Lunes=0
            })
            .Select(g => new WeeklyFrequencyCell(g.Key.DayOfWeek, g.Key.Hour, g.Count()))
            .ToList();

        // ── Índice de concentración ───────────────────────────────────────────
        var kgByPoint   = rows.GroupBy(r => r.PickupPoint?.Id ?? Guid.Empty)
                              .Select(g => g.Sum(r => r.Weight))
                              .ToList();
        var concIndex   = HeatMapAggregationService.CalculateConcentrationIndex(kgByPoint);

        // Variación vs año anterior
        var prevFrom    = new DateTime(request.Year - 1, 1, 1);
        var prevTo      = dateFrom;
        var prevMoves   = await _db.WasteMoves.AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                && ((wm.ActualPickupStart  >= prevFrom && wm.ActualPickupStart  < prevTo)
                  || (wm.PlannedPickupStart >= prevFrom && wm.PlannedPickupStart < prevTo)))
            .Include(wm => wm.WasteMoveResidues)
            .Include(wm => wm.ServiceOrder)
                .ThenInclude(so => so!.PickupPoint)
            .ToListAsync(ct);

        var prevKgByPoint = prevMoves
            .SelectMany(wm => wm.WasteMoveResidues, (wm, wmr) => new
            {
                PointId = wm.ServiceOrder?.PickupPoint?.Id ?? Guid.Empty,
                Weight  = wmr.Weight ?? 0m
            })
            .GroupBy(x => x.PointId)
            .Select(g => g.Sum(x => x.Weight))
            .ToList();

        var prevConc   = HeatMapAggregationService.CalculateConcentrationIndex(prevKgByPoint);
        var concVarPct = prevConc > 0 ? Math.Round((concIndex - prevConc) / prevConc * 100, 1) : 0;

        // ── Alertas ──────────────────────────────────────────────────────────
        var p95 = kgByPoint.Count > 0
            ? kgByPoint.OrderBy(x => x).ElementAt((int)(kgByPoint.Count * 0.95))
            : 0m;

        // Resolver códigos de municipio a nombres para mostrar en alertas
        var munCodes = rows
            .Where(r => r.PickupPoint?.MunicipalityCode != null)
            .Select(r => r.PickupPoint!.MunicipalityCode!)
            .Distinct().ToList();
        var munNameByCode = await _db.Municipalities.AsNoTracking()
            .Where(m => munCodes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, m => m.Name, ct);

        var pointInputs = rows
            .GroupBy(r => r.PickupPoint?.Id ?? Guid.Empty)
            .Select(g =>
            {
                var pp       = g.First().PickupPoint;
                var lastPick = g.Max(r => r.PickupDate);
                var munDisplay = pp?.MunicipalityCode != null && munNameByCode.TryGetValue(pp.MunicipalityCode, out var mn)
                    ? mn : pp?.MunicipalityCode;
                return new PointAlertInput(pp?.Name ?? "", munDisplay, g.Sum(r => r.Weight), lastPick);
            }).ToList();

        var zoneInputs = rows
            .GroupBy(r => r.PickupPoint?.MunicipalityCode ?? "")
            .Select(g =>
            {
                var zoneName = munNameByCode.TryGetValue(g.Key, out var mn) ? mn : g.Key;
                return new ZoneAlertInput(zoneName, g.Sum(r => r.Weight), g.Sum(r => r.Weight) >= p95);
            })
            .ToList();

        var curFreqByZone  = rows.GroupBy(r => r.PickupPoint?.MunicipalityCode ?? "")
            .ToDictionary(
                g => munNameByCode.TryGetValue(g.Key, out var mn) ? mn : g.Key,
                g => (double)g.Select(r => r.WasteMove.Id).Distinct().Count());
        var prevFreqByZone = prevMoves
            .Where(wm => wm.ServiceOrder?.PickupPoint?.MunicipalityCode != null)
            .GroupBy(wm => wm.ServiceOrder!.PickupPoint!.MunicipalityCode!)
            .ToDictionary(
                g => munNameByCode.TryGetValue(g.Key, out var mn) ? mn : g.Key,
                g => (double)g.Count());
        var freqInputs = curFreqByZone.Select(kvp =>
        {
            prevFreqByZone.TryGetValue(kvp.Key, out var prev);
            return new FreqAlertInput(kvp.Key, kvp.Value, prev);
        }).ToList();

        var alerts = _aggregator.GenerateAccumulationAlerts(pointInputs, zoneInputs, freqInputs);

        return new WastePatternAnalysisDto(
            Year                           : request.Year,
            TemporalHeatMap                : temporalHeatMap,
            MonthlyTrends                  : monthlyTrends,
            WeeklyFrequency                : weeklyFrequency,
            ConcentrationIndex             : concIndex,
            ConcentrationIndexVariationPct : concVarPct,
            Alerts                         : alerts);
    }
}
