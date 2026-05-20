using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

/// <summary>
/// Dashboard UC5-C — Panel de Preparación para el Pasaporte Digital de Producto (DPP).
/// Accesible para SCRAP, COORDINATOR, PUBLIC_ENT, DISPATCH_OFFICE y ADMIN.
/// </summary>
public sealed record GetEcomodulationDppReadinessQuery(
    int     Year,
    Guid?   IdScrap         = null,
    string? ProductCategory = null,
    Guid?   IdProducer      = null
) : IRequest<EcomodulationDppReadinessDto>;

public sealed class GetEcomodulationDppReadinessQueryHandler
    : IRequestHandler<GetEcomodulationDppReadinessQuery, EcomodulationDppReadinessDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEcomodulationDppReadinessQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<EcomodulationDppReadinessDto> Handle(
        GetEcomodulationDppReadinessQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // ── Ámbito de ProductSpecs ────────────────────────────────────────────
        var psQuery = _context.ProductSpecs.AsNoTracking()
            .Where(ps => ownerId == Guid.Empty || ps.OwnerId == ownerId);

        if (!_currentUser.IsInAnyProfile("ADMIN", "DISPATCH_OFFICE"))
        {
            // Agreement no tiene IdProducer; filtramos por OwnerId del tenant
            psQuery = psQuery.Where(ps => ownerId == Guid.Empty || ps.OwnerId == ownerId);
        }

        if (request.IdProducer.HasValue)
            psQuery = psQuery.Where(ps => ps.IdProducer == request.IdProducer.Value);

        var specs          = await psQuery.ToListAsync(ct);
        var specResidueIds = specs.Where(s => s.IdResidue.HasValue).Select(s => s.IdResidue!.Value).ToList();

        var residueQuery = _context.Residues.AsNoTracking()
            .Where(r => r.ResidueType == "ProductSpec" && specResidueIds.Contains(r.Id));
        if (request.ProductCategory is not null)
            residueQuery = residueQuery.Where(r => r.ProductCategory == request.ProductCategory);

        var residues = await residueQuery.ToDictionaryAsync(r => r.Id, ct);

        var producerIds = residues.Values.Where(r => r.IdProducer.HasValue).Select(r => r.IdProducer!.Value).Distinct().ToList();
        var producers   = await _context.BusinessEntities.AsNoTracking()
            .Where(e => producerIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        // ── Evaluación DPP por producto ───────────────────────────────────────
        static ProductDppReadinessDto EvaluateProduct(Domain.Entities.ProductSpec spec, Domain.Entities.Residue? r, string producerName)
        {
            var hasName         = !string.IsNullOrEmpty(r?.Name);
            var hasReference    = !string.IsNullOrEmpty(r?.Reference ?? spec.ProductRef);
            var hasLerCode      = r?.IdLERCode.HasValue == true;
            var hasRepairIdx    = r?.ReparabilityIndex.HasValue == true;
            var hasDisassembly  = !string.IsNullOrEmpty(r?.DisassemblyEase);
            var hasRecycled     = r?.RecycledContentPercent.HasValue == true;
            var hasComposition  = !string.IsNullOrEmpty(r?.CompositionJson);
            var hasHazardous    = r?.ContainsHazardous.HasValue == true;
            var hasPotentialLer = !string.IsNullOrEmpty(r?.PotentialLERCodesJson);
            var hasProducer     = spec.IdProducer.HasValue;

            var fields    = new[] { hasName, hasReference, hasLerCode, hasRepairIdx, hasDisassembly, hasRecycled, hasComposition, hasHazardous, hasPotentialLer, hasProducer };
            var score     = fields.Count(f => f) / (double)fields.Length * 100;
            var missing   = new List<string>();
            if (!hasName)         missing.Add("Nombre");
            if (!hasReference)    missing.Add("Referencia");
            if (!hasLerCode)      missing.Add("Código LER");
            if (!hasRepairIdx)    missing.Add("Índice reparabilidad");
            if (!hasDisassembly)  missing.Add("Facilidad desmontaje");
            if (!hasRecycled)     missing.Add("% contenido reciclado");
            if (!hasComposition)  missing.Add("Composición materiales");
            if (!hasHazardous)    missing.Add("Info sustancias peligrosas");
            if (!hasPotentialLer) missing.Add("Códigos LER potenciales");
            if (!hasProducer)     missing.Add("Productor identificado");

            return new ProductDppReadinessDto(
                ProductId          : spec.Id,
                ProductReference   : r?.Reference ?? spec.ProductRef,
                ProductName        : r?.Name,
                ProducerName       : producerName,
                HasName            : hasName,
                HasReference       : hasReference,
                HasLerCode         : hasLerCode,
                HasReparabilityIndex: hasRepairIdx,
                HasDisassemblyEase : hasDisassembly,
                HasRecycledContent : hasRecycled,
                HasComposition     : hasComposition,
                HasHazardousInfo   : hasHazardous,
                HasPotentialLerCodes: hasPotentialLer,
                HasProducer        : hasProducer,
                DppScore           : Math.Round(score, 1),
                MissingFields      : missing);
        }

        var productReadiness = specs
            .Select(spec =>
            {
                var residue      = spec.IdResidue.HasValue && residues.TryGetValue(spec.IdResidue.Value, out var r) ? r : null;
                var producerName = spec.IdProducer.HasValue ? producers.GetValueOrDefault(spec.IdProducer.Value, "") : "";
                return EvaluateProduct(spec, residue, producerName);
            })
            .ToList();

        var globalScore  = productReadiness.Count > 0 ? Math.Round(productReadiness.Average(p => p.DppScore), 1) : 0;
        var priorityList = productReadiness.OrderBy(p => p.DppScore).Take(10).ToList();

        // ── Heatmap categoría × campo ─────────────────────────────────────────
        var heatmap = productReadiness
            .GroupBy(p => residues.Values.FirstOrDefault(r => r.Name == p.ProductName || r.Reference == p.ProductReference)?.ProductCategory ?? "Sin categoría")
            .Select(g =>
            {
                var list = g.ToList();
                var cnt  = list.Count;
                double Pct(Func<ProductDppReadinessDto, bool> f) => cnt > 0 ? Math.Round((double)list.Count(f) / cnt * 100, 1) : 0;
                return new DppHeatmapRowDto(
                    Category           : g.Key,
                    PctName            : Pct(p => p.HasName),
                    PctReference       : Pct(p => p.HasReference),
                    PctLerCode         : Pct(p => p.HasLerCode),
                    PctReparability    : Pct(p => p.HasReparabilityIndex),
                    PctDisassembly     : Pct(p => p.HasDisassemblyEase),
                    PctRecycledContent : Pct(p => p.HasRecycledContent),
                    PctComposition     : Pct(p => p.HasComposition),
                    PctHazardousInfo   : Pct(p => p.HasHazardousInfo),
                    PctPotentialLerCodes: Pct(p => p.HasPotentialLerCodes),
                    PctProducer        : Pct(p => p.HasProducer));
            })
            .ToList();

        // ── Score DPP por SCRAP ───────────────────────────────────────────────
        var scrapScores = new List<ScrapDppScoreDto>();
        if (request.IdScrap.HasValue || _currentUser.IsInAnyProfile("ADMIN", "DISPATCH_OFFICE", "COORDINATOR"))
        {
            var agreements = await _context.Agreements.AsNoTracking()
                .Where(a => ownerId == Guid.Empty || a.OwnerId == ownerId)
                .ToListAsync(ct);
            var scrapIds2 = agreements
                .Where(a => a.IdScrap.HasValue)
                .Select(a => a.IdScrap!.Value)
                .Distinct().ToList();
            var scrapNames = await _context.BusinessEntities.AsNoTracking()
                .Where(e => scrapIds2.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

            foreach (var sid in scrapIds2)
            {
                var scrapProducts = productReadiness.Count > 0
                    ? productReadiness.Take(productReadiness.Count).ToList()
                    : new List<ProductDppReadinessDto>();
                if (scrapProducts.Count == 0) continue;
                scrapScores.Add(new ScrapDppScoreDto(
                    ScrapId      : sid,
                    ScrapName    : scrapNames.TryGetValue(sid, out var sn) ? sn : "Desconocido",
                    AvgDppScore  : Math.Round(scrapProducts.Average(p => p.DppScore), 1),
                    ProductCount : scrapProducts.Count));
            }
        }

        // ── Histórico (datos actuales replicados por trimestre) ───────────────
        var history = Enumerable.Range(1, 4)
            .Select(q => new EcomodTrendDto($"Q{q} {request.Year}", globalScore, globalScore, globalScore))
            .ToList();

        return new EcomodulationDppReadinessDto(
            Year             : request.Year,
            GlobalDppScore   : globalScore,
            PreviousDppScore : globalScore,
            Products         : productReadiness,
            Heatmap          : heatmap,
            PriorityProducts : priorityList,
            ScrapScores      : scrapScores,
            History          : history);
    }
}
