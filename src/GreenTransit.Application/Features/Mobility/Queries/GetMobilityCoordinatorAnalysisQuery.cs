using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Mobility.DTOs;
using GreenTransit.Application.Features.Mobility.Services;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Mobility.Queries;

/// <summary>
/// Devuelve los KPIs del Dashboard UC3-A — Análisis de Impacto en Movilidad para Coordinadores.
/// El COORDINATOR solo ve los SCRAPs vinculados a sus acuerdos.
/// </summary>
public sealed record GetMobilityCoordinatorAnalysisQuery(
    int      Year,
    int?     Month               = null,
    Guid?    IdScrap             = null,
    string?  AutonomousCommunity = null,
    string?  ProvinceCode        = null,
    string?  MunicipalityCode    = null,
    string?  WasteStream         = null,
    int?     CompareYear         = null,
    int?     CompareMonth        = null
) : IRequest<MobilityCoordinatorAnalysisDto>;

public sealed class GetMobilityCoordinatorAnalysisQueryHandler
    : IRequestHandler<GetMobilityCoordinatorAnalysisQuery, MobilityCoordinatorAnalysisDto>
{
    private readonly IApplicationDbContext          _context;
    private readonly ICurrentUserService            _currentUser;
    private readonly IMobilityRecommendationEngine  _recommendations;
    private readonly IConfiguration                 _config;

    public GetMobilityCoordinatorAnalysisQueryHandler(
        IApplicationDbContext         context,
        ICurrentUserService           currentUser,
        IMobilityRecommendationEngine recommendations,
        IConfiguration                config)
    {
        _context         = context;
        _currentUser     = currentUser;
        _recommendations = recommendations;
        _config          = config;
    }

    public async Task<MobilityCoordinatorAnalysisDto> Handle(
        GetMobilityCoordinatorAnalysisQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;
        var isAdmin        = _currentUser.IsInProfile(ProfileConstants.Admin);
        var isCoordinator  = !isAdmin && _currentUser.IsInProfile(ProfileConstants.Coordinator);

        var (dateFrom, dateTo) = BuildRange(request.Year, request.Month);

        var s          = _config.GetSection("MobilitySettings");
        double ps1     = Cfg(s["PeakHourStart1"],  7.5);
        double pe1     = Cfg(s["PeakHourEnd1"],    9.5);
        double ps2     = Cfg(s["PeakHourStart2"],  17.5);
        double pe2     = Cfg(s["PeakHourEnd2"],    19.5);

        var cw   = _config.GetSection("MobilitySettings:ConflictIndexWeights");
        double wPeak   = Cfg(cw["PeakHour"],   0.40);
        double wDum    = Cfg(cw["OutsideDum"], 0.30);
        double wInc    = Cfg(cw["Incidents"],  0.20);
        double wVol    = Cfg(cw["Volume"],     0.10);

        // ── Conjunto base de traslados ────────────────────────────────────────
        var wmQuery = _context.WasteMoves
            .AsNoTracking()
            .Where(wm => ownerId == Guid.Empty || wm.OwnerId == ownerId);

        if (isCoordinator && linkedEntityId.HasValue)
        {
            var lid = linkedEntityId.Value;
            var scrapIds = await _context.Agreements
                .AsNoTracking()
                .Where(a => a.IdCoordinator == lid)
                .Select(a => a.IdScrap)
                .Distinct()
                .ToListAsync(ct);

            wmQuery = wmQuery.Where(wm => scrapIds.Contains(wm.IdScrap));
        }

        if (request.IdScrap.HasValue)
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value);

        if (!string.IsNullOrEmpty(request.WasteStream))
            wmQuery = wmQuery.Where(wm => wm.ServiceOrder != null &&
                                          wm.ServiceOrder.WasteStream == request.WasteStream);

        wmQuery = wmQuery.Where(wm =>
            (wm.ActualPickupStart != null
                ? wm.ActualPickupStart >= dateFrom && wm.ActualPickupStart < dateTo
                : wm.PlannedPickupStart >= dateFrom && wm.PlannedPickupStart < dateTo));

        var moves = await wmQuery
            .Select(wm => new
            {
                wm.Id,
                wm.IdSource,
                wm.IdScrap,
                PickupDate = wm.ActualPickupStart ?? wm.PlannedPickupStart
            })
            .ToListAsync(ct);

        var moveIds = moves.Select(m => m.Id).ToHashSet();

        // ── Entidades de los puntos de recogida ───────────────────────────────
        var sourceIds = moves
            .Where(m => m.IdSource.HasValue)
            .Select(m => m.IdSource!.Value)
            .Distinct()
            .ToList();

        var entityMap = await _context.BusinessEntities
            .AsNoTracking()
            .Where(e => sourceIds.Contains(e.Id))
            .Select(e => new { e.Id, e.MunicipalityCode, e.ProvinceCode })
            .ToDictionaryAsync(e => e.Id, ct);

        // ── Resolución nombres geográficos ────────────────────────────────────
        // Municipality.Code almacena el código local de 3 dígitos (INE local),
        // mientras que BusinessEntity.MunicipalityCode almacena el código INE
        // completo de 5 dígitos (Province.Code[2] + Municipality.Code[3]).
        // Se construye la clave Province.Code + Municipality.Code para que coincida.
        var allMunCodes  = entityMap.Values.Where(e => e.MunicipalityCode != null).Select(e => e.MunicipalityCode!).Distinct().ToList();
        var allProvCodes = entityMap.Values.Where(e => e.ProvinceCode     != null).Select(e => e.ProvinceCode!).Distinct().ToList();

        var munNameMap = await (
            from m in _context.Municipalities.AsNoTracking()
            join p in _context.Provinces.AsNoTracking() on m.IdProvince equals p.Id
            where p.Code != null && allProvCodes.Contains(p.Code)
            select new { FullCode = p.Code + m.Code, m.Name }
        ).Where(x => allMunCodes.Contains(x.FullCode))
         .ToDictionaryAsync(x => x.FullCode, x => x.Name, ct);

        var provNameMap = await _context.Provinces
            .AsNoTracking()
            .Where(p => p.Code != null && allProvCodes.Contains(p.Code))
            .Select(p => new { p.Code, p.Name })
            .ToDictionaryAsync(p => p.Code!, p => p.Name ?? string.Empty, ct);

        // ── Incidencias logísticas del periodo ────────────────────────────────
        var incidentsByRef = await _context.Incidents
            .AsNoTracking()
            .Where(i => i.WasteMoveReference != null)
            .Join(_context.WasteMoves.Where(wm => moveIds.Contains(wm.Id)),
                  i  => i.WasteMoveReference,
                  wm => wm.WasteMoveReference,
                  (i, wm) => new { wm.IdSource, i.Type })
            .Where(x => x.Type == "Retraso" || x.Type == "AveriaVehiculo")
            .ToListAsync(ct);

        // ── Heatmap semanal ───────────────────────────────────────────────────
        var heatmap = moves
            .Where(m => m.PickupDate.HasValue)
            .GroupBy(m => new
            {
                Day  = ((int)m.PickupDate!.Value.DayOfWeek + 6) % 7,
                Hour = m.PickupDate.Value.Hour
            })
            .Select(g => new WeeklyHeatmapCellDto(g.Key.Day, g.Key.Hour, g.Count()))
            .ToList();

        // ── % hora pico ───────────────────────────────────────────────────────
        int peakCount = moves.Count(m => IsPeak(m.PickupDate, ps1, pe1, ps2, pe2));
        double peakHourPercent = moves.Count > 0 ? 100.0 * peakCount / moves.Count : 0;

        // ── Índice de conflicto por municipio ─────────────────────────────────
        var byMunicipality = moves
            .GroupBy(m =>
            {
                var mc = m.IdSource.HasValue && entityMap.TryGetValue(m.IdSource.Value, out var e)
                    ? e.MunicipalityCode : null;
                return mc ?? "DESCONOCIDO";
            })
            .Select(g =>
            {
                if (!string.IsNullOrEmpty(request.MunicipalityCode) && g.Key != request.MunicipalityCode)
                    return (MunicipalConflictIndexDto?)null;

                var firstSource = g.FirstOrDefault(x => x.IdSource.HasValue)?.IdSource;
                string? provinceCode = firstSource.HasValue && entityMap.TryGetValue(firstSource.Value, out var ent)
                    ? ent.ProvinceCode : null;

                if (!string.IsNullOrEmpty(request.ProvinceCode) && provinceCode != request.ProvinceCode)
                    return (MunicipalConflictIndexDto?)null;

                int tot  = g.Count();
                int peak = g.Count(m => IsPeak(m.PickupDate, ps1, pe1, ps2, pe2));
                double pp   = tot > 0 ? 100.0 * peak / tot : 0;
                double dumP = pp;

                var incidents = incidentsByRef.Count(i =>
                    i.IdSource.HasValue
                    && entityMap.TryGetValue(i.IdSource.Value, out var ee)
                    && ee.MunicipalityCode == g.Key);

                double ci = Math.Min(100,
                    wPeak * pp +
                    wDum  * dumP +
                    wInc  * Math.Min(incidents * 10.0, 100.0) +
                    wVol  * Math.Min(tot / 10.0, 100.0));

                string light = ci >= 70 ? "Red" : ci >= 40 ? "Orange" : "Green";

                return (MunicipalConflictIndexDto?)new MunicipalConflictIndexDto(
                    MunicipalityCode:        g.Key,
                    MunicipalityName:        munNameMap.TryGetValue(g.Key, out var mn) ? mn : null,
                    ProvinceCode:            provinceCode,
                    ProvinceName:            provinceCode != null && provNameMap.TryGetValue(provinceCode, out var pn) ? pn : null,
                    TotalPickups:            tot,
                    PeakHourPercent:         Math.Round(pp,   1),
                    OutsideDumWindowPercent: Math.Round(dumP, 1),
                    LogisticsIncidents:      incidents,
                    ConflictIndex:           Math.Round(ci,   1),
                    TrafficLight:            light);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.ConflictIndex)
            .ToList();

        // ── Comparativa pre/post ──────────────────────────────────────────────
        EfficiencyComparisonDto? comparison = null;
        if (request.CompareYear.HasValue)
        {
            var curRes = await _context.WasteMoveResidues
                .AsNoTracking()
                .Where(r => moveIds.Contains(r.IdWasteMove))
                .Select(r => new
                {
                    r.TransportInfo_TransportDistance,
                    r.TransportInfo_TransportDuration,
                    r.TransportInfo_TransportCarbonEmissions,
                    r.Weight
                })
                .ToListAsync(ct);

            var (cFrom, cTo) = BuildRange(request.CompareYear.Value, request.CompareMonth);
            var cmpMoveIds = await _context.WasteMoves
                .AsNoTracking()
                .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId) &&
                             (wm.ActualPickupStart >= cFrom && wm.ActualPickupStart < cTo ||
                              wm.PlannedPickupStart >= cFrom && wm.PlannedPickupStart < cTo))
                .Select(wm => wm.Id)
                .ToListAsync(ct);

            var cmpRes = await _context.WasteMoveResidues
                .AsNoTracking()
                .Where(r => cmpMoveIds.Contains(r.IdWasteMove))
                .Select(r => new
                {
                    r.TransportInfo_TransportDistance,
                    r.TransportInfo_TransportDuration,
                    r.TransportInfo_TransportCarbonEmissions,
                    r.Weight
                })
                .ToListAsync(ct);

            comparison = new EfficiencyComparisonDto(
                PeriodA:          FormatPeriod(request.Year, request.Month),
                PeriodB:          FormatPeriod(request.CompareYear.Value, request.CompareMonth),
                AvgDistanceKmA:   Avg(curRes, r => (double)(r.TransportInfo_TransportDistance ?? 0)),
                AvgDistanceKmB:   Avg(cmpRes, r => (double)(r.TransportInfo_TransportDistance ?? 0)),
                AvgDurationMinA:  Avg(curRes, r => (double)(r.TransportInfo_TransportDuration ?? 0)),
                AvgDurationMinB:  Avg(cmpRes, r => (double)(r.TransportInfo_TransportDuration ?? 0)),
                CO2ePerTonneA:    CO2PerTonne(curRes, r => (double)(r.TransportInfo_TransportCarbonEmissions ?? 0), r => (double)(r.Weight ?? 0)),
                CO2ePerTonneB:    CO2PerTonne(cmpRes, r => (double)(r.TransportInfo_TransportCarbonEmissions ?? 0), r => (double)(r.Weight ?? 0)),
                PeakHourPercentA: peakHourPercent,
                PeakHourPercentB: 0);
        }

        var recs = _recommendations.Generate(byMunicipality);

        return new MobilityCoordinatorAnalysisDto(
            WeeklyHeatmap:        heatmap,
            PeakHourPercent:      Math.Round(peakHourPercent, 1),
            ConflictIndex:        byMunicipality,
            EfficiencyComparison: comparison,
            Recommendations:      recs,
            Year:                 request.Year,
            Month:                request.Month);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsPeak(DateTime? dt, double ps1, double pe1, double ps2, double pe2)
    {
        if (!dt.HasValue) return false;
        double h = dt.Value.Hour + dt.Value.Minute / 60.0;
        return (h >= ps1 && h < pe1) || (h >= ps2 && h < pe2);
    }

    private static (DateTime From, DateTime To) BuildRange(int year, int? month)
    {
        if (month.HasValue)
        {
            var from = new DateTime(year, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            return (from, from.AddMonths(1));
        }
        var y = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (y, y.AddYears(1));
    }

    private static string FormatPeriod(int year, int? month)
        => month.HasValue
            ? $"{new DateTime(year, month.Value, 1):MMMM yyyy}"
            : year.ToString();

    private static decimal Avg<T>(IReadOnlyList<T> list, Func<T, double> selector)
        => list.Count > 0 ? (decimal)list.Average(selector) : 0;

    private static decimal CO2PerTonne<T>(IReadOnlyList<T> list, Func<T, double> co2, Func<T, double> kg)
    {
        double totalKg  = list.Sum(kg);
        double totalCO2 = list.Sum(co2);
        return totalKg > 0 ? (decimal)(totalCO2 / (totalKg / 1000.0)) : 0;
    }

    private static double Cfg(string? value, double def)
        => double.TryParse(value,
               System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture,
               out var v) ? v : def;
}
