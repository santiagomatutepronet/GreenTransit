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
    int?    Quarter          = null,
    int?    Month            = null,
    string? AutonomousCommunity = null,
    string? Category         = null,
    string? FlowType         = null
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
        var scrapId  = _currentUser.LinkedEntityId;
        var isAdmin  = _currentUser.IsInProfile(ProfileConstants.Admin);

        // ── Objetivos regulatorios del año ────────────────────────────────────
        var targets = await _db.RegulatoryTargets.AsNoTracking()
            .Where(t => t.OwnerId == ownerId && t.Year == request.Year)
            .ToListAsync(ct);
        var defaultTarget = targets.FirstOrDefault();
        var configDefault = double.TryParse(_config["RegulatoryTargets:DefaultMinRecyclingPercent"], out var cfgVal) ? cfgVal : 65.0;
        var targetRecycling = (decimal)(defaultTarget?.MinRecyclingPercent ?? configDefault);

        // ── Pesos en planta (entradas) filtrados por SCRAP ────────────────────
        var entryResiduesQ = _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && (isAdmin || ep.WasteMove.IdScrap == scrapId || ep.WasteMove.IdScrap2 == scrapId)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .SelectMany(ep => ep.EntryPlantResidues);

        if (!string.IsNullOrEmpty(request.AutonomousCommunity))
            entryResiduesQ = entryResiduesQ.Where(epr =>
                epr.EntryPlant.WasteMove.Destination!.StateCode == request.AutonomousCommunity);

        var totalEntry = await entryResiduesQ.SumAsync(epr => epr.Weight ?? 0m, ct);

        // ── Pesos en tratamiento ──────────────────────────────────────────────
        var treatmentQ = _db.TreatmentPlants.AsNoTracking()
            .Where(tp => tp.WasteMove!.OwnerId == ownerId
                      && (isAdmin
                          || tp.WasteMove.IdScrap  == scrapId
                          || tp.WasteMove.IdScrap2 == scrapId)
                      && tp.WasteMove.ActualPickupStart.HasValue
                      && tp.WasteMove.ActualPickupStart.Value.Year == request.Year);

        var recyclingWeight    = await treatmentQ
            .Where(tp => tp.TreatmentOperation!.IsRecycling)
            .SelectMany(tp => tp.TreatmentPlantResidues)
            .SumAsync(r => r.WeightTotal ?? 0m, ct);

        var valorizationWeight = await treatmentQ
            .Where(tp => tp.TreatmentOperation!.IsEnergyRecovery)
            .SelectMany(tp => tp.TreatmentPlantResidues)
            .SumAsync(r => r.WeightTotal ?? 0m, ct);

        var reuseWeight        = await treatmentQ
            .Where(tp => tp.TreatmentOperation!.IsPreparationForReuse)
            .SelectMany(tp => tp.TreatmentPlantResidues)
            .SumAsync(r => r.WeightTotal ?? 0m, ct);

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

        // Peso real por categoría para calcular cumplimiento de cuota
        var realWeightByCategory = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && (isAdmin || ep.WasteMove.IdScrap == scrapId || ep.WasteMove.IdScrap2 == scrapId)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .SelectMany(ep => ep.EntryPlantResidues)
            .GroupBy(epr => epr.Residue.ProductCategory ?? "")
            .Select(g => new { Category = g.Key, WeightKg = g.Sum(r => r.Weight) })
            .ToDictionaryAsync(x => x.Category, x => x.WeightKg, ct);

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
                PublicEntityName = a.PublicEntity!.Name,
                ProvinceName     = _db.Provinces.Where(p => p.Code == a.ProvinceCode).Select(p => p.Name).FirstOrDefault() ?? a.ProvinceCode,
                MunicipalityName = _db.Municipalities.Where(m => m.Code == a.MunicipalityCode).Select(m => m.Name).FirstOrDefault() ?? a.MunicipalityCode
            })
            .ToListAsync(ct);

        var activeAgreements = agreementsRaw.Count(a => a.Status == "Active");

        var agreementRows = agreementsRaw.Select(a => new AgreementSummaryRowDto(
            a.Id,
            a.AgreementNumber ?? "",
            a.PublicEntityName ?? "",
            a.AutonomousCommunity ?? "",
            a.ProvinceName ?? "",
            a.MunicipalityName ?? "",
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
        // Datos simplificados: agrupamos por trimestre del año actual
        var quarterlyTrend = new List<QuarterlyComplianceTrendDto>();
        for (var q = 1; q <= 4; q++)
        {
            var qFrom = new DateTime(request.Year, (q - 1) * 3 + 1, 1);
            var qTo   = qFrom.AddMonths(3);
            var qEntry = await _db.EntryPlants.AsNoTracking()
                .Where(ep => ep.WasteMove.OwnerId == ownerId
                          && (isAdmin || ep.WasteMove.IdScrap == scrapId || ep.WasteMove.IdScrap2 == scrapId)
                          && ep.WasteMove.ActualPickupStart >= qFrom
                          && ep.WasteMove.ActualPickupStart <  qTo)
                .SelectMany(ep => ep.EntryPlantResidues)
                .SumAsync(epr => (decimal?)epr.Weight ?? 0m, ct);

            var qRecycling = await _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && (isAdmin || tp.WasteMove.IdScrap == scrapId || tp.WasteMove.IdScrap2 == scrapId)
                          && tp.WasteMove.ActualPickupStart >= qFrom
                          && tp.WasteMove.ActualPickupStart <  qTo
                          && tp.TreatmentOperation!.IsRecycling)
                .SelectMany(tp => tp.TreatmentPlantResidues)
                .SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

            var qValorization = await _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && (isAdmin || tp.WasteMove.IdScrap == scrapId || tp.WasteMove.IdScrap2 == scrapId)
                          && tp.WasteMove.ActualPickupStart >= qFrom
                          && tp.WasteMove.ActualPickupStart <  qTo
                          && tp.TreatmentOperation!.IsEnergyRecovery)
                .SelectMany(tp => tp.TreatmentPlantResidues)
                .SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

            var qReuse = await _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && (isAdmin || tp.WasteMove.IdScrap == scrapId || tp.WasteMove.IdScrap2 == scrapId)
                          && tp.WasteMove.ActualPickupStart >= qFrom
                          && tp.WasteMove.ActualPickupStart <  qTo
                          && tp.TreatmentOperation!.IsPreparationForReuse)
                .SelectMany(tp => tp.TreatmentPlantResidues)
                .SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

            quarterlyTrend.Add(new QuarterlyComplianceTrendDto(
                request.Year, q,
                qEntry > 0 ? qRecycling    / qEntry * 100m : 0m,
                qEntry > 0 ? qValorization / qEntry * 100m : 0m,
                qEntry > 0 ? qReuse        / qEntry * 100m : 0m,
                targetRecycling));
        }

        // ── Alertas ───────────────────────────────────────────────────────────
        var alerts = isAdmin
            ? new List<ComplianceAlertDto>()
            : scrapId.HasValue
                ? (await _monitor.GetScrapAlertsAsync(scrapId.Value, ownerId, request.Year, ct)).ToList()
                : [];

        // ── Variaciones vs periodo anterior ───────────────────────────────────
        var prevYear       = request.Year - 1;
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
