using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Mobility.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Mobility.Queries;

/// <summary>
/// Devuelve los KPIs del Dashboard UC3-B — Monitorización de Movilidad para Ayuntamientos.
/// Para PUBLIC_ENT restringe los datos al municipio de su entidad vinculada.
/// </summary>
public sealed record GetMobilityMunicipalMonitoringQuery(
    int     Year,
    int?    Month       = null,
    Guid?   IdScrap     = null,
    string? WasteStream = null
) : IRequest<MobilityMunicipalMonitoringDto>;

public sealed class GetMobilityMunicipalMonitoringQueryHandler
    : IRequestHandler<GetMobilityMunicipalMonitoringQuery, MobilityMunicipalMonitoringDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly IConfiguration        _config;

    public GetMobilityMunicipalMonitoringQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IConfiguration        config)
    {
        _context     = context;
        _currentUser = currentUser;
        _config      = config;
    }

    public async Task<MobilityMunicipalMonitoringDto> Handle(
        GetMobilityMunicipalMonitoringQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;
        var isAdmin        = _currentUser.IsInProfile(ProfileConstants.Admin);
        var isPublicEnt    = !isAdmin && _currentUser.IsInProfile(ProfileConstants.PublicEnt);

        var (dateFrom, dateTo) = BuildRange(request.Year, request.Month);

        var s      = _config.GetSection("MobilitySettings");
        double ps1 = Cfg(s["PeakHourStart1"], 7.5);
        double pe1 = Cfg(s["PeakHourEnd1"],   9.5);
        double ps2 = Cfg(s["PeakHourStart2"], 17.5);
        double pe2 = Cfg(s["PeakHourEnd2"],   19.5);

        // ── Traslados del periodo ─────────────────────────────────────────────
        var wmQuery = _context.WasteMoves
            .AsNoTracking()
            .Where(wm => ownerId == Guid.Empty || wm.OwnerId == ownerId);

        if (!string.IsNullOrEmpty(request.WasteStream))
            wmQuery = wmQuery.Where(wm => wm.ServiceOrder != null &&
                                          wm.ServiceOrder.WasteStream == request.WasteStream);

        if (request.IdScrap.HasValue)
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value);

        if (isPublicEnt && linkedEntityId.HasValue)
        {
            var lid = linkedEntityId.Value;

            // Obtener el MunicipalityCode de la entidad pública para ampliar el filtro
            var municipalityCode = await _context.BusinessEntities
                .AsNoTracking()
                .Where(e => e.Id == lid)
                .Select(e => e.MunicipalityCode)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrEmpty(municipalityCode))
            {
                // Incluye traslados emitidos por la entidad pública O cuyo punto de
                // recogida pertenece al mismo municipio (IdPickupPoint → MunicipalityCode)
                wmQuery = wmQuery.Where(wm =>
                    (wm.ServiceOrder != null && wm.ServiceOrder.IdIssuedBy == lid) ||
                    (wm.ServiceOrder != null &&
                     wm.ServiceOrder.PickupPoint != null &&
                     wm.ServiceOrder.PickupPoint.MunicipalityCode == municipalityCode));
            }
            else
            {
                // Sin municipio configurado: solo los emitidos por la entidad
                wmQuery = wmQuery.Where(wm =>
                    wm.ServiceOrder != null && wm.ServiceOrder.IdIssuedBy == lid);
            }
        }

        var wmCurrent = wmQuery.Where(wm =>
            (wm.ActualPickupStart >= dateFrom  && wm.ActualPickupStart < dateTo) ||
            (wm.ActualPickupStart == null && wm.PlannedPickupStart >= dateFrom && wm.PlannedPickupStart < dateTo));

        var movesRaw = await wmCurrent
            .Select(wm => new
            {
                wm.Id,
                wm.IdScrap,
                wm.WasteMoveReference,
                PickupDate = wm.ActualPickupStart ?? wm.PlannedPickupStart
            })
            .ToListAsync(ct);

        // ── SCRAP names ───────────────────────────────────────────────────────
        var scrapIds = movesRaw
            .Where(m => m.IdScrap.HasValue)
            .Select(m => m.IdScrap!.Value)
            .Distinct()
            .ToList();

        var scrapNames = await _context.BusinessEntities
            .AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name ?? e.Id.ToString(), ct);

        // ── KPI cards ─────────────────────────────────────────────────────────
        int totalPickups = movesRaw.Count;
        int peakCount    = movesRaw.Count(m => IsPeak(m.PickupDate, ps1, pe1, ps2, pe2));
        double peakPct    = totalPickups > 0 ? 100.0 * peakCount / totalPickups : 0;
        double outPeakPct = 100.0 - peakPct;

        var moveIdsList = movesRaw.Select(m => m.Id).ToList();
        decimal totalKg = await _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => moveIdsList.Contains(r.IdWasteMove))
            .SumAsync(r => r.Weight ?? 0, ct);

        var (prevFrom, prevTo) = request.Month.HasValue
            ? BuildRange(request.Year, request.Month.Value > 1 ? request.Month.Value - 1 : 12)
            : BuildRange(request.Year - 1, null);

        var prevMoveCount = await wmQuery.CountAsync(wm =>
            (wm.ActualPickupStart >= prevFrom && wm.ActualPickupStart < prevTo) ||
            (wm.ActualPickupStart == null && wm.PlannedPickupStart >= prevFrom && wm.PlannedPickupStart < prevTo), ct);

        double? pickupTrend = prevMoveCount > 0
            ? (double)(totalPickups - prevMoveCount) / prevMoveCount * 100
            : null;

        var kpiCards = new List<MobilityKpiCardDto>
        {
            new("Recogidas del periodo",          (double)totalPickups, "recogidas", pickupTrend),
            new("Kg RAEE recogidos",              (double)totalKg,      "kg",        null),
            new("Recogidas fuera de hora pico",   outPeakPct,           "%",         null),
            new("Cumplimiento DUM estimado",      outPeakPct,           "%",         null)
        };

        // ── Recogidas planificadas ────────────────────────────────────────────
        var soQuery = _context.ServiceOrders
            .AsNoTracking()
            .Where(so => (ownerId == Guid.Empty || so.OwnerId == ownerId) &&
                         (so.Status == "Pending" || so.Status == "Scheduled") &&
                         so.PlannedPickupStart >= DateTime.UtcNow &&
                         so.PlannedPickupStart < dateTo);

        if (isPublicEnt && linkedEntityId.HasValue)
        {
            var lid = linkedEntityId.Value;
            soQuery = soQuery.Where(so => so.IdIssuedBy == lid);
        }

        var plannedSOs = await soQuery
            .OrderBy(so => so.PlannedPickupStart)
            .Take(50)
            .Select(so => new
            {
                so.Id,
                so.ServiceOrderNumber,
                so.PlannedPickupStart,
                so.PlannedPickupEnd
            })
            .ToListAsync(ct);

        var plannedPickups = plannedSOs.Select(so =>
        {
            bool inPeak = IsPeak(so.PlannedPickupStart, ps1, pe1, ps2, pe2);
            return new PlannedPickupCalendarItemDto(
                ServiceOrderId:     so.Id,
                ServiceOrderNumber: so.ServiceOrderNumber,
                PlannedPickupStart: so.PlannedPickupStart,
                PlannedPickupEnd:   so.PlannedPickupEnd,
                ScrapName:          "—",
                TrafficLight:       inPeak ? "Red" : "Green");
        }).ToList();

        // ── Histórico mensual (últimos 12 meses) ──────────────────────────────
        var histFrom = dateFrom.AddMonths(-11);
        var histMoves = await wmQuery
            .Where(wm =>
                (wm.ActualPickupStart >= histFrom  && wm.ActualPickupStart < dateTo) ||
                (wm.ActualPickupStart == null && wm.PlannedPickupStart >= histFrom && wm.PlannedPickupStart < dateTo))
            .Select(wm => new
            {
                PickupDate = wm.ActualPickupStart ?? wm.PlannedPickupStart,
                wm.WasteMoveReference
            })
            .ToListAsync(ct);

        var incRefs = histMoves
            .Where(m => m.WasteMoveReference != null)
            .Select(m => m.WasteMoveReference!)
            .Distinct()
            .ToList();

        var incByPeriod = await _context.Incidents
            .AsNoTracking()
            .Where(i => incRefs.Contains(i.WasteMoveReference!))
            .Select(i => new { i.OpenedAt.Year, i.OpenedAt.Month })
            .ToListAsync(ct);

        var historicalSeries = histMoves
            .GroupBy(m => new
            {
                Year  = m.PickupDate.HasValue ? m.PickupDate.Value.Year  : 0,
                Month = m.PickupDate.HasValue ? m.PickupDate.Value.Month : 0
            })
            .Where(g => g.Key.Year > 0)
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var incCount = incByPeriod.Count(i => i.Year == g.Key.Year && i.Month == g.Key.Month);
                return new PickupIncidentSeriesDto(
                    Period:        $"{g.Key.Year:0000}-{g.Key.Month:00}",
                    PickupCount:   g.Count(),
                    IncidentCount: incCount);
            })
            .ToList();

        // ── Cumplimiento por SCRAP ────────────────────────────────────────────
        var scrapCompliance = movesRaw
            .Where(m => m.IdScrap.HasValue)
            .GroupBy(m => m.IdScrap!.Value)
            .Select(g =>
            {
                int tot   = g.Count();
                int peak  = g.Count(m => IsPeak(m.PickupDate, ps1, pe1, ps2, pe2));
                double pp = tot > 0 ? 100.0 * peak / tot : 0;
                double ci = pp * 0.7;
                return new ScrapMobilityComplianceRowDto(
                    IdScrap:              g.Key,
                    ScrapName:            scrapNames.TryGetValue(g.Key, out var n) ? n : g.Key.ToString(),
                    TotalMoves:           tot,
                    TotalKg:              0,
                    PeakHourPercent:      Math.Round(pp, 1),
                    DumCompliancePercent: Math.Round(100 - pp, 1),
                    OpenIncidents:        0,
                    ConflictIndex:        Math.Round(ci, 1),
                    TrafficLight:         ci >= 70 ? "Red" : ci >= 40 ? "Orange" : "Green");
            })
            .OrderByDescending(r => r.ConflictIndex)
            .ToList();

        // ── Notificaciones ────────────────────────────────────────────────────
        var notifications = plannedPickups
            .Where(p => p.TrafficLight != "Green" && p.PlannedPickupStart.HasValue)
            .Select(p => new MobilityNotificationDto(
                Type:        "PeakHourPickup",
                Message:     $"Recogida {p.ServiceOrderNumber} planificada en hora pico el {p.PlannedPickupStart!.Value:dd/MM/yyyy HH:mm}.",
                GeneratedAt: DateTime.UtcNow,
                Severity:    "Warning"))
            .ToList();

        return new MobilityMunicipalMonitoringDto(
            KpiCards:            kpiCards,
            PlannedPickups:      plannedPickups,
            HistoricalSeries:    historicalSeries,
            ScrapCompliance:     scrapCompliance,
            ActiveNotifications: notifications,
            Year:                request.Year,
            Month:               request.Month);
    }

    private static bool IsPeak(DateTime? dt, double ps1, double pe1, double ps2, double pe2)
    {
        if (!dt.HasValue) return false;
        double h = dt.Value.Hour + dt.Value.Minute / 60.0;
        return (h >= ps1 && h < pe1) || (h >= ps2 && h < pe2);
    }

    private static (DateTime From, DateTime To) BuildRange(int year, int? month)
    {
        int m = month.HasValue ? Math.Clamp(month.Value, 1, 12) : 1;
        var from = month.HasValue
            ? new DateTime(year, m, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return month.HasValue ? (from, from.AddMonths(1)) : (from, from.AddYears(1));
    }

    private static double Cfg(string? v, double d)
        => double.TryParse(v,
               System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture,
               out var r) ? r : d;
}
