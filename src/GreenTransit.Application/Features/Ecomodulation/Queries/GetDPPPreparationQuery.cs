using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

/// <summary>
/// Dashboard EC-C — Preparación DPP (Pasaporte Digital de Producto).
/// PRODUCER: sus propios productos y estado de completitud de fichas.
/// SCRAP: % de productores adheridos con fichas DPP-ready.
/// ADMIN: todos los productos del tenant.
/// </summary>
public sealed record GetDPPPreparationQuery(
    int?    Year              = null,
    string? Category          = null,
    string? ProductReference  = null
) : IRequest<DPPPreparationDto>;

public sealed class GetDPPPreparationQueryHandler
    : IRequestHandler<GetDPPPreparationQuery, DPPPreparationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetDPPPreparationQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<DPPPreparationDto> Handle(
        GetDPPPreparationQuery request, CancellationToken ct)
    {
        var ownerId       = _currentUser.OwnerId;
        var activeProfile = _currentUser.ProfileReference ?? string.Empty;

        // ── Ámbito de residuos ProductSpec según perfil ───────────────────────
        var residuesQuery = _context.Residues.AsNoTracking()
            .Where(r => r.ResidueType == "ProductSpec");

        if (_currentUser.IsInProfile(ProfileConstants.Producer))
        {
            // PRODUCER: solo sus propios productos
            residuesQuery = residuesQuery
                .Where(r => r.IdProducer == _currentUser.LinkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: productos de los productores adheridos (derivados de traslados)
            var scrapId = _currentUser.LinkedEntityId;
            var producerIds = await _context.ServiceOrders.AsNoTracking()
                .Join(_context.WasteMoves,
                    so => so.Id,
                    wm => wm.ServiceOrderId,
                    (so, wm) => new { so.IdIssuedBy, wm.IdScrap, wm.IdScrap2, wm.OwnerId })
                .Where(x => (ownerId == Guid.Empty || x.OwnerId == ownerId)
                          && (x.IdScrap == scrapId || x.IdScrap2 == scrapId))
                .Select(x => x.IdIssuedBy)
                .Distinct()
                .ToListAsync(ct);

            residuesQuery = residuesQuery
                .Where(r => r.IdProducer.HasValue && producerIds.Contains(r.IdProducer));
        }
        // ADMIN: sin restricción adicional, todos los ProductSpec del tenant
        // (ResidueType no tiene OwnerId directo; se asume ámbito global del tenant)

        // Filtros opcionales de UI
        if (!string.IsNullOrEmpty(request.Category))
            residuesQuery = residuesQuery.Where(r => r.ProductCategory == request.Category);
        if (!string.IsNullOrEmpty(request.ProductReference))
            residuesQuery = residuesQuery.Where(r => r.Reference == request.ProductReference
                                                  || r.Name!.Contains(request.ProductReference));

        var residues = await residuesQuery.ToListAsync(ct);

        // ── Fichas técnicas (ProductSpecs) ─────────────────────────────────────
        var residueIds = residues.Select(r => r.Id).ToList();
        var specs = await _context.ProductSpecs.AsNoTracking()
            .Where(ps => ps.IdResidue.HasValue && residueIds.Contains(ps.IdResidue.Value)
                      && (ownerId == Guid.Empty || ps.OwnerId == ownerId))
            .ToListAsync(ct);

        // Mapa residueId → spec
        var specByResidue = specs
            .Where(s => s.IdResidue.HasValue)
            .ToDictionary(s => s.IdResidue!.Value);

        // Mapa residueId → productor
        var producerEntityIds = residues
            .Where(r => r.IdProducer.HasValue)
            .Select(r => r.IdProducer!.Value)
            .Distinct()
            .ToList();

        var producerNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => producerEntityIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        // ── Reglas de ecomodulación activas (catálogo compartido) ─────────────
        var activeRules = await _context.EcoModulationRules.AsNoTracking()
            .Join(_context.EcoModulationRuleSets,
                r => r.RuleSetId, rs => rs.Id,
                (r, rs) => new { Rule = r, rs.Status })
            .Where(x => x.Status == "Active")
            .Select(x => x.Rule)
            .ToListAsync(ct);

        // ── Evaluar cada residuo/ficha ─────────────────────────────────────────
        var productList = new List<DPPProductReadinessDto>();

        foreach (var residue in residues)
        {
            specByResidue.TryGetValue(residue.Id, out var spec);

            var hasName         = !string.IsNullOrEmpty(residue.Name);
            var hasReference    = !string.IsNullOrEmpty(residue.Reference ?? spec?.ProductRef);
            var hasLerCode      = residue.IdLERCode.HasValue;
            var hasReparability = residue.ReparabilityIndex.HasValue;
            var hasDisassembly  = !string.IsNullOrEmpty(residue.DisassemblyEase);
            var hasRecycled     = residue.RecycledContentPercent.HasValue;
            var hasComposition  = !string.IsNullOrEmpty(residue.CompositionJson);
            var hasHazardous    = residue.ContainsHazardous.HasValue;
            var hasPotentialLer = !string.IsNullOrEmpty(residue.PotentialLERCodesJson);
            var hasProducer     = residue.IdProducer.HasValue;

            var fields    = new[] { hasName, hasReference, hasLerCode, hasReparability, hasDisassembly, hasRecycled, hasComposition, hasHazardous, hasPotentialLer, hasProducer };
            var score     = fields.Count(f => f) / (double)fields.Length * 100;
            var isDppReady = score >= 80;

            var missing = new List<string>();
            if (!hasName)         missing.Add("Nombre");
            if (!hasReference)    missing.Add("Referencia");
            if (!hasLerCode)      missing.Add("Código LER");
            if (!hasReparability) missing.Add("Índice reparabilidad");
            if (!hasDisassembly)  missing.Add("Facilidad desmontaje");
            if (!hasRecycled)     missing.Add("% contenido reciclado");
            if (!hasComposition)  missing.Add("Composición materiales");
            if (!hasHazardous)    missing.Add("Info sustancias peligrosas");
            if (!hasPotentialLer) missing.Add("Códigos LER potenciales");
            if (!hasProducer)     missing.Add("Productor identificado");

            // Reglas que aplican según la categoría del producto
            var applicableRules = activeRules
                .Where(r => !r.ProductCategory.HasValue
                         || r.ProductCategory.ToString() == residue.ProductCategory)
                .Select(r => r.RuleCode)
                .ToList();

            productList.Add(new DPPProductReadinessDto(
                ProductSpecId         : spec?.Id ?? Guid.Empty,
                ProductRef            : residue.Reference ?? spec?.ProductRef ?? "—",
                ProductName           : residue.Name,
                ProductCategory       : residue.ProductCategory,
                ProducerName          : residue.IdProducer.HasValue
                                        ? producerNames.GetValueOrDefault(residue.IdProducer.Value)
                                        : null,
                HasName               : hasName,
                HasReference          : hasReference,
                HasLerCode            : hasLerCode,
                HasReparabilityIndex  : hasReparability,
                HasDisassemblyEase    : hasDisassembly,
                HasRecycledContent    : hasRecycled,
                HasComposition        : hasComposition,
                HasHazardousInfo      : hasHazardous,
                HasPotentialLerCodes  : hasPotentialLer,
                HasProducer           : hasProducer,
                DppCompletionPct      : Math.Round(score, 1),
                IsDppReady            : isDppReady,
                MissingFields         : missing,
                ApplicableEcomodRules : applicableRules));
        }

        // ── KPIs ───────────────────────────────────────────────────────────────
        var total        = productList.Count;
        var fullSpec     = productList.Count(p => p.MissingFields.Count == 0);
        var dppReady     = productList.Count(p => p.IsDppReady);
        var pctFull      = total > 0 ? Math.Round((double)fullSpec  / total * 100, 1) : 0;
        var pctDppReady  = total > 0 ? Math.Round((double)dppReady  / total * 100, 1) : 0;

        // ── Adherencia por productor (vista SCRAP / ADMIN) ─────────────────────
        var producerAdherence = new List<DPPProducerAdherenceSummaryDto>();
        if (_currentUser.IsInProfile(ProfileConstants.Scrap)
            || _currentUser.IsInProfile(ProfileConstants.Admin))
        {
            producerAdherence = productList
                .Where(p => !string.IsNullOrEmpty(p.ProducerName))
                .GroupBy(p => p.ProducerName!)
                .Select(g =>
                {
                    var producerTotal   = g.Count();
                    var producerReady   = g.Count(p => p.IsDppReady);
                    var producerReadyPct = producerTotal > 0
                        ? Math.Round((double)producerReady / producerTotal * 100, 1)
                        : 0;
                    var producerId = residues
                        .FirstOrDefault(r => r.IdProducer.HasValue
                            && producerNames.GetValueOrDefault(r.IdProducer.Value) == g.Key)
                        ?.IdProducer ?? Guid.Empty;
                    return new DPPProducerAdherenceSummaryDto(
                        ProducerId      : producerId,
                        ProducerName    : g.Key,
                        TotalProducts   : producerTotal,
                        DppReadyProducts: producerReady,
                        DppReadyPct     : producerReadyPct,
                        TrafficLight    : producerReadyPct >= 80 ? "Green"
                                        : producerReadyPct >= 50 ? "Orange" : "Red");
                })
                .OrderBy(p => p.DppReadyPct)
                .ToList();
        }

        // ── Reglas aplicables (resumen) ────────────────────────────────────────
        var applicableRulesSummary = activeRules.Select(rule =>
        {
            var affected = productList.Count(p =>
                p.ApplicableEcomodRules.Contains(rule.RuleCode));
            return new DPPEcomodRuleApplicabilityDto(
                RuleCode        : rule.RuleCode,
                FeeImpactType   : rule.FeeImpactType,
                FeeImpactValue  : rule.FeeImpactValue,
                ProductCategory : rule.ProductCategory,
                ProductsAffected: affected);
        }).ToList();

        return new DPPPreparationDto(
            TotalProducts        : total,
            ProductsWithFullSpec : fullSpec,
            ProductsDppReady     : dppReady,
            PctFullSpec          : pctFull,
            PctDppReady          : pctDppReady,
            ActiveProfile        : activeProfile,
            Products             : productList,
            ProducerAdherence    : producerAdherence,
            ApplicableRules      : applicableRulesSummary);
    }
}
