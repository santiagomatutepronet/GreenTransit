using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.TratamientoReciclaje.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Reporting.TratamientoReciclaje.Queries;

/// <summary>
/// Dashboard TR-A — Análisis de Calidad y Revalorización (SCRAP).
/// Accesible para SCRAP y ADMIN.
/// </summary>
public sealed record GetTRScrapAnalysisQuery(
    int     Year,
    int?    Month            = null,
    Guid?   IdScrap          = null,
    string? ProvinceCode     = null,
    string? MunicipalityCode = null,
    string? IdLERCode        = null,
    string? TreatmentOperationCode = null
) : IRequest<TRScrapAnalysisDto>;

public sealed class GetTRScrapAnalysisQueryHandler
    : IRequestHandler<GetTRScrapAnalysisQuery, TRScrapAnalysisDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly IConfiguration        _config;

    public GetTRScrapAnalysisQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IConfiguration        config)
    {
        _context     = context;
        _currentUser = currentUser;
        _config      = config;
    }

    public async Task<TRScrapAnalysisDto> Handle(
        GetTRScrapAnalysisQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        var (dateFrom, dateTo) = BuildRange(request.Year, request.Month);

        // ── Filtro base de traslados ──────────────────────────────────────────
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId));

        if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: solo sus propios traslados
            var linkedId = _currentUser.LinkedEntityId;
            wmQuery = wmQuery.Where(wm => wm.IdScrap == linkedId || wm.IdScrap2 == linkedId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: traslados de sus SCRAPs vía Agreements
            var linkedId = _currentUser.LinkedEntityId;
            var scrapIdsForCoord = await _context.Agreements
                .AsNoTracking()
                .Where(a => a.IdCoordinator == linkedId)
                .Select(a => a.IdScrap)
                .Distinct()
                .ToListAsync(ct);
            wmQuery = wmQuery.Where(wm => scrapIdsForCoord.Contains(wm.IdScrap!.Value));

            // Filtro opcional de UI: solo si el SCRAP está en el subconjunto del coordinador
            if (request.IdScrap.HasValue && scrapIdsForCoord.Contains(request.IdScrap.Value))
                wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value
                                           || wm.IdScrap2 == request.IdScrap.Value);
        }
        else
        {
            // ADMIN / DISPATCH_OFFICE: todos los traslados del tenant
            if (request.IdScrap.HasValue)
                wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value
                                           || wm.IdScrap2 == request.IdScrap.Value);
        }

        var moveIds = await wmQuery.Select(wm => wm.Id).ToListAsync(ct);

        // ── Mapa moveId → IdDestination (planta física) ───────────────────────
        var moveDestinationMap = await wmQuery
            .Where(wm => wm.IdDestination.HasValue)
            .Select(wm => new { wm.Id, wm.IdDestination })
            .ToDictionaryAsync(wm => wm.Id, wm => wm.IdDestination!.Value, ct);

        var plantEntityIds = moveDestinationMap.Values.Distinct().ToList();
        var plantNameMap = await _context.BusinessEntities.AsNoTracking()
            .Where(e => plantEntityIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        // ── TreatmentPlants del periodo ───────────────────────────────────────
        var tpQuery = _context.TreatmentPlants.AsNoTracking()
            .Where(tp => moveIds.Contains(tp.IdWasteMove ?? Guid.Empty)
                      && tp.PlantTreatmentDate >= dateFrom
                      && tp.PlantTreatmentDate <  dateTo);

        if (!string.IsNullOrEmpty(request.TreatmentOperationCode))
        {
            tpQuery = tpQuery.Where(tp =>
                tp.TreatmentOperation != null &&
                tp.TreatmentOperation.Code == request.TreatmentOperationCode);
        }

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

        // ── TreatmentPlantResidues ────────────────────────────────────────────
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
                r.IdResidue,
                ResidueName = r.Residue != null ? r.Residue.Name : null,
                LERCode     = r.Residue != null && r.Residue.LerCode != null ? r.Residue.LerCode.Code : null,
                r.WeightTotal,
                r.WeightReused,
                r.WeightValued,
                r.WeightRemove
            })
            .ToListAsync(ct);

        // ── Balance por planta ────────────────────────────────────────────────
        // Agrupar por planta destino (entidad física), no por registro TreatmentPlant
        var tpIdToMoveId = treatments.ToDictionary(t => t.Id, t => t.IdWasteMove ?? Guid.Empty);

        var balanceByPlant = tpr
            .GroupBy(r =>
            {
                var moveId = tpIdToMoveId.GetValueOrDefault(r.IdTreatmentPlant);
                return moveDestinationMap.TryGetValue(moveId, out var destId) ? destId : r.IdTreatmentPlant;
            })
            .Select(g =>
            {
                var totalReused  = g.Sum(r => r.WeightReused  ?? 0m);
                var totalValued  = g.Sum(r => r.WeightValued  ?? 0m);
                var totalRemove  = g.Sum(r => r.WeightRemove  ?? 0m);
                var totalWeight  = g.Sum(r => r.WeightTotal   ?? 0m);
                var rate         = totalWeight > 0 ? (double)((totalReused + totalValued) / totalWeight * 100m) : 0d;
                var plantName    = plantNameMap.TryGetValue(g.Key, out var n) ? n : g.Key.ToString()[..8];
                return new TreatmentBalanceDto(
                    g.Key,
                    plantName,
                    totalReused, totalValued, totalRemove, totalWeight, rate);
            })
            .OrderByDescending(b => b.RecyclingRate)
            .ToList();

        // ── Tasa de revalorización por tipo de residuo ────────────────────────
        var recyclingByResidue = tpr
            .GroupBy(r => new { r.LERCode, r.ResidueName })
            .Select(g =>
            {
                var totalReused  = g.Sum(r => r.WeightReused  ?? 0m);
                var totalValued  = g.Sum(r => r.WeightValued  ?? 0m);
                var totalWeight  = g.Sum(r => r.WeightTotal   ?? 0m);
                var rate         = totalWeight > 0 ? (double)((totalReused + totalValued) / totalWeight * 100m) : 0d;
                var light        = rate > 70 ? "Green" : rate >= 40 ? "Orange" : "Red";
                return new RecyclingRateByResidueDto(
                    g.Key.LERCode ?? "—",
                    g.Key.ResidueName ?? "—",
                    totalWeight, totalReused, totalValued, rate, light);
            })
            .OrderByDescending(r => r.RecyclingRate)
            .ToList();

        // ── Evolución mensual (últimos 12 meses) ──────────────────────────────
        var last12From = dateTo.AddMonths(-12);
        var trendTreatments = await _context.TreatmentPlants.AsNoTracking()
            .Where(tp => moveIds.Contains(tp.IdWasteMove ?? Guid.Empty)
                      && tp.PlantTreatmentDate >= last12From
                      && tp.PlantTreatmentDate <  dateTo)
            .Select(tp => new
            {
                tp.Id,
                tp.PlantTreatmentDate,
                tp.ImproperWeight
            })
            .ToListAsync(ct);

        var trendTpIds = trendTreatments.Select(t => t.Id).ToList();
        var trendResidues = await _context.TreatmentPlantResidues.AsNoTracking()
            .Where(r => trendTpIds.Contains(r.IdTreatmentPlant))
            .Select(r => new { r.IdTreatmentPlant, r.WeightTotal, r.WeightReused, r.WeightValued })
            .ToListAsync(ct);

        var tpDateMap = trendTreatments.ToDictionary(t => t.Id, t => t.PlantTreatmentDate);
        var tpImproperMap = trendTreatments.ToDictionary(t => t.Id, t => t.ImproperWeight ?? 0m);

        var monthlyTrend = trendResidues
            .GroupBy(r =>
            {
                var d = tpDateMap.GetValueOrDefault(r.IdTreatmentPlant);
                return d.HasValue ? $"{d.Value.Year:0000}-{d.Value.Month:00}" : "—";
            })
            .Where(g => g.Key != "—")
            .Select(g =>
            {
                var totalReused  = g.Sum(r => r.WeightReused ?? 0m);
                var totalValued  = g.Sum(r => r.WeightValued ?? 0m);
                var totalWeight  = g.Sum(r => r.WeightTotal  ?? 0m);
                var rate         = totalWeight > 0 ? (double)((totalReused + totalValued) / totalWeight * 100m) : 0d;
                var improper     = g.Sum(r => tpImproperMap.GetValueOrDefault(r.IdTreatmentPlant));
                var improperRate = totalWeight > 0 ? (double)(improper / totalWeight * 100m) : 0d;
                return new MonthlyRecyclingTrendDto(g.Key, rate, improperRate, totalValued);
            })
            .OrderBy(t => t.Period)
            .ToList();

        // ── Comparativa Multi-SCRAP ───────────────────────────────────────────
        var scrapIds = await wmQuery
            .Where(wm => wm.IdScrap.HasValue)
            .Select(wm => wm.IdScrap!.Value)
            .Distinct()
            .ToListAsync(ct);

        var scrapNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var moveScrapMap = await wmQuery
            .Where(wm => wm.IdScrap.HasValue)
            .Select(wm => new { wm.Id, wm.IdScrap })
            .ToDictionaryAsync(wm => wm.Id, wm => wm.IdScrap!.Value, ct);

        var openIncidentIds = treatments.Where(t => t.IncidentId.HasValue).Select(t => t.IncidentId!.Value).ToHashSet();
        var openIncidentsByMove = await _context.Incidents.AsNoTracking()
            .Where(i => openIncidentIds.Contains(i.Id) && i.ClosedAt == null)
            .Select(i => new { i.Id })
            .ToListAsync(ct);
        var openIncidentSet = openIncidentsByMove.Select(i => i.Id).ToHashSet();

        var scrapComparison = scrapIds.Select(scrapId =>
        {
            var scrapMoveIds = moveScrapMap
                .Where(kv => kv.Value == scrapId)
                .Select(kv => kv.Key)
                .ToHashSet();

            var scrapTpIds = treatments
                .Where(t => t.IdWasteMove.HasValue && scrapMoveIds.Contains(t.IdWasteMove.Value))
                .Select(t => t.Id)
                .ToHashSet();

            var scrapResidues = tpr.Where(r => scrapTpIds.Contains(r.IdTreatmentPlant)).ToList();
            var totalW  = scrapResidues.Sum(r => r.WeightTotal  ?? 0m);
            var reused  = scrapResidues.Sum(r => r.WeightReused ?? 0m);
            var valued  = scrapResidues.Sum(r => r.WeightValued ?? 0m);
            var rate    = totalW > 0 ? (double)((reused + valued) / totalW * 100m) : 0d;
            var improper = treatments.Where(t => scrapTpIds.Contains(t.Id)).Sum(t => t.ImproperWeight ?? 0m);
            var improperRate = totalW > 0 ? (double)(improper / totalW * 100m) : 0d;
            var incidents = treatments.Count(t => scrapTpIds.Contains(t.Id)
                                                && t.IncidentId.HasValue
                                                && openIncidentSet.Contains(t.IncidentId.Value));
            return new TRScrapComparisonDto(
                scrapId,
                scrapNames.GetValueOrDefault(scrapId) ?? scrapId.ToString()[..8],
                scrapMoveIds.Count, totalW, rate, improperRate, incidents);
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
                var totalW = tpr.Where(r => tpIdsInGroup.Contains(r.IdTreatmentPlant)).Sum(r => r.WeightTotal ?? 0m);
                var opType = g.Key.OperationCode!.StartsWith("R") ? "R" : "D";
                return new TreatmentOperationsDistributionDto(
                    g.Key.OperationCode!, g.Key.OperationDesc ?? g.Key.OperationCode!, opType, totalW);
            })
            .OrderByDescending(o => o.WeightTotal)
            .ToList();

        // ── Incidencias abiertas ──────────────────────────────────────────────
        var tpWithIncident = treatments.Where(t => t.IncidentId.HasValue).ToList();
        var incidentIds = tpWithIncident.Select(t => t.IncidentId!.Value).ToList();
        var openIncidents = await _context.Incidents.AsNoTracking()
            .Where(i => incidentIds.Contains(i.Id) && i.ClosedAt == null)
            .Select(i => new
            {
                i.Id, i.Type, i.Severity, i.OpenedAt, i.WasteMoveReference
            })
            .ToListAsync(ct);

        var incidentDtos = openIncidents.Select(i => new TRIncidentDto(
            i.Id, i.WasteMoveReference, i.Type, i.Severity,
            i.OpenedAt, (int)(DateTime.UtcNow - i.OpenedAt).TotalDays, null))
            .ToList();

        return new TRScrapAnalysisDto(
            balanceByPlant,
            recyclingByResidue,
            monthlyTrend,
            scrapComparison,
            opsDistribution,
            incidentDtos,
            request.Year,
            request.Month);
    }

    private static (DateTime From, DateTime To) BuildRange(int year, int? month)
    {
        if (month.HasValue)
            return (new DateTime(year, month.Value, 1),
                    new DateTime(year, month.Value, 1).AddMonths(1));
        return (new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
    }
}
