using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.TratamientoReciclaje.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Reporting.TratamientoReciclaje.Queries;

/// <summary>
/// Dashboard TR-B — Monitorización de Reciclaje Municipal (Ayuntamiento).
/// Accesible para PUBLIC_ENT y ADMIN.
/// </summary>
public sealed record GetTRMunicipalMonitoringQuery(
    int     Year,
    int?    Month            = null,
    Guid?   IdScrap          = null,
    string? IdLERCode        = null
) : IRequest<TRMunicipalMonitoringDto>;

public sealed class GetTRMunicipalMonitoringQueryHandler
    : IRequestHandler<GetTRMunicipalMonitoringQuery, TRMunicipalMonitoringDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly IConfiguration        _config;

    public GetTRMunicipalMonitoringQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IConfiguration        config)
    {
        _context     = context;
        _currentUser = currentUser;
        _config      = config;
    }

    public async Task<TRMunicipalMonitoringDto> Handle(
        GetTRMunicipalMonitoringQuery request, CancellationToken ct)
    {
        var ownerId   = _currentUser.OwnerId;
        var (dateFrom, dateTo) = BuildRange(request.Year, request.Month);
        var (prevFrom, prevTo) = BuildRange(request.Year - 1, request.Month);

        double minRecycling = double.Parse(
            _config["RecyclingSettings:MinRecyclingRateThreshold"] ?? "50",
            System.Globalization.CultureInfo.InvariantCulture);
        double maxImproper = double.Parse(
            _config["RecyclingSettings:MaxImproperRateThreshold"] ?? "15",
            System.Globalization.CultureInfo.InvariantCulture);

        // ── Traslados del municipio ───────────────────────────────────────────
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId));

        if (_currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            var linkedId = _currentUser.LinkedEntityId;

            // Obtener el MunicipalityCode de la entidad pública vinculada
            var municipalityCode = await _context.BusinessEntities
                .AsNoTracking()
                .Where(e => e.Id == linkedId)
                .Select(e => e.MunicipalityCode)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrEmpty(municipalityCode))
            {
                // Traslados cuyo origen pertenece al municipio O cuya SO fue emitida por la entidad
                var sourceIdsInMunicipality = await _context.BusinessEntities
                    .AsNoTracking()
                    .Where(e => e.MunicipalityCode == municipalityCode)
                    .Select(e => e.Id)
                    .ToListAsync(ct);

                var soIdsIssuedBy = await _context.ServiceOrders
                    .AsNoTracking()
                    .Where(so => so.IdIssuedBy == linkedId
                              && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                    .Select(so => so.Id)
                    .ToListAsync(ct);

                wmQuery = wmQuery.Where(wm =>
                    (wm.IdSource.HasValue && sourceIdsInMunicipality.Contains(wm.IdSource.Value))
                    || (wm.ServiceOrderId.HasValue && soIdsIssuedBy.Contains(wm.ServiceOrderId.Value)));
            }
            else
            {
                // Sin municipio configurado: solo traslados de SOs emitidas por la entidad
                wmQuery = wmQuery.Where(wm =>
                    wm.ServiceOrder != null && wm.ServiceOrder.IdIssuedBy == linkedId);
            }
        }
        // ADMIN y DISPATCH_OFFICE: todos los traslados del tenant sin restricción adicional

        if (request.IdScrap.HasValue)
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value);

        var moveIds = await wmQuery.Select(wm => wm.Id).ToListAsync(ct);

        // ── TreatmentPlants del periodo ───────────────────────────────────────
        var tpQuery = _context.TreatmentPlants.AsNoTracking()
            .Where(tp => moveIds.Contains(tp.IdWasteMove ?? Guid.Empty)
                      && tp.PlantTreatmentDate >= dateFrom
                      && tp.PlantTreatmentDate <  dateTo);

        var treatments = await tpQuery
            .Select(tp => new
            {
                tp.Id,
                tp.IdWasteMove,
                tp.PlantTreatmentDate,
                tp.ImproperWeight,
                tp.IdTreatmentOperation,
                OperationCode = tp.TreatmentOperation != null ? tp.TreatmentOperation.Code : null,
                OperationDesc = tp.TreatmentOperation != null ? tp.TreatmentOperation.Description : null,
                tp.IncidentId
            })
            .ToListAsync(ct);

        var tpIds = treatments.Select(t => t.Id).ToList();

        var residuesQuery = _context.TreatmentPlantResidues.AsNoTracking()
            .Where(r => tpIds.Contains(r.IdTreatmentPlant));

        if (!string.IsNullOrEmpty(request.IdLERCode))
        {
            residuesQuery = residuesQuery.Where(r =>
                r.Residue != null && r.Residue.LerCode != null &&
                r.Residue.LerCode.Code == request.IdLERCode);
        }

        var tpr = await residuesQuery
            .Select(r => new
            {
                r.IdTreatmentPlant,
                r.WeightTotal,
                r.WeightReused,
                r.WeightValued,
                r.WeightRemove
            })
            .ToListAsync(ct);

        // ── KPI Cards ─────────────────────────────────────────────────────────
        decimal totalW  = tpr.Sum(r => r.WeightTotal  ?? 0m);
        decimal reused  = tpr.Sum(r => r.WeightReused ?? 0m);
        decimal valued  = tpr.Sum(r => r.WeightValued ?? 0m);
        decimal remove  = tpr.Sum(r => r.WeightRemove ?? 0m);
        decimal improper = treatments.Sum(t => t.ImproperWeight ?? 0m);

        double recyclingRate = totalW > 0 ? (double)((reused + valued) / totalW * 100m) : 0d;
        double improperRate  = totalW > 0 ? (double)(improper / totalW * 100m) : 0d;

        // Periodo anterior
        var prevTpIds = await _context.TreatmentPlants.AsNoTracking()
            .Where(tp => moveIds.Contains(tp.IdWasteMove ?? Guid.Empty)
                      && tp.PlantTreatmentDate >= prevFrom
                      && tp.PlantTreatmentDate <  prevTo)
            .Select(tp => tp.Id)
            .ToListAsync(ct);

        var prevTpr = await _context.TreatmentPlantResidues.AsNoTracking()
            .Where(r => prevTpIds.Contains(r.IdTreatmentPlant))
            .Select(r => new { r.WeightTotal, r.WeightReused, r.WeightValued })
            .ToListAsync(ct);

        decimal prevTotalW  = prevTpr.Sum(r => r.WeightTotal  ?? 0m);
        decimal prevReused  = prevTpr.Sum(r => r.WeightReused ?? 0m);
        decimal prevValued  = prevTpr.Sum(r => r.WeightValued ?? 0m);
        double  prevRate    = prevTotalW > 0 ? (double)((prevReused + prevValued) / prevTotalW * 100m) : 0d;

        double? rateChange  = prevRate > 0 ? (recyclingRate - prevRate) / prevRate * 100 : null;
        double? wChange     = prevTotalW > 0 ? ((double)(totalW - prevTotalW) / (double)prevTotalW * 100) : null;

        var kpiCards = new List<TRKpiCardDto>
        {
            new("Kg tratados",       (double)totalW,           "kg",  (double?)prevTotalW, wChange),
            new("Tasa de reciclaje", recyclingRate,            "%",   prevRate,             rateChange),
            new("% Impropios",       improperRate,             "%",   null,                 null),
            new("Kg revalorizados",  (double)(reused + valued), "kg",  null,                null)
        };

        // ── Histórico mensual ─────────────────────────────────────────────────
        var last12From = dateTo.AddMonths(-12);
        var trendTps = await _context.TreatmentPlants.AsNoTracking()
            .Where(tp => moveIds.Contains(tp.IdWasteMove ?? Guid.Empty)
                      && tp.PlantTreatmentDate >= last12From
                      && tp.PlantTreatmentDate <  dateTo)
            .Select(tp => new { tp.Id, tp.PlantTreatmentDate, tp.ImproperWeight })
            .ToListAsync(ct);

        var trendTpIdSet = trendTps.Select(t => t.Id).ToList();
        var trendTpr = await _context.TreatmentPlantResidues.AsNoTracking()
            .Where(r => trendTpIdSet.Contains(r.IdTreatmentPlant))
            .Select(r => new { r.IdTreatmentPlant, r.WeightTotal, r.WeightReused, r.WeightValued })
            .ToListAsync(ct);

        var tpDateMap = trendTps.ToDictionary(t => t.Id, t => t.PlantTreatmentDate);
        var tpImprMap = trendTps.ToDictionary(t => t.Id, t => t.ImproperWeight ?? 0m);

        var monthlyTrend = trendTpr
            .GroupBy(r =>
            {
                var d = tpDateMap.GetValueOrDefault(r.IdTreatmentPlant);
                return d.HasValue ? $"{d.Value.Year:0000}-{d.Value.Month:00}" : "—";
            })
            .Where(g => g.Key != "—")
            .Select(g =>
            {
                var tw = g.Sum(r => r.WeightTotal  ?? 0m);
                var rv = g.Sum(r => r.WeightReused ?? 0m);
                var vl = g.Sum(r => r.WeightValued ?? 0m);
                var rr = tw > 0 ? (double)((rv + vl) / tw * 100m) : 0d;
                var impr = g.Sum(r => tpImprMap.GetValueOrDefault(r.IdTreatmentPlant));
                var ir   = tw > 0 ? (double)(impr / tw * 100m) : 0d;
                return new MonthlyRecyclingTrendDto(g.Key, rr, ir, vl);
            })
            .OrderBy(t => t.Period)
            .ToList();

        // ── Detalle por SCRAP ─────────────────────────────────────────────────
        var scrapIds = await wmQuery.Where(wm => wm.IdScrap.HasValue)
            .Select(wm => wm.IdScrap!.Value).Distinct().ToListAsync(ct);
        var scrapNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var moveScrapMap = await wmQuery.Where(wm => wm.IdScrap.HasValue)
            .Select(wm => new { wm.Id, wm.IdScrap })
            .ToDictionaryAsync(wm => wm.Id, wm => wm.IdScrap!.Value, ct);

        var openIncidentIds = treatments.Where(t => t.IncidentId.HasValue)
            .Select(t => t.IncidentId!.Value).ToList();
        var openIncidentSet = await _context.Incidents.AsNoTracking()
            .Where(i => openIncidentIds.Contains(i.Id) && i.ClosedAt == null)
            .Select(i => i.Id).ToHashSetAsync(ct);

        var scrapDetails = scrapIds.Select(scrapId =>
        {
            var scrapMoveIds = moveScrapMap.Where(kv => kv.Value == scrapId).Select(kv => kv.Key).ToHashSet();
            var scrapTpIds   = treatments.Where(t => t.IdWasteMove.HasValue && scrapMoveIds.Contains(t.IdWasteMove.Value)).Select(t => t.Id).ToHashSet();
            var scrapTpr     = tpr.Where(r => scrapTpIds.Contains(r.IdTreatmentPlant)).ToList();
            var tw = scrapTpr.Sum(r => r.WeightTotal  ?? 0m);
            var rv = scrapTpr.Sum(r => r.WeightReused ?? 0m);
            var vl = scrapTpr.Sum(r => r.WeightValued ?? 0m);
            var rr = tw > 0 ? (double)((rv + vl) / tw * 100m) : 0d;
            var impr = treatments.Where(t => scrapTpIds.Contains(t.Id)).Sum(t => t.ImproperWeight ?? 0m);
            var ir   = tw > 0 ? (double)(impr / tw * 100m) : 0d;
            var light = rr > 70 ? "Green" : rr >= 40 ? "Orange" : "Red";
            var openInc = treatments.Count(t => scrapTpIds.Contains(t.Id)
                                             && t.IncidentId.HasValue
                                             && openIncidentSet.Contains(t.IncidentId.Value));
            return new TRMunicipalScrapDetailDto(
                scrapId, scrapNames.GetValueOrDefault(scrapId) ?? scrapId.ToString()[..8],
                scrapMoveIds.Count, tw, rr, ir, openInc, light);
        })
        .OrderByDescending(s => s.RecyclingRate)
        .ToList();

        // ── Distribución de operaciones R/D ────────────────────────────────────
        var opsDistribution = treatments
            .Where(t => t.OperationCode != null)
            .GroupBy(t => new { t.OperationCode, t.OperationDesc })
            .Select(g =>
            {
                var tpIdsInGroup = g.Select(t => t.Id).ToHashSet();
                var tw = tpr.Where(r => tpIdsInGroup.Contains(r.IdTreatmentPlant)).Sum(r => r.WeightTotal ?? 0m);
                var opType = g.Key.OperationCode!.StartsWith("R") ? "R" : "D";
                return new TreatmentOperationsDistributionDto(
                    g.Key.OperationCode!, g.Key.OperationDesc ?? g.Key.OperationCode!, opType, tw);
            })
            .OrderByDescending(o => o.WeightTotal)
            .ToList();

        // ── Alertas de calidad ────────────────────────────────────────────────
        var alerts = new List<TRQualityAlertDto>();
        if (recyclingRate < minRecycling)
            alerts.Add(new TRQualityAlertDto(
                "LowRecyclingRate",
                $"La tasa de reciclaje ({recyclingRate:F1}%) está por debajo del umbral mínimo del {minRecycling}%.",
                "Warning", null, null));
        if (improperRate > maxImproper)
            alerts.Add(new TRQualityAlertDto(
                "HighImproperRate",
                $"El porcentaje de impropios ({improperRate:F1}%) supera el umbral máximo del {maxImproper}%.",
                "Danger", null, null));
        foreach (var s in scrapDetails.Where(s => s.OpenIncidents > 0))
            alerts.Add(new TRQualityAlertDto(
                "OpenIncidents",
                $"El SCRAP {s.ScrapName} tiene {s.OpenIncidents} incidencia(s) de tratamiento abierta(s).",
                "Info", s.ScrapName, null));

        return new TRMunicipalMonitoringDto(
            kpiCards, monthlyTrend, scrapDetails, opsDistribution, alerts,
            request.Year, request.Month);
    }

    private static (DateTime From, DateTime To) BuildRange(int year, int? month)
    {
        if (month.HasValue)
            return (new DateTime(year, month.Value, 1),
                    new DateTime(year, month.Value, 1).AddMonths(1));
        return (new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
    }
}
