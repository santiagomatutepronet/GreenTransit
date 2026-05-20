using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.TratamientoReciclaje.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.TratamientoReciclaje.Queries;

/// <summary>
/// Vista TR-D — Datos Operativos de Tratamiento y Reciclaje (Oficina de Asignación).
/// Accesible para DISPATCH_OFFICE y ADMIN.
/// </summary>
public sealed record GetTRDispatchDataQuery(
    int     Year,
    int?    Month            = null,
    Guid?   IdScrap          = null,
    string? ProvinceCode     = null,
    string? MunicipalityCode = null,
    string? IdLERCode        = null
) : IRequest<TRDispatchDataDto>;

public sealed class GetTRDispatchDataQueryHandler
    : IRequestHandler<GetTRDispatchDataQuery, TRDispatchDataDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetTRDispatchDataQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<TRDispatchDataDto> Handle(
        GetTRDispatchDataQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        var (dateFrom, dateTo) = BuildRange(request.Year, request.Month);

        // DISPATCH_OFFICE y ADMIN ven todos los traslados del tenant
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId));

        if (request.IdScrap.HasValue)
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value);

        var moves = await wmQuery
            .Select(wm => new { wm.Id, wm.IdScrap, wm.WasteMoveReference })
            .ToListAsync(ct);
        var moveIds     = moves.Select(m => m.Id).ToList();
        var moveScrapMap = moves.ToDictionary(m => m.Id, m => m.IdScrap);
        var moveRefMap   = moves.ToDictionary(m => m.Id, m => m.WasteMoveReference);

        // ── TreatmentPlants del periodo ───────────────────────────────────────
        var treatments = await _context.TreatmentPlants.AsNoTracking()
            .Where(tp => moveIds.Contains(tp.IdWasteMove ?? Guid.Empty)
                      && tp.PlantTreatmentDate >= dateFrom
                      && tp.PlantTreatmentDate <  dateTo)
            .Select(tp => new
            {
                tp.Id,
                tp.IdWasteMove,
                tp.PlantTreatmentDate,
                tp.ImproperWeight,
                OperationCode = tp.TreatmentOperation != null ? tp.TreatmentOperation.Code : null,
                OperationDesc = tp.TreatmentOperation != null ? tp.TreatmentOperation.Description : null,
                tp.IncidentId
            })
            .ToListAsync(ct);

        var tpIds = treatments.Select(t => t.Id).ToList();

        var residuesQuery = _context.TreatmentPlantResidues.AsNoTracking()
            .Where(r => tpIds.Contains(r.IdTreatmentPlant));

        if (!string.IsNullOrEmpty(request.IdLERCode))
            residuesQuery = residuesQuery.Where(r =>
                r.Residue != null && r.Residue.LerCode != null &&
                r.Residue.LerCode.Code == request.IdLERCode);

        var tpr = await residuesQuery
            .Select(r => new
            {
                r.IdTreatmentPlant,
                r.WeightTotal, r.WeightReused, r.WeightValued, r.WeightRemove,
                ResidueName = r.Residue != null ? r.Residue.Name : null,
                LERCode     = r.Residue != null && r.Residue.LerCode != null ? r.Residue.LerCode.Code : null
            })
            .ToListAsync(ct);

        // ── Residuos de traslado (para transporte) ────────────────────────────
        var wmResidues = await _context.WasteMoveResidues.AsNoTracking()
            .Where(r => moveIds.Contains(r.IdWasteMove))
            .Select(r => new
            {
                r.IdWasteMove, r.VehicleType,
                r.TransportInfo_TransportDistance,
                r.TransportInfo_TransportDuration,
                r.TransportInfo_TransportCarbonEmissions
            })
            .ToListAsync(ct);

        // ── Entidades: nombres SCRAP ──────────────────────────────────────────
        var scrapIds = moves.Where(m => m.IdScrap.HasValue).Select(m => m.IdScrap!.Value).Distinct().ToList();
        var scrapNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        // ── Resumen por SCRAP ─────────────────────────────────────────────────
        var openIncidentIds = treatments.Where(t => t.IncidentId.HasValue).Select(t => t.IncidentId!.Value).ToList();
        var openIncidentSet = await _context.Incidents.AsNoTracking()
            .Where(i => openIncidentIds.Contains(i.Id) && i.ClosedAt == null)
            .Select(i => i.Id).ToHashSetAsync(ct);

        var scrapSummaries = scrapIds.Select(scrapId =>
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
            var openInc = treatments.Count(t => scrapTpIds.Contains(t.Id)
                                             && t.IncidentId.HasValue
                                             && openIncidentSet.Contains(t.IncidentId.Value));
            return new TRScrapComparisonDto(
                scrapId, scrapNames.GetValueOrDefault(scrapId) ?? scrapId.ToString()[..8],
                scrapMoveIds.Count, tw, rr, ir, openInc);
        })
        .OrderByDescending(s => s.RecyclingRate)
        .ToList();

        // ── Balance agregado ──────────────────────────────────────────────────
        var totalW  = tpr.Sum(r => r.WeightTotal  ?? 0m);
        var reused  = tpr.Sum(r => r.WeightReused ?? 0m);
        var valued  = tpr.Sum(r => r.WeightValued ?? 0m);
        var remove  = tpr.Sum(r => r.WeightRemove ?? 0m);
        var rate    = totalW > 0 ? (double)((reused + valued) / totalW * 100m) : 0d;
        var aggregatedBalance = new TreatmentBalanceDto(
            Guid.Empty, "Todos los SCRAPs", reused, valued, remove, totalW, rate);

        // ── Evolución mensual ─────────────────────────────────────────────────
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
                var tw2 = g.Sum(r => r.WeightTotal  ?? 0m);
                var rv2 = g.Sum(r => r.WeightReused ?? 0m);
                var vl2 = g.Sum(r => r.WeightValued ?? 0m);
                var rr2 = tw2 > 0 ? (double)((rv2 + vl2) / tw2 * 100m) : 0d;
                var impr2 = g.Sum(r => tpImprMap.GetValueOrDefault(r.IdTreatmentPlant));
                var ir2   = tw2 > 0 ? (double)(impr2 / tw2 * 100m) : 0d;
                return new MonthlyRecyclingTrendDto(g.Key, rr2, ir2, vl2);
            })
            .OrderBy(t => t.Period)
            .ToList();

        // ── Distribución de operaciones R/D ────────────────────────────────────
        var opsDistribution = treatments
            .Where(t => t.OperationCode != null)
            .GroupBy(t => new { t.OperationCode, t.OperationDesc })
            .Select(g =>
            {
                var ids = g.Select(t => t.Id).ToHashSet();
                var tw2 = tpr.Where(r => ids.Contains(r.IdTreatmentPlant)).Sum(r => r.WeightTotal ?? 0m);
                var opType = g.Key.OperationCode!.StartsWith("R") ? "R" : "D";
                return new TreatmentOperationsDistributionDto(
                    g.Key.OperationCode!, g.Key.OperationDesc ?? g.Key.OperationCode!, opType, tw2);
            })
            .OrderByDescending(o => o.WeightTotal)
            .ToList();

        // ── Dataset de exportación ────────────────────────────────────────────
        var tpMoveMap = treatments.ToDictionary(t => t.Id, t => t.IdWasteMove ?? Guid.Empty);
        var moveWmrMap = wmResidues.GroupBy(r => r.IdWasteMove)
            .ToDictionary(g => g.Key, g => g.First());

        var exportRows = tpr.Select(r =>
        {
            var tpEntry  = treatments.FirstOrDefault(t => t.Id == r.IdTreatmentPlant);
            var moveId   = tpMoveMap.GetValueOrDefault(r.IdTreatmentPlant);
            var scrapId2 = moveScrapMap.GetValueOrDefault(moveId);
            var wmr      = moveWmrMap.GetValueOrDefault(moveId);
            var tw2 = r.WeightTotal ?? 0m;
            var rv2 = r.WeightReused ?? 0m;
            var vl2 = r.WeightValued ?? 0m;
            var rr2 = tw2 > 0 ? (double?)((rv2 + vl2) / tw2 * 100m) : null;
            return new TRExportRowDto(
                tpEntry?.PlantTreatmentDate?.ToString("yyyy-MM-dd"),
                null, null, null,
                scrapId2.HasValue ? scrapNames.GetValueOrDefault(scrapId2.Value) : null,
                null, null,
                r.LERCode, r.ResidueName,
                wmr?.VehicleType,
                (decimal?)wmr?.TransportInfo_TransportDistance,
                (decimal?)wmr?.TransportInfo_TransportDuration,
                (decimal?)wmr?.TransportInfo_TransportCarbonEmissions,
                tpEntry?.OperationCode,
                r.WeightTotal, r.WeightReused, r.WeightValued, r.WeightRemove,
                tpEntry?.ImproperWeight, rr2);
        }).ToList();

        return new TRDispatchDataDto(
            scrapSummaries, aggregatedBalance, monthlyTrend, opsDistribution, exportRows,
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
