using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Services;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Queries;

/// <summary>
/// Dashboard CN-A — Panel de Cumplimiento Normativo — Visión SCRAP.
/// </summary>
public sealed record GetScrapComplianceOverviewQuery(
    int     Year,
    int?    Quarter             = null,
    int?    Month               = null,
    string? AutonomousCommunity = null,
    string? Category            = null,
    string? FlowType            = null,
    Guid?   IdScrap             = null
) : IRequest<ScrapComplianceOverviewDto>;

public sealed class GetScrapComplianceOverviewQueryHandler
    : IRequestHandler<GetScrapComplianceOverviewQuery, ScrapComplianceOverviewDto>
{
    private readonly IApplicationDbContext        _db;
    private readonly ICurrentUserService          _currentUser;
    private readonly ComplianceMonitoringService  _monitor;
    private readonly IConfiguration               _config;

    public GetScrapComplianceOverviewQueryHandler(
        IApplicationDbContext       db,
        ICurrentUserService         currentUser,
        ComplianceMonitoringService monitor,
        IConfiguration              config)
    {
        _db          = db;
        _currentUser = currentUser;
        _monitor     = monitor;
        _config      = config;
    }

    public async Task<ScrapComplianceOverviewDto> Handle(
        GetScrapComplianceOverviewQuery request, CancellationToken ct)
    {
        var ownerId  = _currentUser.OwnerId;
        var isAdmin  = _currentUser.IsInProfile(ProfileConstants.Admin);
        // ADMIN puede filtrar por SCRAP desde UI; SCRAP ve siempre su propia entidad
        var scrapId  = isAdmin && request.IdScrap.HasValue
            ? request.IdScrap
            : _currentUser.LinkedEntityId;

        // ── Objetivos regulatorios del año ────────────────────────────────────
        var targets = await _db.RegulatoryTargets.AsNoTracking()
            .Where(t => t.OwnerId == ownerId && t.Year == request.Year)
            .ToListAsync(ct);
        var defaultTarget = targets.FirstOrDefault();
        var configDefault = double.TryParse(_config["RegulatoryTargets:DefaultMinRecyclingPercent"], out var cfgVal) ? cfgVal : 65.0;
        var targetRecycling = (decimal)(defaultTarget?.MinRecyclingPercent ?? configDefault);

        // ── Pesos en planta (entradas) filtrados por SCRAP ────────────────────
        // OPTIMIZACIÓN: una sola consulta agregada por mes/categoría/CCAA que
        // sustituye a las anteriores 3 round-trips (totalEntry, realWeightByCategory
        // y allEntryByMonth). El servidor SQL agrega y devolvemos un conjunto pequeño.
        var entryAggregated = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && (isAdmin || ep.WasteMove.IdScrap == scrapId || ep.WasteMove.IdScrap2 == scrapId)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .SelectMany(ep => ep.EntryPlantResidues)
            .GroupBy(epr => new
            {
                Month     = epr.EntryPlant.WasteMove.ActualPickupStart!.Value.Month,
                Category  = epr.Residue.ProductCategory ?? "",
                StateCode = epr.EntryPlant.WasteMove.Destination!.StateCode ?? ""
            })
            .Select(g => new
            {
                g.Key.Month,
                g.Key.Category,
                g.Key.StateCode,
                Weight = g.Sum(r => (decimal?)r.Weight) ?? 0m
            })
            .ToListAsync(ct);

        // totalEntry con filtro de CCAA aplicado en memoria
        var totalEntry = string.IsNullOrEmpty(request.AutonomousCommunity)
            ? entryAggregated.Sum(x => x.Weight)
            : entryAggregated.Where(x => x.StateCode == request.AutonomousCommunity).Sum(x => x.Weight);

        // ── Pesos en tratamiento ──────────────────────────────────────────────
        // OPTIMIZACIÓN: una sola consulta agregada por mes y tipo de operación que
        // sustituye a 4 round-trips (recyclingWeight, valorizationWeight, reuseWeight
        // y allTreatmentByMonth).
        var treatmentAggregated = await _db.TreatmentPlants.AsNoTracking()
            .Where(tp => tp.WasteMove!.OwnerId == ownerId
                      && (isAdmin
                          || tp.WasteMove.IdScrap  == scrapId
                          || tp.WasteMove.IdScrap2 == scrapId)
                      && tp.WasteMove.ActualPickupStart.HasValue
                      && tp.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .GroupBy(tp => new
            {
                Month       = tp.WasteMove!.ActualPickupStart!.Value.Month,
                IsRecycling = tp.TreatmentOperation!.IsRecycling,
                IsEnergy    = tp.TreatmentOperation.IsEnergyRecovery,
                IsReuse     = tp.TreatmentOperation.IsPreparationForReuse
            })
            .Select(g => new
            {
                g.Key.Month,
                g.Key.IsRecycling,
                g.Key.IsEnergy,
                g.Key.IsReuse,
                Weight = g.SelectMany(tp => tp.TreatmentPlantResidues).Sum(r => (decimal?)r.WeightTotal) ?? 0m
            })
            .ToListAsync(ct);

        var recyclingWeight    = treatmentAggregated.Where(x => x.IsRecycling).Sum(x => x.Weight);
        var valorizationWeight = treatmentAggregated.Where(x => x.IsEnergy).Sum(x => x.Weight);
        var reuseWeight        = treatmentAggregated.Where(x => x.IsReuse).Sum(x => x.Weight);

        var recyclingPct    = totalEntry > 0 ? recyclingWeight    / totalEntry * 100m : 0m;
        var valorizationPct = totalEntry > 0 ? valorizationWeight / totalEntry * 100m : 0m;
        var reusePct        = totalEntry > 0 ? reuseWeight        / totalEntry * 100m : 0m;

        // ── Cuotas de mercado ─────────────────────────────────────────────────
        var msQuery = _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId
                      && (isAdmin || ms.IdScrap == scrapId)
                      && ms.Year == request.Year);

        if (!string.IsNullOrEmpty(request.AutonomousCommunity))
            msQuery = msQuery.Where(ms => ms.AutonomousCommunity == request.AutonomousCommunity);
        if (!string.IsNullOrEmpty(request.Category))
            msQuery = msQuery.Where(ms => ms.Category == request.Category);
        if (!string.IsNullOrEmpty(request.FlowType))
            msQuery = msQuery.Where(ms => ms.FlowType == request.FlowType);

        var marketSharesData = await msQuery.ToListAsync(ct);
        var targetTotal      = marketSharesData.Sum(ms => ms.Weight);

        // Peso real por categoría — derivado del agregado ya cargado (sin query extra)
        var realWeightByCategory = entryAggregated
            .GroupBy(x => x.Category)
            .ToDictionary(g => g.Key, g => (decimal?)g.Sum(x => x.Weight));

        var marketShareRows = marketSharesData.Select(ms =>
        {
            var real = realWeightByCategory.GetValueOrDefault(ms.Category ?? "", 0m) ?? 0m;
            var pct  = ms.Weight > 0 ? real / ms.Weight * 100m : 0m;
            return new MarketShareRowDto(
                ms.Category ?? "",
                ms.AutonomousCommunity ?? "",
                ms.FlowType ?? "",
                ms.Weight,
                real,
                pct,
                ComplianceMonitoringService.GetStatus(pct, 100m),
                pct < 80m,
                []);
        }).ToList();

        var totalRealForQuota   = realWeightByCategory.Values.Sum() ?? 0m;
        var marketShareCompPct  = targetTotal > 0 ? totalRealForQuota / targetTotal * 100m : 0m;

        // ── Convenios ─────────────────────────────────────────────────────────
        var agreementsRaw = await _db.Agreements.AsNoTracking()
            .Where(a => a.OwnerId == ownerId
                     && (isAdmin || a.IdScrap == scrapId))
            .Select(a => new
            {
                a.Id, a.AgreementNumber, a.Status,
                a.EffectiveFrom, a.EffectiveTo, a.WasteStream,
                a.AutonomousCommunity, a.ProvinceCode, a.MunicipalityCode,
                PublicEntityName = a.PublicEntity!.Name
            })
            .ToListAsync(ct);

        // Resolver nombres de provincia y municipio en una sola consulta por lote
        var provinceCodes    = agreementsRaw.Where(a => a.ProvinceCode    != null).Select(a => a.ProvinceCode!).Distinct().ToList();
        var municipalityCodes = agreementsRaw.Where(a => a.MunicipalityCode != null).Select(a => a.MunicipalityCode!).Distinct().ToList();

        var provinceNames = provinceCodes.Count > 0
            ? await _db.Provinces.AsNoTracking()
                .Where(p => provinceCodes.Contains(p.Code))
                .Select(p => new { p.Code, p.Name })
                .ToDictionaryAsync(p => p.Code, p => p.Name, ct)
            : new Dictionary<string, string>();

        var municipalityNames = municipalityCodes.Count > 0
            ? await _db.Municipalities.AsNoTracking()
                .Where(m => municipalityCodes.Contains(m.Code))
                .Select(m => new { m.Code, m.Name })
                .ToDictionaryAsync(m => m.Code, m => m.Name, ct)
            : new Dictionary<string, string>();

        var activeAgreements = agreementsRaw.Count(a => a.Status == "Active");

        var agreementRows = agreementsRaw.Select(a => new AgreementSummaryRowDto(
            a.Id,
            a.AgreementNumber ?? "",
            a.PublicEntityName ?? "",
            a.AutonomousCommunity ?? "",
            (a.ProvinceCode != null && provinceNames.TryGetValue(a.ProvinceCode, out var pn) ? pn : a.ProvinceCode) ?? "",
            (a.MunicipalityCode != null && municipalityNames.TryGetValue(a.MunicipalityCode, out var mn) ? mn : a.MunicipalityCode) ?? "",
            a.WasteStream ?? "",
            a.Status ?? "",
            a.EffectiveFrom,
            a.EffectiveTo,
            a.EffectiveTo.HasValue ? (int)(a.EffectiveTo.Value - DateTime.UtcNow).TotalDays : 9999,
            _monitor.GetExpiryStatus(a.EffectiveTo)
        )).ToList();

        // ── Liquidaciones ─────────────────────────────────────────────────────
        var settlementsRaw = await _db.Settlements.AsNoTracking()
            .Where(s => s.OwnerId == ownerId
                     && (isAdmin || s.IdScrap == scrapId)
                     && s.Year    == request.Year)
            .Select(s => new
            {
                s.Id, s.SettlementNumber, s.Year, s.Month,
                s.TotalAmount, s.Currency, s.ValidationStatus, s.ValidatedAt,
                PublicEntityName = s.PublicEntity!.Name
            })
            .ToListAsync(ct);

        var settlementMonthly = settlementsRaw
            .GroupBy(s => new { s.Year, s.Month })
            .Select(g => new SettlementMonthlyBarDto(
                g.Key.Year,
                g.Key.Month ?? 0,
                g.Where(s => s.ValidationStatus == "Pending").Sum(s => s.TotalAmount),
                g.Where(s => s.ValidationStatus == "Approved").Sum(s => s.TotalAmount),
                g.Where(s => s.ValidationStatus == "Rejected").Sum(s => s.TotalAmount)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        var settlementRows = settlementsRaw.Select(s => new SettlementRowDto(
            s.Id, s.SettlementNumber ?? "", s.Year, s.Month ?? 0,
            s.PublicEntityName ?? "", s.TotalAmount, s.Currency ?? "EUR",
            s.ValidationStatus ?? "", s.ValidatedAt)).ToList();

        // ── Evolución trimestral ──────────────────────────────────────────────
        // Cargamos los datos del año completo una sola vez y agrupamos en memoria

        var allEntryByMonth = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && (isAdmin || ep.WasteMove.IdScrap == scrapId || ep.WasteMove.IdScrap2 == scrapId)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .SelectMany(ep => ep.EntryPlantResidues)
            .Select(epr => new { Month = epr.EntryPlant.WasteMove.ActualPickupStart!.Value.Month, Weight = (decimal?)epr.Weight ?? 0m })
            .ToListAsync(ct);

        var allTreatmentByMonth = await _db.TreatmentPlants.AsNoTracking()
            .Where(tp => tp.WasteMove!.OwnerId == ownerId
                      && (isAdmin || tp.WasteMove.IdScrap == scrapId || tp.WasteMove.IdScrap2 == scrapId)
                      && tp.WasteMove.ActualPickupStart.HasValue
                      && tp.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .Select(tp => new
            {
                Month        = tp.WasteMove!.ActualPickupStart!.Value.Month,
                IsRecycling  = tp.TreatmentOperation!.IsRecycling,
                IsEnergy     = tp.TreatmentOperation.IsEnergyRecovery,
                IsReuse      = tp.TreatmentOperation.IsPreparationForReuse,
                Weight       = tp.TreatmentPlantResidues.Sum(r => (decimal?)r.WeightTotal ?? 0m)
            })
            .ToListAsync(ct);

        var quarterlyTrend = Enumerable.Range(1, 4).Select(q =>
        {
            var months     = Enumerable.Range((q - 1) * 3 + 1, 3).ToHashSet();
            var qEntry     = allEntryByMonth.Where(x => months.Contains(x.Month)).Sum(x => x.Weight);
            var qRecycling = allTreatmentByMonth.Where(x => months.Contains(x.Month) && x.IsRecycling).Sum(x => x.Weight);
            var qVal       = allTreatmentByMonth.Where(x => months.Contains(x.Month) && x.IsEnergy).Sum(x => x.Weight);
            var qReuse     = allTreatmentByMonth.Where(x => months.Contains(x.Month) && x.IsReuse).Sum(x => x.Weight);
            return new QuarterlyComplianceTrendDto(
                request.Year, q,
                qEntry > 0 ? qRecycling / qEntry * 100m : 0m,
                qEntry > 0 ? qVal       / qEntry * 100m : 0m,
                qEntry > 0 ? qReuse     / qEntry * 100m : 0m,
                targetRecycling);
        }).ToList();

        // ── Alertas ───────────────────────────────────────────────────────────
        var alerts = isAdmin
            ? new List<ComplianceAlertDto>()
            : scrapId.HasValue
                ? (await _monitor.GetScrapAlertsAsync(scrapId.Value, ownerId, request.Year, ct,
                    realWeightByCategory.ToDictionary(kv => kv.Key, kv => kv.Value ?? 0m))).ToList()
                : [];

        // ── Variaciones vs periodo anterior ───────────────────────────────────
        // Una sola consulta para el año anterior: entrada total y peso reciclado
        // se obtienen agregando en SQL (sin SelectMany previo a Sum por categoría).
        var prevYear = request.Year - 1;
        var prevEntry = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && (isAdmin || ep.WasteMove.IdScrap == scrapId || ep.WasteMove.IdScrap2 == scrapId)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == prevYear)
            .SelectMany(ep => ep.EntryPlantResidues)
            .SumAsync(epr => (decimal?)epr.Weight ?? 0m, ct);

        var prevRecycling = prevEntry > 0
            ? await _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && (isAdmin || tp.WasteMove.IdScrap == scrapId || tp.WasteMove.IdScrap2 == scrapId)
                          && tp.WasteMove.ActualPickupStart.HasValue
                          && tp.WasteMove.ActualPickupStart.Value.Year == prevYear
                          && tp.TreatmentOperation!.IsRecycling)
                .SelectMany(tp => tp.TreatmentPlantResidues)
                .SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct)
            : 0m;
        var prevRecyclingPct = prevEntry > 0 ? prevRecycling / prevEntry * 100m : 0m;

        var variationRecycling = prevRecyclingPct > 0
            ? (double)((recyclingPct - prevRecyclingPct) / prevRecyclingPct * 100m)
            : 0d;

        return new ScrapComplianceOverviewDto(
            Year:                           request.Year,
            Quarter:                        request.Quarter,
            Month:                          request.Month,
            RecyclingRatePct:               recyclingPct,
            ValorizationRatePct:            valorizationPct,
            ReuseRatePct:                   reusePct,
            MarketShareCompliancePct:       marketShareCompPct,
            ActiveAgreements:               activeAgreements,
            VariationRecyclingVsPrevPct:    variationRecycling,
            VariationValorizationVsPrevPct: 0d,
            VariationMarketShareVsPrevPct:  0d,
            RecyclingStatus:                ComplianceMonitoringService.GetStatus(recyclingPct, targetRecycling),
            ValorizationStatus:             ComplianceMonitoringService.GetStatus(valorizationPct, 40m),
            MarketShareStatus:              ComplianceMonitoringService.GetStatus(marketShareCompPct, 100m),
            QuarterlyTrend:                 quarterlyTrend,
            MarketShareRows:                marketShareRows,
            Agreements:                     agreementRows,
            SettlementMonthly:              settlementMonthly,
            Settlements:                    settlementRows,
            Alerts:                         alerts);
    }
}
