using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

/// <summary>
/// Dashboard EC-A — Panel SCRAP de Ecomodulación.
/// Analiza el impacto económico de las reglas de ecomodulación en las liquidaciones del SCRAP.
/// Accesible para SCRAP, DISPATCH_OFFICE y ADMIN.
/// </summary>
public sealed record GetScrapEcoModulationPanelQuery(
    int   Year,
    int?  Quarter    = null,
    int?  Month      = null,
    Guid? ProducerId = null,
    Guid? IdScrap    = null
) : IRequest<ScrapEcoModulationPanelDto>;

public sealed class GetScrapEcoModulationPanelQueryHandler
    : IRequestHandler<GetScrapEcoModulationPanelQuery, ScrapEcoModulationPanelDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetScrapEcoModulationPanelQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<ScrapEcoModulationPanelDto> Handle(
        GetScrapEcoModulationPanelQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // ── Ámbito de liquidaciones ───────────────────────────────────────────
        var settlementsQuery = _context.Settlements.AsNoTracking()
            .Where(s => ownerId == Guid.Empty || s.OwnerId == ownerId)
            .Where(s => s.Year == request.Year);

        if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: solo sus propias liquidaciones
            settlementsQuery = settlementsQuery
                .Where(s => s.IdScrap == _currentUser.LinkedEntityId);
        }
        else
        {
            // DISPATCH_OFFICE / ADMIN: sin restricción, filtro opcional de UI
            if (request.IdScrap.HasValue)
                settlementsQuery = settlementsQuery
                    .Where(s => s.IdScrap == request.IdScrap.Value);
        }

        if (request.Month.HasValue)
            settlementsQuery = settlementsQuery.Where(s => s.Month == request.Month.Value);
        else if (request.Quarter.HasValue)
        {
            var firstMonth = (request.Quarter.Value - 1) * 3 + 1;
            var lastMonth  = firstMonth + 2;
            settlementsQuery = settlementsQuery
                .Where(s => s.Month >= firstMonth && s.Month <= lastMonth);
        }

        var settlements = await settlementsQuery
            .Select(s => new
            {
                s.Id,
                s.SettlementNumber,
                s.Year,
                s.Month,
                s.BaseAmount,
                s.AdjustmentsAmount,
                s.TotalAmount,
                s.Status
            })
            .ToListAsync(ct);

        var settlementIds = settlements.Select(s => s.Id).ToList();

        // ── KPIs económicos ───────────────────────────────────────────────────
        var totalAdjustments  = settlements.Sum(s => s.AdjustmentsAmount);
        var totalSettlements  = settlements.Count;

        // Período anterior: mismo filtro de mes/trimestre pero año - 1
        var prevQuery = _context.Settlements.AsNoTracking()
            .Where(s => (ownerId == Guid.Empty || s.OwnerId == ownerId)
                     && s.Year == request.Year - 1);

        if (_currentUser.IsInProfile(ProfileConstants.Scrap))
            prevQuery = prevQuery.Where(s => s.IdScrap == _currentUser.LinkedEntityId);
        else if (request.IdScrap.HasValue)
            prevQuery = prevQuery.Where(s => s.IdScrap == request.IdScrap.Value);

        if (request.Month.HasValue)
            prevQuery = prevQuery.Where(s => s.Month == request.Month.Value);
        else if (request.Quarter.HasValue)
        {
            var fm = (request.Quarter.Value - 1) * 3 + 1;
            prevQuery = prevQuery.Where(s => s.Month >= fm && s.Month <= fm + 2);
        }

        var prevAmount = await prevQuery.SumAsync(s => s.AdjustmentsAmount, ct);
        var variationPct = prevAmount == 0
            ? 0
            : Math.Round((double)((totalAdjustments - prevAmount) / Math.Abs(prevAmount)) * 100, 1);

        // ── Desglose por período ──────────────────────────────────────────────
        var byPeriod = settlements
            .GroupBy(s => s.Month.HasValue
                ? $"{s.Year}/{s.Month.Value:D2}"
                : s.Year.ToString())
            .Select(g => new EcomodSettlementPeriodDto(
                Period            : g.Key,
                AdjustmentsAmount : g.Sum(s => s.AdjustmentsAmount),
                SettlementCount   : g.Count()))
            .OrderBy(p => p.Period)
            .ToList();

        // ── Top productores adheridos ─────────────────────────────────────────
        // Derivar productores de traslados del SCRAP vinculados a ServiceOrders
        var scrapEntityId = _currentUser.IsInProfile(ProfileConstants.Scrap)
            ? _currentUser.LinkedEntityId
            : request.IdScrap;

        List<EcomodProducerImpactDto> topProducers = [];

        if (scrapEntityId.HasValue)
        {
            var producerIds = await _context.ServiceOrders.AsNoTracking()
                .Join(_context.WasteMoves,
                    so => so.Id,
                    wm => wm.ServiceOrderId,
                    (so, wm) => new { so.IdIssuedBy, wm.IdScrap, wm.IdScrap2, wm.OwnerId })
                .Where(x => (ownerId == Guid.Empty || x.OwnerId == ownerId)
                          && (x.IdScrap == scrapEntityId.Value || x.IdScrap2 == scrapEntityId.Value))
                .Select(x => x.IdIssuedBy)
                .Distinct()
                .ToListAsync(ct);

            var producerNames = await _context.BusinessEntities.AsNoTracking()
                .Where(e => producerIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Name })
                .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

            // Impacto por productor: approx. usando SettlementLines de las liquidaciones
            var linesByCategory = await _context.SettlementLines.AsNoTracking()
                .Where(sl => settlementIds.Contains(sl.SettlementId))
                .Select(sl => new { sl.SettlementId, sl.Amount, sl.ProductCategory })
                .ToListAsync(ct);

            topProducers = producerIds
                .Where(pid => pid.HasValue && producerNames.ContainsKey(pid.Value))
                .Select(pid =>
                {
                    var total = linesByCategory.Sum(l => l.Amount) / (producerIds.Count > 0 ? producerIds.Count : 1);
                    return new EcomodProducerImpactDto(
                        ProducerId      : pid!.Value,
                        ProducerName    : producerNames[pid.Value],
                        TotalAdjustment : total,
                        ImpactDirection : total >= 0 ? "Bonificación" : "Penalización");
                })
                .OrderByDescending(p => Math.Abs(p.TotalAdjustment))
                .Take(10)
                .ToList();
        }

        // ── Reglas activas de ecomodulación ───────────────────────────────────
        var activeRules = await _context.EcoModulationRules.AsNoTracking()
            .Join(_context.EcoModulationRuleSets,
                r => r.RuleSetId,
                rs => rs.Id,
                (r, rs) => new { Rule = r, rs.Status, rs.OwnerId })
            .Where(x => x.Status == "Active"
                     && (ownerId == Guid.Empty || x.OwnerId == ownerId))
            .Select(x => x.Rule)
            .ToListAsync(ct);

        var settlementLines = await _context.SettlementLines.AsNoTracking()
            .Where(sl => settlementIds.Contains(sl.SettlementId))
            .ToListAsync(ct);

        var activeRuleDtos = activeRules.Select(rule =>
        {
            var affectedLines = settlementLines
                .Where(sl => rule.ProductCategory.HasValue
                           ? sl.ProductCategory == rule.ProductCategory
                           : true)
                .ToList();
            return new EcomodActiveRuleDto(
                RuleCode                : rule.RuleCode,
                FeeImpactType           : rule.FeeImpactType,
                FeeImpactValue          : rule.FeeImpactValue,
                ProductCategory         : rule.ProductCategory,
                SettlementLinesAffected : affectedLines.Count,
                TotalEconomicImpact     : affectedLines.Sum(l => l.Amount));
        }).ToList();

        // ── Detalle de liquidaciones ──────────────────────────────────────────
        var settlementDetails = settlements
            .Select(s => new EcomodSettlementDetailDto(
                SettlementId      : s.Id,
                SettlementNumber  : s.SettlementNumber,
                Year              : s.Year,
                Month             : s.Month,
                BaseAmount        : s.BaseAmount,
                AdjustmentsAmount : s.AdjustmentsAmount,
                TotalAmount       : s.TotalAmount,
                Status            : s.Status))
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month ?? 0)
            .ToList();

        return new ScrapEcoModulationPanelDto(
            Year                  : request.Year,
            Quarter               : request.Quarter,
            Month                 : request.Month,
            TotalAdjustmentsAmount: totalAdjustments,
            PreviousPeriodAmount  : prevAmount,
            VariationPct          : variationPct,
            TotalSettlements      : totalSettlements,
            ByPeriod              : byPeriod,
            TopProducers          : topProducers,
            ActiveRules           : activeRuleDtos,
            Settlements           : settlementDetails);
    }
}
