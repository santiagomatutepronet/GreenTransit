using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Services;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Queries;

/// <summary>
/// Dashboard CN-E — Panel de Datos de Cumplimiento — Oficina de Asignación.
/// Perfiles: DISPATCH_OFFICE, ADMIN.
/// </summary>
public sealed record GetDispatchOfficeComplianceDataQuery(
    int     Year,
    Guid?   IdScrap             = null,
    string? AutonomousCommunity = null,
    string? FlowType            = null,
    string? Category            = null
) : IRequest<DispatchOfficeComplianceDataDto>;

public sealed class GetDispatchOfficeComplianceDataQueryHandler
    : IRequestHandler<GetDispatchOfficeComplianceDataQuery, DispatchOfficeComplianceDataDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public GetDispatchOfficeComplianceDataQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<DispatchOfficeComplianceDataDto> Handle(
        GetDispatchOfficeComplianceDataQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // ── Pesos de entrada del año completo ─────────────────────────────────
        var entryBase = _db.WasteMoves.AsNoTracking()
            .Where(wm => wm.OwnerId == ownerId
                      && wm.ActualPickupStart.HasValue
                      && wm.ActualPickupStart.Value.Year == request.Year);

        var totalEntry = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year
                      && (!request.IdScrap.HasValue || ep.WasteMove.IdScrap == request.IdScrap.Value))
            .SelectMany(ep => ep.EntryPlantResidues)
            .SumAsync(epr => (decimal?)epr.Weight ?? 0m, ct);

        // ── Tratamientos del ecosistema ───────────────────────────────────────
        var treatBase = _db.TreatmentPlants.AsNoTracking()
            .Where(tp => tp.WasteMove!.OwnerId == ownerId
                      && tp.WasteMove.ActualPickupStart.HasValue
                      && tp.WasteMove.ActualPickupStart.Value.Year == request.Year);

        if (request.IdScrap.HasValue)
            treatBase = treatBase.Where(tp => tp.WasteMove!.IdScrap == request.IdScrap.Value);

        var recyclingW    = await treatBase.Where(tp => tp.TreatmentOperation!.IsRecycling)
            .SelectMany(tp => tp.TreatmentPlantResidues).SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);
        var valorizationW = await treatBase.Where(tp => tp.TreatmentOperation!.IsEnergyRecovery)
            .SelectMany(tp => tp.TreatmentPlantResidues).SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);
        var reuseW        = await treatBase.Where(tp => tp.TreatmentOperation!.IsPreparationForReuse)
            .SelectMany(tp => tp.TreatmentPlantResidues).SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

        var ecosystemRecycling    = totalEntry > 0 ? recyclingW    / totalEntry * 100m : 0m;
        var ecosystemValorization = totalEntry > 0 ? valorizationW / totalEntry * 100m : 0m;
        var ecosystemReuse        = totalEntry > 0 ? reuseW        / totalEntry * 100m : 0m;

        // ── KPIs globales ─────────────────────────────────────────────────────
        var activeScraps = await _db.BusinessEntities.AsNoTracking()
            .Where(e => e.EntityRole == "SCRAP")
            .CountAsync(ct);

        var activeAgreements = await _db.Agreements.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.Status == "Active")
            .CountAsync(ct);

        var totalApproved = await _db.Settlements.AsNoTracking()
            .Where(s => s.OwnerId == ownerId && s.Year == request.Year && s.ValidationStatus == "Approved")
            .SumAsync(s => (decimal?)s.TotalAmount ?? 0m, ct);

        // ── Ranking de SCRAPs ─────────────────────────────────────────────────
        var msData = await _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId && ms.Year == request.Year)
            .GroupBy(ms => new { ms.IdScrap, ScrapName = ms.Scrap!.Name })
            .Select(g => new { g.Key.IdScrap, g.Key.ScrapName, TargetKg = g.Sum(ms => ms.Weight) })
            .ToListAsync(ct);

        var realByScrap = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .GroupBy(ep => ep.WasteMove.IdScrap)
            .Select(g => new
            {
                ScrapId = g.Key,
                RealKg  = g.SelectMany(ep => ep.EntryPlantResidues).Sum(epr => epr.Weight ?? 0m)
            })
            .ToDictionaryAsync(x => x.ScrapId, x => x.RealKg, ct);

        var settlementsPerScrap = await _db.Settlements.AsNoTracking()
            .Where(s => s.OwnerId == ownerId && s.Year == request.Year && s.ValidationStatus == "Approved")
            .GroupBy(s => s.IdScrap)
            .Select(g => new { ScrapId = g.Key, Amount = g.Sum(s => s.TotalAmount) })
            .ToDictionaryAsync(x => x.ScrapId, x => x.Amount, ct);

        var agreementsPerScrap = await _db.Agreements.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.Status == "Active")
            .GroupBy(a => a.IdScrap)
            .Select(g => new { ScrapId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ScrapId, x => x.Count, ct);

        var scrapRanking = msData.Select(ms =>
        {
            var real   = realByScrap.GetValueOrDefault(ms.IdScrap, 0m);
            var pct    = ms.TargetKg > 0 ? real / ms.TargetKg * 100m : 0m;
            return new ScrapComplianceRankingDto(
                ms.IdScrap ?? Guid.Empty, ms.ScrapName, pct, ms.TargetKg, real,
                ecosystemRecycling, ecosystemValorization,
                agreementsPerScrap.GetValueOrDefault(ms.IdScrap, 0),
                settlementsPerScrap.GetValueOrDefault(ms.IdScrap, 0m),
                ComplianceMonitoringService.GetStatus(pct, 100m));
        })
        .OrderByDescending(r => r.CompliancePct)
        .ToList();

        // ── Tabla exportable ──────────────────────────────────────────────────
        var exportQuery = _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId && ms.Year == request.Year);

        if (request.IdScrap.HasValue)
            exportQuery = exportQuery.Where(ms => ms.IdScrap == request.IdScrap.Value);
        if (!string.IsNullOrEmpty(request.AutonomousCommunity))
            exportQuery = exportQuery.Where(ms => ms.AutonomousCommunity == request.AutonomousCommunity);
        if (!string.IsNullOrEmpty(request.FlowType))
            exportQuery = exportQuery.Where(ms => ms.FlowType == request.FlowType);
        if (!string.IsNullOrEmpty(request.Category))
            exportQuery = exportQuery.Where(ms => ms.Category == request.Category);

        var exportRaw = await exportQuery
            .Select(ms => new
            {
                ScrapName        = ms.Scrap!.Name,
                ms.Category,
                ms.AutonomousCommunity,
                ms.FlowType,
                ms.Weight,
                ProvinceName     = "",
                MunicipalityName = ""
            })
            .ToListAsync(ct);

        var exportRows = exportRaw.Select(ms =>
        {
            var scrapId = msData.FirstOrDefault(m => m.ScrapName == ms.ScrapName)?.IdScrap ?? Guid.Empty;
            var real    = realByScrap.GetValueOrDefault(scrapId, 0m);
            var pct     = ms.Weight > 0 ? real / ms.Weight * 100m : 0m;
            return new ComplianceExportRowDto(
                ms.ScrapName, ms.Category ?? "", ms.AutonomousCommunity ?? "",
                ms.ProvinceName, ms.MunicipalityName, ms.FlowType ?? "",
                request.Year, "Anual", ms.Weight, real, pct,
                ecosystemRecycling, ecosystemValorization,
                agreementsPerScrap.GetValueOrDefault(scrapId, 0),
                settlementsPerScrap.GetValueOrDefault(scrapId, 0m));
        }).ToList();

        // ── Evolución interanual ──────────────────────────────────────────────
        var targets = await _db.RegulatoryTargets.AsNoTracking()
            .Where(t => t.OwnerId == ownerId)
            .ToListAsync(ct);

        var interannualTrend = new List<InterannualRateTrendDto>();
        for (var y = request.Year - 3; y <= request.Year; y++)
        {
            var yEntry = await _db.EntryPlants.AsNoTracking()
                .Where(ep => ep.WasteMove.OwnerId == ownerId
                          && ep.WasteMove.ActualPickupStart.HasValue
                          && ep.WasteMove.ActualPickupStart.Value.Year == y)
                .SelectMany(ep => ep.EntryPlantResidues)
                .SumAsync(epr => (decimal?)epr.Weight ?? 0m, ct);

            if (yEntry == 0)
            {
                interannualTrend.Add(new InterannualRateTrendDto(y, 0, 0, 0, (decimal)(targets.FirstOrDefault(t => t.Year == y)?.MinRecyclingPercent ?? 65)));
                continue;
            }

            var yRecycling = await _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && tp.WasteMove.ActualPickupStart.HasValue
                          && tp.WasteMove.ActualPickupStart.Value.Year == y
                          && tp.TreatmentOperation!.IsRecycling)
                .SelectMany(tp => tp.TreatmentPlantResidues)
                .SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

            var yValorization = await _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && tp.WasteMove.ActualPickupStart.HasValue
                          && tp.WasteMove.ActualPickupStart.Value.Year == y
                          && tp.TreatmentOperation!.IsEnergyRecovery)
                .SelectMany(tp => tp.TreatmentPlantResidues)
                .SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

            var yReuse = await _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && tp.WasteMove.ActualPickupStart.HasValue
                          && tp.WasteMove.ActualPickupStart.Value.Year == y
                          && tp.TreatmentOperation!.IsPreparationForReuse)
                .SelectMany(tp => tp.TreatmentPlantResidues)
                .SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

            var targetRecycling = (decimal)(targets.FirstOrDefault(t => t.Year == y)?.MinRecyclingPercent ?? 65);
            interannualTrend.Add(new InterannualRateTrendDto(y,
                yRecycling    / yEntry * 100m,
                yValorization / yEntry * 100m,
                yReuse        / yEntry * 100m,
                targetRecycling));
        }

        // ── Heatmap geográfico ────────────────────────────────────────────────
        var geoHeatmapRaw = await _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId && ms.Year == request.Year)
            .Select(ms => new { ms.AutonomousCommunity, ms.IdScrap, ScrapName = ms.Scrap!.Name, ms.Weight })
            .ToListAsync(ct);

        var geoHeatmap = geoHeatmapRaw
            .GroupBy(x => new { x.AutonomousCommunity, x.IdScrap, x.ScrapName })
            .Select(g =>
            {
                var target = g.Sum(x => x.Weight);
                var real   = realByScrap.GetValueOrDefault(g.Key.IdScrap, 0m);
                var pct    = target > 0 ? real / target * 100m : 0m;
                return new GeoComplianceHeatmapDto(
                    g.Key.AutonomousCommunity ?? "",
                    g.Key.IdScrap ?? Guid.Empty,
                    g.Key.ScrapName,
                    pct,
                    ComplianceMonitoringService.GetStatus(pct, 100m));
            })
            .OrderBy(x => x.AutonomousCommunity)
            .ToList();

        // ── Panel normativo (cambios recientes) ───────────────────────────────
        var regulatoryChanges = new List<RegulatoryChangeDto>();

        var recentTargets = await _db.RegulatoryTargets.AsNoTracking()
            .Where(t => t.OwnerId == ownerId)
            .OrderByDescending(t => t.Year)
            .Take(5)
            .ToListAsync(ct);

        regulatoryChanges.AddRange(recentTargets.Select(t => new RegulatoryChangeDto(
            "RegulatoryTarget",
            $"Objetivo reciclaje {t.Year}: {t.MinRecyclingPercent}% — Reutilización: {t.MinReusePercent}%",
            t.Year.ToString(),
            new DateTime(t.Year, 1, 1),
            new DateTime(t.Year, 12, 31))));

        var recentFactors = await _db.EmissionFactorSets.AsNoTracking()
            .Where(efs => efs.OwnerId == ownerId)
            .OrderByDescending(efs => efs.ValidFrom)
            .Take(3)
            .Select(efs => new { efs.FactorSetName, efs.Version, efs.ValidFrom, efs.ValidTo })
            .ToListAsync(ct);

        regulatoryChanges.AddRange(recentFactors.Select(f => new RegulatoryChangeDto(
            "EmissionFactor",
            $"Factores de emisión v{f.Version}: {f.FactorSetName}",
            f.Version ?? "",
            f.ValidFrom,
            f.ValidTo)));

        var recentRuleSets = await _db.EcoModulationRuleSets.AsNoTracking()
            .Where(rs => rs.OwnerId == ownerId)
            .OrderByDescending(rs => rs.ValidFrom)
            .Take(3)
            .Select(rs => new { rs.RuleSetName, rs.Version, rs.ValidFrom, rs.ValidTo })
            .ToListAsync(ct);

        regulatoryChanges.AddRange(recentRuleSets.Select(r => new RegulatoryChangeDto(
            "EcoModulation",
            $"Reglas ecomodulación v{r.Version}: {r.RuleSetName}",
            r.Version ?? "",
            r.ValidFrom,
            r.ValidTo)));

        return new DispatchOfficeComplianceDataDto(
            Year:                       request.Year,
            EcosystemRecyclingPct:      ecosystemRecycling,
            EcosystemValorizationPct:   ecosystemValorization,
            EcosystemReusePct:          ecosystemReuse,
            ActiveScraps:               activeScraps,
            ActiveAgreements:           activeAgreements,
            TotalApprovedAmountYear:    totalApproved,
            VariationRecyclingVsPrevPct: 0d,
            VariationAmountVsPrevPct:   0d,
            ScrapRanking:               scrapRanking,
            ExportRows:                 exportRows,
            InterannualTrend:           interannualTrend,
            GeoHeatmap:                 geoHeatmap,
            RegulatoryChanges:          regulatoryChanges.OrderByDescending(r => r.ValidFrom).ToList());
    }
}
