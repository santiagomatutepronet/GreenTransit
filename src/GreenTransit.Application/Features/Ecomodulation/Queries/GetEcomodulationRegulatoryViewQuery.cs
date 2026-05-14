using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

/// <summary>
/// Dashboard UC5-B — Panel de Monitorización Regulatoria — Autoridad / Certificador.
/// Accesible para PUBLIC_ENT, COORDINATOR y ADMIN.
/// </summary>
public sealed record GetEcomodulationRegulatoryViewQuery(
    int     Year,
    Guid?   IdScrap              = null,
    string? ProductCategory      = null,
    string? ProvinceCode         = null,
    string? AutonomousCommunity  = null
) : IRequest<EcomodulationRegulatoryViewDto>;

public sealed class GetEcomodulationRegulatoryViewQueryHandler
    : IRequestHandler<GetEcomodulationRegulatoryViewQuery, EcomodulationRegulatoryViewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly IConfiguration        _config;

    public GetEcomodulationRegulatoryViewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IConfiguration        config)
    {
        _context     = context;
        _currentUser = currentUser;
        _config      = config;
    }

    public async Task<EcomodulationRegulatoryViewDto> Handle(
        GetEcomodulationRegulatoryViewQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // ── Pesos del índice de circularidad ──────────────────────────────────
        var wRecycled    = double.TryParse(_config["Ecomodulation:CircularityWeights:RecycledContent"],   System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _r) ? _r : 0.30;
        var wRepair      = double.TryParse(_config["Ecomodulation:CircularityWeights:ReparabilityIndex"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _rp) ? _rp : 0.25;
        var wDisassembly = double.TryParse(_config["Ecomodulation:CircularityWeights:DisassemblyEase"],   System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _d) ? _d : 0.20;
        var wNonHazard   = double.TryParse(_config["Ecomodulation:CircularityWeights:NonHazardous"],      System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _n) ? _n : 0.15;
        var wLerCodes    = double.TryParse(_config["Ecomodulation:CircularityWeights:PotentialLerCodes"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _l) ? _l : 0.10;

        // ── Ámbito de SCRAPs accesibles ───────────────────────────────────────
        var scrapIds = new List<Guid>();
        if (!_currentUser.IsInAnyProfile("ADMIN"))
        {
            var linkedId = _currentUser.LinkedEntityId;
            if (_currentUser.IsInAnyProfile("COORDINATOR"))
            {
                scrapIds = await _context.Agreements.AsNoTracking()
                    .Where(a => a.IdCoordinator == linkedId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                    .Select(a => a.IdScrap)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct().ToListAsync(ct);
            }
            else if (_currentUser.IsInAnyProfile("PUBLIC_ENT"))
            {
                scrapIds = await _context.Agreements.AsNoTracking()
                    .Where(a => a.IdPublicEntity == linkedId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                    .Select(a => a.IdScrap)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct().ToListAsync(ct);
            }
        }

        if (request.IdScrap.HasValue)
            scrapIds = scrapIds.Count > 0 ? scrapIds.Where(id => id == request.IdScrap.Value).ToList() : [request.IdScrap.Value];

        // ── ProductSpecs de los SCRAPs ────────────────────────────────────────
        var psQuery = _context.ProductSpecs.AsNoTracking()
            .Where(ps => ownerId == Guid.Empty || ps.OwnerId == ownerId);

        if (scrapIds.Count > 0)
        {
            psQuery = psQuery.Where(ps => ownerId == Guid.Empty || ps.OwnerId == ownerId);
        }

        var specResidueIds = await psQuery.Select(ps => ps.IdResidue).ToListAsync(ct);

        var residueQuery = _context.Residues.AsNoTracking()
            .Where(r => r.ResidueType == "ProductSpec" && specResidueIds.Contains(r.Id));
        if (request.ProductCategory is not null)
            residueQuery = residueQuery.Where(r => r.ProductCategory == request.ProductCategory);

        var residues = await residueQuery.ToListAsync(ct);

        // ── KPIs ejecutivos ───────────────────────────────────────────────────
        var totalSpecs   = residues.Count;
        var totalScraps  = scrapIds.Count > 0 ? scrapIds.Count
            : await _context.Agreements.AsNoTracking()
                .Where(a => ownerId == Guid.Empty || a.OwnerId == ownerId)
                .Select(a => a.IdScrap).Distinct().CountAsync(ct);

        double CalcCircularityIndex(IEnumerable<Domain.Entities.Residue> items)
        {
            var list = items.ToList();
            if (list.Count == 0) return 0;
            var avgRecycled  = (double)list.Average(r => r.RecycledContentPercent ?? 0);
            var avgRepair    = list.Average(r => (double)(r.ReparabilityIndex ?? 0)) / 10.0 * 100.0;
            var avgDisassem  = list.Average(r => r.DisassemblyEase == "Easy" ? 100.0 : r.DisassemblyEase == "Medium" ? 50.0 : 0.0);
            var pctNonHaz    = (double)list.Count(r => r.ContainsHazardous != true) / list.Count * 100;
            var pctLerCodes  = (double)list.Count(r => !string.IsNullOrEmpty(r.PotentialLERCodesJson)) / list.Count * 100;
            return wRecycled * avgRecycled + wRepair * avgRepair + wDisassembly * avgDisassem
                 + wNonHazard * pctNonHaz + wLerCodes * pctLerCodes;
        }

        var ecoIdx          = Math.Round(CalcCircularityIndex(residues), 1);
        var pctImprovement  = totalSpecs > 0
            ? (double)residues.Count(r => (r.ReparabilityIndex ?? 0) < 5 || (r.RecycledContentPercent ?? 0) < 30) / totalSpecs * 100
            : 0;

        // ── Ranking SCRAPs ────────────────────────────────────────────────────
        var allScrapEntities = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id) || (scrapIds.Count == 0 && e.EntityRole == "SCRAP"))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var activeRulesCount = await _context.EcoModulationRuleSets.AsNoTracking()
            .Where(rs => (ownerId == Guid.Empty || rs.OwnerId == ownerId) && rs.Status == "Active")
            .SelectMany(rs => rs.EcoModulationRules)
            .CountAsync(ct);

        var scrapRanking = allScrapEntities.Select(se =>
        {
            var pctCoverage = totalSpecs > 0 ? (double)residues.Count(r => r.ReparabilityIndex.HasValue && r.RecycledContentPercent.HasValue && !string.IsNullOrEmpty(r.CompositionJson)) / totalSpecs * 100 : 0;
            var ciIdx       = CalcCircularityIndex(residues);
            var maturity    = Math.Round((pctCoverage * 0.5 + ciIdx * 0.5), 1);
            return new ScrapMaturityRankDto(
                ScrapId            : se.Key,
                ScrapName          : se.Value,
                ProductCount       : totalSpecs,
                SpecCoveragePct    : Math.Round(pctCoverage, 1),
                AvgCircularityIndex: Math.Round(ciIdx, 1),
                ActiveRules        : activeRulesCount,
                MaturityIndex      : maturity,
                TrafficLight       : maturity >= 70 ? "Green" : maturity >= 40 ? "Orange" : "Red");
        }).OrderByDescending(s => s.MaturityIndex).ToList();

        // ── Comparativa por categoría ─────────────────────────────────────────
        var categoryComparison = residues
            .GroupBy(r => r.ProductCategory ?? "Sin categoría")
            .Select(g =>
            {
                var list = g.ToList();
                var cnt  = list.Count;
                return new CategoryEcodesignDto(
                    Category           : g.Key,
                    AvgReparability    : cnt > 0 ? Math.Round(list.Average(r => (double)(r.ReparabilityIndex ?? 0)), 1) : 0,
                    AvgRecycledContent : cnt > 0 ? Math.Round((double)list.Average(r => r.RecycledContentPercent ?? 0), 1) : 0,
                    PctDisassemblyEasy : cnt > 0 ? Math.Round((double)list.Count(r => r.DisassemblyEase == "Easy") / cnt * 100, 1) : 0,
                    PctNonHazardous    : cnt > 0 ? Math.Round((double)list.Count(r => r.ContainsHazardous != true) / cnt * 100, 1) : 0,
                    PctWithLerCodes    : cnt > 0 ? Math.Round((double)list.Count(r => !string.IsNullOrEmpty(r.PotentialLERCodesJson)) / cnt * 100, 1) : 0);
            })
            .ToList();

        // ── Evolución temporal (trimestral simple con datos actuales) ─────────
        var trends = Enumerable.Range(1, 4).Select(q => new EcomodTrendDto(
            Period            : $"Q{q} {request.Year}",
            CircularityIndex  : ecoIdx,
            SpecCoveragePct   : totalSpecs > 0 ? Math.Round((double)residues.Count(r => r.ReparabilityIndex.HasValue) / totalSpecs * 100, 1) : 0,
            AvgRecycledContent: totalSpecs > 0 ? Math.Round((double)residues.Average(r => r.RecycledContentPercent ?? 0), 1) : 0))
            .ToList();

        // ── Alertas de cumplimiento ───────────────────────────────────────────
        var alerts = new List<EcomodComplianceAlertDto>();
        foreach (var scrap in allScrapEntities)
        {
            var pctCov = totalSpecs > 0 ? (double)residues.Count(r => r.ReparabilityIndex.HasValue) / totalSpecs * 100 : 0;
            if (pctCov < 50)
                alerts.Add(new EcomodComplianceAlertDto("Warning", $"{scrap.Value} tiene {pctCov:F0}% de cobertura de fichas de ecodiseño (umbral: 50%)", scrap.Key, scrap.Value));
        }
        if (ecoIdx < 40)
            alerts.Add(new EcomodComplianceAlertDto("Danger", $"El índice de circularidad del ecosistema ({ecoIdx:F1}) está por debajo del umbral mínimo (40)", null, null));

        var hazardousWithoutCode = residues.Count(r => r.ContainsHazardous == true && string.IsNullOrEmpty(r.DangerousCode));
        if (hazardousWithoutCode > 0)
            alerts.Add(new EcomodComplianceAlertDto("Warning", $"{hazardousWithoutCode} producto(s) marcado(s) como peligrosos sin código de peligrosidad informado", null, null));

        return new EcomodulationRegulatoryViewDto(
            Year                              : request.Year,
            TotalScrapsWithData               : totalScraps,
            TotalProductSpecs                 : totalSpecs,
            EcosystemCircularityIndex         : ecoIdx,
            PctProductsWithImprovementPotential: Math.Round(pctImprovement, 1),
            PreviousCircularityIndex          : ecoIdx,
            ScrapRanking                      : scrapRanking,
            CategoryComparison                : categoryComparison,
            Trends                            : trends,
            ComplianceAlerts                  : alerts);
    }
}
