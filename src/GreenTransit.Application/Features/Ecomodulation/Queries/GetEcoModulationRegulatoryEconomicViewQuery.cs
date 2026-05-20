using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

/// <summary>
/// Dashboard EC-B — Vista Regulatoria de Ecomodulación (supervisión económica).
/// Permite a COORDINATOR, PUBLIC_ENT, DISPATCH_OFFICE y ADMIN supervisar
/// cómo las reglas de ecomodulación afectan económicamente al ecosistema.
/// </summary>
public sealed record GetEcoModulationRegulatoryEconomicViewQuery(
    int    Year,
    Guid?  IdScrap              = null,
    string? AutonomousCommunity = null,
    string? Category            = null
) : IRequest<EcoModulationRegulatoryEconomicViewDto>;

public sealed class GetEcoModulationRegulatoryEconomicViewQueryHandler
    : IRequestHandler<GetEcoModulationRegulatoryEconomicViewQuery, EcoModulationRegulatoryEconomicViewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEcoModulationRegulatoryEconomicViewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<EcoModulationRegulatoryEconomicViewDto> Handle(
        GetEcoModulationRegulatoryEconomicViewQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // ── Ámbito de liquidaciones según perfil ──────────────────────────────
        var settlementsQuery = _context.Settlements.AsNoTracking()
            .Where(s => (ownerId == Guid.Empty || s.OwnerId == ownerId)
                     && s.Year == request.Year);

        if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: liquidaciones de los SCRAPs de sus acuerdos
            var linkedId = _currentUser.LinkedEntityId;
            var scrapIds = await _context.Agreements.AsNoTracking()
                .Where(a => a.IdCoordinator == linkedId
                         && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.IdScrap)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToListAsync(ct);

            settlementsQuery = settlementsQuery
                .Where(s => s.IdScrap.HasValue && scrapIds.Contains(s.IdScrap.Value));

            if (request.IdScrap.HasValue && scrapIds.Contains(request.IdScrap.Value))
                settlementsQuery = settlementsQuery
                    .Where(s => s.IdScrap == request.IdScrap.Value);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            // PUBLIC_ENT: sus propias liquidaciones de convenio
            settlementsQuery = settlementsQuery
                .Where(s => s.IdPublicEntity == _currentUser.LinkedEntityId);
        }
        else
        {
            // DISPATCH_OFFICE / ADMIN: todos los del tenant; filtro opcional de UI
            if (request.IdScrap.HasValue)
                settlementsQuery = settlementsQuery
                    .Where(s => s.IdScrap == request.IdScrap.Value);
        }

        // ── Cargar liquidaciones con agrupación por SCRAP ─────────────────────
        var settlements = await settlementsQuery
            .Select(s => new
            {
                s.Id,
                s.IdScrap,
                s.Year,
                s.Month,
                s.BaseAmount,
                s.AdjustmentsAmount,
                s.TotalAmount
            })
            .ToListAsync(ct);

        var settlementIds = settlements.Select(s => s.Id).ToList();

        // ── Nombres de SCRAPs ──────────────────────────────────────────────────
        var scrapEntityIds = settlements
            .Where(s => s.IdScrap.HasValue)
            .Select(s => s.IdScrap!.Value)
            .Distinct()
            .ToList();

        var scrapNameMap = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapEntityIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name })
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        // ── KPIs globales ─────────────────────────────────────────────────────
        var totalAdjustments = settlements.Sum(s => s.AdjustmentsAmount);

        // ── Comparativa por SCRAP ─────────────────────────────────────────────
        var scrapComparison = settlements
            .Where(s => s.IdScrap.HasValue)
            .GroupBy(s => s.IdScrap!.Value)
            .Select(g =>
            {
                var baseAmt  = g.Sum(s => s.BaseAmount);
                var adjAmt   = g.Sum(s => s.AdjustmentsAmount);
                var totalAmt = g.Sum(s => s.TotalAmount);
                var adjRate  = baseAmt != 0
                    ? Math.Round((double)(adjAmt / baseAmt) * 100, 1)
                    : 0;
                return new EcomodScrapSettlementSummaryDto(
                    ScrapId              : g.Key,
                    ScrapName            : scrapNameMap.GetValueOrDefault(g.Key, g.Key.ToString()[..8]),
                    TotalBaseAmount      : baseAmt,
                    TotalAdjustmentsAmount: adjAmt,
                    TotalAmount          : totalAmt,
                    SettlementCount      : g.Count(),
                    AdjustmentRatePct    : adjRate);
            })
            .OrderByDescending(s => Math.Abs(s.TotalAdjustmentsAmount))
            .ToList();

        // ── Reglas activas del catálogo y su impacto en el ecosistema ─────────
        var activeRules = await _context.EcoModulationRules.AsNoTracking()
            .Join(_context.EcoModulationRuleSets,
                r => r.RuleSetId, rs => rs.Id,
                (r, rs) => new { Rule = r, rs.Status, rs.OwnerId })
            .Where(x => x.Status == "Active"
                     && (ownerId == Guid.Empty || x.OwnerId == ownerId))
            .Select(x => x.Rule)
            .ToListAsync(ct);

        var settlementLines = await _context.SettlementLines.AsNoTracking()
            .Where(sl => settlementIds.Contains(sl.SettlementId))
            .ToListAsync(ct);

        // Mapping settlementId → scrapId para calcular cuántos SCRAPs aplican cada regla
        var settlementScrapMap = settlements
            .Where(s => s.IdScrap.HasValue)
            .ToDictionary(s => s.Id, s => s.IdScrap!.Value);

        var rulesImpact = activeRules.Select(rule =>
        {
            var affectedLines = settlementLines
                .Where(sl => !rule.ProductCategory.HasValue
                          || sl.ProductCategory == rule.ProductCategory)
                .ToList();

            var scrapsApplying = affectedLines
                .Select(sl => settlementScrapMap.GetValueOrDefault(sl.SettlementId))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Count();

            return new EcomodRuleEcosystemImpactDto(
                RuleCode            : rule.RuleCode,
                FeeImpactType       : rule.FeeImpactType,
                FeeImpactValue      : rule.FeeImpactValue,
                ProductCategory     : rule.ProductCategory,
                TotalLinesAffected  : affectedLines.Count,
                TotalEconomicImpact : affectedLines.Sum(l => l.Amount),
                ScrapsApplyingRule  : scrapsApplying);
        }).ToList();

        // ── Tendencia del ecosistema por período ──────────────────────────────
        var ecosystemTrend = settlements
            .GroupBy(s => s.Month.HasValue
                ? $"{s.Year}/{s.Month.Value:D2}"
                : s.Year.ToString())
            .Select(g => new EcomodSettlementPeriodDto(
                Period            : g.Key,
                AdjustmentsAmount : g.Sum(s => s.AdjustmentsAmount),
                SettlementCount   : g.Count()))
            .OrderBy(p => p.Period)
            .ToList();

        return new EcoModulationRegulatoryEconomicViewDto(
            Year                         : request.Year,
            TotalAdjustmentsEcosystem    : totalAdjustments,
            TotalScrapsWithSettlements   : scrapComparison.Count,
            TotalActiveRules             : activeRules.Count,
            ScrapComparison              : scrapComparison,
            RulesEcosystemImpact         : rulesImpact,
            EcosystemTrend               : ecosystemTrend);
    }
}
