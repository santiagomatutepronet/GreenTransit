using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using GreenTransit.Application.Features.Ecomodulation.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

/// <summary>
/// Dashboard UC5-A — Panel de Datos de Ecomodulación — SCRAP (Proveedor del dato).
/// Accesible para SCRAP, DISPATCH_OFFICE y ADMIN.
/// </summary>
public sealed record GetEcomodulationScrapOverviewQuery(
    int     Year,
    string? ProductCategory        = null,
    Guid?   IdProducer             = null,
    Guid?   EcoModulationRuleSetId = null
) : IRequest<EcomodulationScrapOverviewDto>;

public sealed class GetEcomodulationScrapOverviewQueryHandler
    : IRequestHandler<GetEcomodulationScrapOverviewQuery, EcomodulationScrapOverviewDto>
{
    private readonly IApplicationDbContext             _context;
    private readonly ICurrentUserService               _currentUser;
    private readonly IConfiguration                    _config;
    private readonly EcomodulationRecommendationEngine _engine;

    public GetEcomodulationScrapOverviewQueryHandler(
        IApplicationDbContext             context,
        ICurrentUserService               currentUser,
        IConfiguration                    config,
        EcomodulationRecommendationEngine  engine)
    {
        _context     = context;
        _currentUser = currentUser;
        _config      = config;
        _engine      = engine;
    }

    public async Task<EcomodulationScrapOverviewDto> Handle(
        GetEcomodulationScrapOverviewQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var wRecycled    = double.TryParse(_config["Ecomodulation:CircularityWeights:RecycledContent"],   System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _r) ? _r : 0.30;
        var wRepair      = double.TryParse(_config["Ecomodulation:CircularityWeights:ReparabilityIndex"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _rp) ? _rp : 0.25;
        var wDisassembly = double.TryParse(_config["Ecomodulation:CircularityWeights:DisassemblyEase"],   System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _d) ? _d : 0.20;
        var wNonHazard   = double.TryParse(_config["Ecomodulation:CircularityWeights:NonHazardous"],      System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _n) ? _n : 0.15;
        var wLerCodes    = double.TryParse(_config["Ecomodulation:CircularityWeights:PotentialLerCodes"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _l) ? _l : 0.10;

        // ── Ámbito de ProductSpecs ────────────────────────────────────────────
        var psQuery = _context.ProductSpecs.AsNoTracking()
            .Where(ps => ownerId == Guid.Empty || ps.OwnerId == ownerId);

        if (!_currentUser.IsInAnyProfile("ADMIN", "DISPATCH_OFFICE"))
        {
            var linkedId    = _currentUser.LinkedEntityId;
            var agreementScrapIds = await _context.Agreements.AsNoTracking()
                .Where(a => a.IdScrap == linkedId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.Id)
                .ToListAsync(ct);
            // Filtrar ProductSpecs de productores cuyos productos tienen traslados del SCRAP vinculado
            // Como Agreement no tiene IdProducer directo, filtramos por el OwnerId del SCRAP
            psQuery = psQuery.Where(ps => ps.OwnerId == ownerId || agreementScrapIds.Count > 0);
        }
        else if (request.IdProducer.HasValue)
        {
            psQuery = psQuery.Where(ps => ps.IdProducer == request.IdProducer.Value);
        }

        var specResidueIds = await psQuery.Select(ps => ps.IdResidue).ToListAsync(ct);

        var residueQuery = _context.Residues.AsNoTracking()
            .Where(r => r.ResidueType == "ProductSpec" && specResidueIds.Contains(r.Id));

        if (request.ProductCategory is not null)
            residueQuery = residueQuery.Where(r => r.ProductCategory == request.ProductCategory);

        var residues = await residueQuery.ToListAsync(ct);

        // ── KPIs de cobertura ─────────────────────────────────────────────────
        var total       = residues.Count;
        var fullSpec    = residues.Count(r => r.ReparabilityIndex.HasValue
                                           && r.RecycledContentPercent.HasValue
                                           && !string.IsNullOrEmpty(r.CompositionJson));
        var partialSpec = residues.Count(r => (r.ReparabilityIndex.HasValue
                                           || r.RecycledContentPercent.HasValue
                                           || !string.IsNullOrEmpty(r.CompositionJson))
                                           && !(r.ReparabilityIndex.HasValue
                                             && r.RecycledContentPercent.HasValue
                                             && !string.IsNullOrEmpty(r.CompositionJson)));
        var noSpec  = total - fullSpec - partialSpec;
        var withLer = residues.Count(r => r.IdLERCode.HasValue);

        // ── Índice de circularidad por categoría ──────────────────────────────
        var byCategory = residues
            .GroupBy(r => r.ProductCategory ?? "Sin categoría")
            .Select(g =>
            {
                var items       = g.ToList();
                var count       = items.Count;
                var avgRecycled = count > 0 ? (double)items.Average(r => r.RecycledContentPercent ?? 0) : 0;
                var avgRepair   = count > 0 ? items.Average(r => (double)(r.ReparabilityIndex ?? 0)) / 10.0 * 100.0 : 0;
                var avgDis      = count > 0 ? items.Average(r => r.DisassemblyEase == "Easy" ? 100.0 : r.DisassemblyEase == "Medium" ? 50.0 : 0.0) : 0;
                var pctNonHaz   = count > 0 ? (double)items.Count(r => r.ContainsHazardous != true) / count * 100 : 0;
                var pctLer      = count > 0 ? (double)items.Count(r => !string.IsNullOrEmpty(r.PotentialLERCodesJson)) / count * 100 : 0;
                var idx         = wRecycled * avgRecycled + wRepair * avgRepair
                                + wDisassembly * avgDis + wNonHazard * pctNonHaz + wLerCodes * pctLer;
                return new CircularityByCategory(
                    Category              : g.Key,
                    CircularityIndex      : Math.Round(idx, 1),
                    AvgRecycledContentPct : Math.Round(avgRecycled, 1),
                    AvgReparabilityIndex  : Math.Round(avgRepair, 1),
                    AvgDisassemblyEase    : Math.Round(avgDis, 1),
                    PctNonHazardous       : Math.Round(pctNonHaz, 1),
                    PctWithLerCodes       : Math.Round(pctLer, 1),
                    TrafficLight          : idx >= 70 ? "Green" : idx >= 40 ? "Orange" : "Red");
            })
            .OrderByDescending(c => c.CircularityIndex)
            .ToList();

        // ── Impacto de reglas ─────────────────────────────────────────────────
        var rsQuery = _context.EcoModulationRuleSets.AsNoTracking()
            .Where(rs => ownerId == Guid.Empty || rs.OwnerId == ownerId);
        if (request.EcoModulationRuleSetId.HasValue)
            rsQuery = rsQuery.Where(rs => rs.Id == request.EcoModulationRuleSetId.Value);

        var rules = await _context.EcoModulationRules.AsNoTracking()
            .Join(rsQuery, r => r.RuleSetId, rs => rs.Id, (r, _) => r)
            .ToListAsync(ct);

        var ruleImpacts = new List<EcomodRuleImpactDto>();
        foreach (var rule in rules)
        {
            var lines = await _context.SettlementLines.AsNoTracking()
                .Where(sl => sl.ProductCategory == rule.ProductCategory)
                .ToListAsync(ct);
            ruleImpacts.Add(new EcomodRuleImpactDto(
                RuleName                : rule.RuleCode,
                ImpactType              : rule.FeeImpactType,
                ProductsAffected        : lines.Count,
                TotalEconomicAdjustment : lines.Sum(l => l.Amount),
                PreviousPeriodAdjustment: 0m,
                VariationPct            : 0,
                MonthlySparkline        : Enumerable.Repeat(0m, 12).ToList()));
        }

        // ── Cobertura por productor ───────────────────────────────────────────
        var producerIdList = residues.Where(r => r.IdProducer.HasValue)
            .Select(r => r.IdProducer!.Value).Distinct().ToList();
        var producers = await _context.BusinessEntities.AsNoTracking()
            .Where(e => producerIdList.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var producerCoverage = residues
            .GroupBy(r => r.IdProducer ?? Guid.Empty)
            .Select(g =>
            {
                var list    = g.ToList();
                var cnt     = list.Count;
                var full    = list.Count(r => r.ReparabilityIndex.HasValue && r.RecycledContentPercent.HasValue && !string.IsNullOrEmpty(r.CompositionJson));
                var partial = list.Count(r => (r.ReparabilityIndex.HasValue || r.RecycledContentPercent.HasValue || !string.IsNullOrEmpty(r.CompositionJson))
                                           && !(r.ReparabilityIndex.HasValue && r.RecycledContentPercent.HasValue && !string.IsNullOrEmpty(r.CompositionJson)));
                var pct     = cnt > 0 ? (double)full / cnt * 100 : 0;
                return new ProducerSpecCoverageDto(
                    ProducerId   : g.Key,
                    ProducerName : producers.GetValueOrDefault(g.Key, "Desconocido"),
                    TotalProducts: cnt,
                    FullSpec     : full,
                    PartialSpec  : partial,
                    NoSpec       : cnt - full - partial,
                    CoveragePct  : Math.Round(pct, 1),
                    TrafficLight : pct >= 80 ? "Green" : pct >= 50 ? "Orange" : "Red");
            })
            .OrderBy(p => p.CoveragePct)
            .ToList();

        // ── Composición de materiales ─────────────────────────────────────────
        var materialMap = new Dictionary<string, (double TotalPct, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in residues.Where(r => !string.IsNullOrEmpty(r.CompositionJson)))
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, double>>(r.CompositionJson!,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dict is null) continue;
                foreach (var (mat, pct) in dict)
                {
                    if (!materialMap.TryGetValue(mat, out var cur))
                        materialMap[mat] = (pct, 1);
                    else
                        materialMap[mat] = (cur.TotalPct + pct, cur.Count + 1);
                }
            }
            catch { /* JSON inválido — ignorar */ }
        }
        var materialComposition = materialMap
            .Select(kv => new MaterialCompositionDto(kv.Key, Math.Round(kv.Value.TotalPct, 1), kv.Value.Count))
            .OrderByDescending(m => m.TotalPercentage)
            .ToList();

        // ── Dataset exportable ────────────────────────────────────────────────
        var lerIds = residues.Where(r => r.IdLERCode.HasValue).Select(r => r.IdLERCode!.Value).Distinct().ToList();
        var lerMap = await _context.LerCodes.AsNoTracking()
            .Where(l => lerIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);

        var exportRows = residues
            .Select(r => new EcomodulationExportRowDto(
                ProductReference       : r.Reference ?? r.Name,
                ProducerName           : r.IdProducer.HasValue ? producers.GetValueOrDefault(r.IdProducer.Value, "") : "",
                Category               : r.ProductCategory ?? "",
                LerCode                : r.IdLERCode.HasValue ? lerMap.GetValueOrDefault(r.IdLERCode.Value) : null,
                ReparabilityIndex      : (double?)r.ReparabilityIndex,
                RecycledContentPercent : (double?)r.RecycledContentPercent,
                DisassemblyEase        : r.DisassemblyEase,
                ContainsHazardous      : r.ContainsHazardous == true,
                Composition            : r.CompositionJson,
                EcomodRuleName         : null,
                EconomicAdjustment     : null))
            .ToList();

        return new EcomodulationScrapOverviewDto(
            Year                    : request.Year,
            TotalProducts           : total,
            ProductsWithFullSpec    : fullSpec,
            ProductsWithPartialSpec : partialSpec,
            ProductsWithNoSpec      : noSpec,
            PctWithFullSpec         : total > 0 ? Math.Round((double)fullSpec / total * 100, 1) : 0,
            PctWithLerCode          : total > 0 ? Math.Round((double)withLer / total * 100, 1) : 0,
            CircularityByCategory   : byCategory,
            RuleImpacts             : ruleImpacts,
            ProducerCoverage        : producerCoverage,
            MaterialComposition     : materialComposition,
            ExportRows              : exportRows,
            Recommendations         : _engine.GenerateFromResidues(residues));
    }
}
