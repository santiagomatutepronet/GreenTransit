using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.TratamientoReciclaje.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.TratamientoReciclaje.Queries;

/// <summary>
/// Dashboard TR-C — Validación y Datos Multi-SCRAP (Coordinador).
/// Accesible para COORDINATOR y ADMIN.
/// </summary>
public sealed record GetTRCoordinatorValidationQuery(
    int     Year,
    int?    Month            = null,
    Guid?   IdScrap          = null,
    string? ProvinceCode     = null,
    string? MunicipalityCode = null,
    string? IdLERCode        = null,
    string? TreatmentOperationCode = null
) : IRequest<TRCoordinatorValidationDto>;

public sealed class GetTRCoordinatorValidationQueryHandler
    : IRequestHandler<GetTRCoordinatorValidationQuery, TRCoordinatorValidationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetTRCoordinatorValidationQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<TRCoordinatorValidationDto> Handle(
        GetTRCoordinatorValidationQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        var (dateFrom, dateTo) = BuildRange(request.Year, request.Month);

        // ── Determinar SCRAPs vinculados al coordinador ────────────────────────
        IQueryable<Guid> scrapIdsQuery;
        if (_currentUser.IsInAnyProfile("ADMIN", "DISPATCH_OFFICE"))
        {
            scrapIdsQuery = _context.WasteMoves.AsNoTracking()
                .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId) && wm.IdScrap.HasValue)
                .Select(wm => wm.IdScrap!.Value)
                .Distinct();
        }
        else
        {
            var linkedId = _currentUser.LinkedEntityId;
            var agreementScrapIds = _context.Agreements.AsNoTracking()
                .Where(a => a.IdCoordinator == linkedId && a.IdScrap.HasValue)
                .Select(a => a.IdScrap!.Value);
            scrapIdsQuery = _context.WasteMoves.AsNoTracking()
                .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId) && wm.IdScrap.HasValue)
                .Select(wm => wm.IdScrap!.Value)
                .Where(id => agreementScrapIds.Contains(id))
                .Distinct();
        }

        var scrapIds = await scrapIdsQuery.ToListAsync(ct);
        if (request.IdScrap.HasValue)
            scrapIds = scrapIds.Where(id => id == request.IdScrap.Value).ToList();

        var scrapNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        // ── Traslados del periodo ─────────────────────────────────────────────
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                      && wm.IdScrap.HasValue
                      && scrapIds.Contains(wm.IdScrap!.Value));

        var moves = await wmQuery
            .Select(wm => new { wm.Id, wm.IdScrap, wm.WasteMoveReference })
            .ToListAsync(ct);
        var moveIds = moves.Select(m => m.Id).ToList();
        var moveScrapMap = moves.ToDictionary(m => m.Id, m => m.IdScrap!.Value);

        // ── TreatmentPlants del periodo ───────────────────────────────────────
        var tpQuery = _context.TreatmentPlants.AsNoTracking()
            .Where(tp => moveIds.Contains(tp.IdWasteMove ?? Guid.Empty)
                      && tp.PlantTreatmentDate >= dateFrom
                      && tp.PlantTreatmentDate <  dateTo);

        if (!string.IsNullOrEmpty(request.TreatmentOperationCode))
            tpQuery = tpQuery.Where(tp =>
                tp.TreatmentOperation != null &&
                tp.TreatmentOperation.Code == request.TreatmentOperationCode);

        var treatments = await tpQuery
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

        // ── Evolución mensual por SCRAP (agregado) ────────────────────────────
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

        // ── Distribución de operaciones R/D ────────────────────────────────────
        var opsDistribution = treatments
            .Where(t => t.OperationCode != null)
            .GroupBy(t => new { t.OperationCode, t.OperationDesc })
            .Select(g =>
            {
                var ids = g.Select(t => t.Id).ToHashSet();
                var tw  = tpr.Where(r => ids.Contains(r.IdTreatmentPlant)).Sum(r => r.WeightTotal ?? 0m);
                var opType = g.Key.OperationCode!.StartsWith("R") ? "R" : "D";
                return new TreatmentOperationsDistributionDto(
                    g.Key.OperationCode!, g.Key.OperationDesc ?? g.Key.OperationCode!, opType, tw);
            })
            .OrderByDescending(o => o.WeightTotal)
            .ToList();

        // ── Dataset de exportación ────────────────────────────────────────────
        var moveRefMap = moves.ToDictionary(m => m.Id, m => m.WasteMoveReference);
        var tpMoveMap  = treatments.ToDictionary(t => t.Id, t => t.IdWasteMove ?? Guid.Empty);

        var exportRows = tpr.Select(r =>
        {
            var tpEntry   = treatments.FirstOrDefault(t => t.Id == r.IdTreatmentPlant);
            var moveId    = tpMoveMap.GetValueOrDefault(r.IdTreatmentPlant);
            var scrapId2  = moveScrapMap.GetValueOrDefault(moveId);
            var tw        = r.WeightTotal ?? 0m;
            var rv        = r.WeightReused ?? 0m;
            var vl        = r.WeightValued ?? 0m;
            var rr        = tw > 0 ? (double?)((rv + vl) / tw * 100m) : null;
            return new TRExportRowDto(
                tpEntry?.PlantTreatmentDate?.ToString("yyyy-MM-dd"),
                null, null, null,
                scrapNames.GetValueOrDefault(scrapId2),
                null, null,
                r.LERCode, r.ResidueName,
                null, null, null, null,
                tpEntry?.OperationCode,
                r.WeightTotal, r.WeightReused, r.WeightValued, r.WeightRemove,
                tpEntry?.ImproperWeight,
                rr);
        }).ToList();

        return new TRCoordinatorValidationDto(
            scrapSummaries, monthlyTrend, opsDistribution, exportRows,
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
