using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Queries;

/// <summary>
/// Dashboard CN-B — Panel de Auditoría de Cuotas de Mercado — Reparto entre SCRAPs.
/// Perfiles: COORDINATOR, DISPATCH_OFFICE, ADMIN.
/// </summary>
public sealed record GetMarketShareAuditQuery(
    int     Year,
    string? AutonomousCommunity = null,
    string? FlowType            = null,
    string? Category            = null,
    Guid?   IdScrap             = null
) : IRequest<MarketShareAuditDto>;

public sealed class GetMarketShareAuditQueryHandler
    : IRequestHandler<GetMarketShareAuditQuery, MarketShareAuditDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public GetMarketShareAuditQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<MarketShareAuditDto> Handle(
        GetMarketShareAuditQuery request, CancellationToken ct)
    {
        var ownerId      = _currentUser.OwnerId;
        var isAdmin      = _currentUser.IsInProfile(ProfileConstants.Admin);
        var isDispatch   = _currentUser.IsInProfile(ProfileConstants.DispatchOffice);
        var isCoordinator = _currentUser.IsInProfile(ProfileConstants.Coordinator);

        // SCRAPs visibles según perfil
        List<Guid> visibleScrapIds = [];
        if (isCoordinator && !isAdmin && !isDispatch)
        {
            visibleScrapIds = await _db.Agreements.AsNoTracking()
                .Where(a => a.IdCoordinator == _currentUser.LinkedEntityId && a.OwnerId == ownerId && a.IdScrap.HasValue)
                .Select(a => a.IdScrap!.Value)
                .Distinct()
                .ToListAsync(ct);
        }

        // ── Cuotas de mercado ─────────────────────────────────────────────────
        var msQuery = _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId && ms.Year == request.Year);

        if (visibleScrapIds.Count > 0)
            msQuery = msQuery.Where(ms => ms.IdScrap.HasValue && visibleScrapIds.Contains(ms.IdScrap!.Value));
        if (request.IdScrap.HasValue)
            msQuery = msQuery.Where(ms => ms.IdScrap == request.IdScrap.Value);
        if (!string.IsNullOrEmpty(request.AutonomousCommunity))
            msQuery = msQuery.Where(ms => ms.AutonomousCommunity == request.AutonomousCommunity);
        if (!string.IsNullOrEmpty(request.FlowType))
            msQuery = msQuery.Where(ms => ms.FlowType == request.FlowType);
        if (!string.IsNullOrEmpty(request.Category))
            msQuery = msQuery.Where(ms => ms.Category == request.Category);

        var marketShares = await msQuery
            .Select(ms => new
            {
                ms.Id, ms.IdScrap, ms.Category, ms.AutonomousCommunity,
                ms.FlowType, ms.Weight,
                ScrapName = ms.Scrap!.Name
            })
            .ToListAsync(ct);

        var scrapIds = marketShares.Select(ms => ms.IdScrap).Distinct().ToList();

        // ── Pesos reales por SCRAP y categoría ────────────────────────────────
        var realWeights = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && ep.WasteMove.IdScrap.HasValue && scrapIds.Contains(ep.WasteMove.IdScrap)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .SelectMany(ep => ep.EntryPlantResidues)
            .GroupBy(epr => new { epr.EntryPlant.WasteMove.IdScrap, Category = epr.Residue.ProductCategory ?? "" })
            .Select(g => new { g.Key.IdScrap, g.Key.Category, WeightKg = g.Sum(r => r.Weight) })
            .ToListAsync(ct);

        var realByScrapCategory = realWeights
            .ToDictionary(x => (x.IdScrap, x.Category), x => x.WeightKg);

        // ── Donut y tabla resumen por SCRAP ───────────────────────────────────
        var scrapGroups = marketShares
            .GroupBy(ms => new { ms.IdScrap, ms.ScrapName })
            .ToList();

        var totalTargetAll = marketShares.Sum(ms => ms.Weight);

        var scrapShares = scrapGroups.Select(g =>
        {
            var target = g.Sum(ms => ms.Weight);
            return new ScrapShareSliceDto(g.Key.IdScrap ?? Guid.Empty, g.Key.ScrapName, target,
                totalTargetAll > 0 ? (double)(target / totalTargetAll * 100m) : 0d);
        }).ToList();

        var scrapSummaries = scrapGroups.Select(g =>
        {
            var target = g.Sum(ms => ms.Weight);
            var real   = g.Sum(ms => realByScrapCategory.GetValueOrDefault((ms.IdScrap, ms.Category ?? ""), 0m) ?? 0m);
            var pct    = target > 0 ? real / target * 100m : 0m;
            var dev    = real - target;
            return new ScrapShareSummaryDto(g.Key.IdScrap ?? Guid.Empty, g.Key.ScrapName, target, real, pct, dev,
                target > 0 ? dev / target * 100m : 0m);
        }).ToList();

        // ── Heatmap SCRAP × Categoría ─────────────────────────────────────────
        var categories = marketShares.Select(ms => ms.Category ?? "").Distinct().OrderBy(c => c).ToList();
        var heatmap = scrapGroups.Select(g =>
        {
            var cells = categories.Select(cat =>
            {
                var t = g.Where(ms => ms.Category == cat).Sum(ms => ms.Weight);
                var r = realByScrapCategory.GetValueOrDefault((g.Key.IdScrap, cat), 0m) ?? 0m;
                var p = t > 0 ? r / t * 100m : 0m;
                return new CategoryComplianceCellDto(cat, p,
                    t == 0 ? "GREEN" : Services.ComplianceMonitoringService.GetStatus(p, 100m));
            }).ToList();
            return new ScrapCategoryHeatmapRowDto(g.Key.IdScrap ?? Guid.Empty, g.Key.ScrapName, cells);
        }).ToList();

        // ── Evolución mensual por SCRAP ───────────────────────────────────────
        var monthlyRaw = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && ep.WasteMove.IdScrap.HasValue && scrapIds.Contains(ep.WasteMove.IdScrap)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .SelectMany(ep => ep.EntryPlantResidues
                .Select(epr => new { ep.WasteMove.IdScrap, Month = ep.WasteMove.ActualPickupStart!.Value.Month, epr.Weight }))
            .GroupBy(x => new { x.IdScrap, x.Month })
            .Select(g => new { g.Key.IdScrap, g.Key.Month, WeightKg = g.Sum(x => x.Weight) })
            .ToListAsync(ct);

        var scrapNameMap = scrapGroups.ToDictionary(g => g.Key.IdScrap, g => g.Key.ScrapName);
        var monthlyEvolution = monthlyRaw.Select(x =>
        {
            var targetMonthly = marketShares
                .Where(ms => ms.IdScrap == x.IdScrap)
                .Sum(ms => ms.Weight) / 12m;
            return new MonthlyScrapWeightDto(request.Year, x.Month, x.IdScrap ?? Guid.Empty,
                scrapNameMap.GetValueOrDefault(x.IdScrap, ""), x.WeightKg ?? 0m, targetMonthly);
        }).OrderBy(x => x.Month).ThenBy(x => x.ScrapName).ToList();

        // ── Desglose territorial ──────────────────────────────────────────────
        var territorial = marketShares.Select(ms =>
        {
            var real = realByScrapCategory.GetValueOrDefault((ms.IdScrap, ms.Category ?? ""), 0m) ?? 0m;
            var pct  = ms.Weight > 0 ? real / ms.Weight * 100m : 0m;
            return new TerritorialBreakdownDto(
                ms.AutonomousCommunity ?? "",
                ms.IdScrap ?? Guid.Empty,
                ms.ScrapName,
                ms.Category ?? "",
                ms.Weight,
                real,
                pct,
                Services.ComplianceMonitoringService.GetStatus(pct, 100m));
        }).ToList();

        // ── Índice de desviación ──────────────────────────────────────────────
        var deviationIndex = scrapSummaries
            .Select(s => new DeviationIndexDto(s.ScrapId, s.ScrapName, s.DeviationPct,
                s.DeviationPct >= 0 ? "GREEN" : "RED"))
            .OrderByDescending(d => d.DeviationPct)
            .ToList();

        return new MarketShareAuditDto(
            Year:                 request.Year,
            ScrapShares:          scrapShares,
            ScrapSummaries:       scrapSummaries,
            Heatmap:              heatmap,
            MonthlyEvolution:     monthlyEvolution,
            TerritorialBreakdown: territorial,
            DeviationIndex:       deviationIndex);
    }
}
