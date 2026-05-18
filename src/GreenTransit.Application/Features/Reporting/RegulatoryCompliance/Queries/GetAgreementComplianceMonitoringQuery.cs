using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Services;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Queries;

/// <summary>
/// Dashboard CN-C — Panel de Monitorización de Convenios — Coordinador.
/// Perfiles: COORDINATOR, DISPATCH_OFFICE, ADMIN.
/// </summary>
public sealed record GetAgreementComplianceMonitoringQuery(
    int     Year,
    int?    Month               = null,
    Guid?   IdScrap             = null,
    string? AutonomousCommunity = null,
    string? ProvinceCode        = null,
    string? MunicipalityCode    = null,
    string? WasteStream         = null,
    string? SubStream           = null,
    string? AgreementStatus     = null
) : IRequest<AgreementComplianceMonitoringDto>;

public sealed class GetAgreementComplianceMonitoringQueryHandler
    : IRequestHandler<GetAgreementComplianceMonitoringQuery, AgreementComplianceMonitoringDto>
{
    private readonly IApplicationDbContext       _db;
    private readonly ICurrentUserService         _currentUser;
    private readonly ComplianceMonitoringService _monitor;

    public GetAgreementComplianceMonitoringQueryHandler(
        IApplicationDbContext       db,
        ICurrentUserService         currentUser,
        ComplianceMonitoringService monitor)
    {
        _db          = db;
        _currentUser = currentUser;
        _monitor     = monitor;
    }

    public async Task<AgreementComplianceMonitoringDto> Handle(
        GetAgreementComplianceMonitoringQuery request, CancellationToken ct)
    {
        var ownerId      = _currentUser.OwnerId;
        var isAdmin      = _currentUser.IsInProfile(ProfileConstants.Admin);
        var isDispatch   = _currentUser.IsInProfile(ProfileConstants.DispatchOffice);
        var isCoordinator = _currentUser.IsInProfile(ProfileConstants.Coordinator);
        var coordinatorId = _currentUser.LinkedEntityId;

        // ── Base de convenios ─────────────────────────────────────────────────
        var agQuery = _db.Agreements.AsNoTracking()
            .Where(a => a.OwnerId == ownerId);

        if (isCoordinator && !isAdmin && !isDispatch)
            agQuery = agQuery.Where(a => a.IdCoordinator == coordinatorId);

        if (request.IdScrap.HasValue)
            agQuery = agQuery.Where(a => a.IdScrap == request.IdScrap.Value);
        if (!string.IsNullOrEmpty(request.AutonomousCommunity))
            agQuery = agQuery.Where(a => a.AutonomousCommunity == request.AutonomousCommunity);
        if (!string.IsNullOrEmpty(request.ProvinceCode))
            agQuery = agQuery.Where(a => a.ProvinceCode == request.ProvinceCode);
        if (!string.IsNullOrEmpty(request.MunicipalityCode))
            agQuery = agQuery.Where(a => a.MunicipalityCode == request.MunicipalityCode);
        if (!string.IsNullOrEmpty(request.WasteStream))
            agQuery = agQuery.Where(a => a.WasteStream == request.WasteStream);
        if (!string.IsNullOrEmpty(request.SubStream))
            agQuery = agQuery.Where(a => a.SubStream == request.SubStream);
        if (!string.IsNullOrEmpty(request.AgreementStatus))
            agQuery = agQuery.Where(a => a.Status == request.AgreementStatus);

        var agreements = await agQuery
            .Select(a => new
            {
                a.Id, a.AgreementNumber, a.Status, a.EffectiveFrom, a.EffectiveTo,
                a.WasteStream, a.SubStream, a.TariffModelType,
                a.AutonomousCommunity, a.ProvinceCode, a.MunicipalityCode,
                a.IdScrap, a.IdPublicEntity,
                ScrapName        = a.Scrap!.Name,
                PublicEntityName = a.PublicEntity!.Name,
                ProvinceName     = _db.Provinces.Where(p => p.Code == a.ProvinceCode).Select(p => p.Name).FirstOrDefault() ?? a.ProvinceCode,
                MunicipalityName = _db.Municipalities.Where(m => m.Code == a.MunicipalityCode).Select(m => m.Name).FirstOrDefault() ?? a.MunicipalityCode
            })
            .ToListAsync(ct);

        var activeAgreements     = agreements.Count(a => a.Status == "Active");
        var expiringSoon         = agreements.Count(a => a.Status == "Active"
            && a.EffectiveTo.HasValue
            && (a.EffectiveTo.Value - DateTime.UtcNow).TotalDays <= 90);

        // ── KPIs de liquidaciones ─────────────────────────────────────────────
        var scrapIds = agreements.Select(a => a.IdScrap).Distinct().ToList();

        var settlementsKpi = await _db.Settlements.AsNoTracking()
            .Where(s => s.OwnerId == ownerId
                     && scrapIds.Contains(s.IdScrap)
                     && s.Year    == request.Year)
            .Select(s => new { s.ValidationStatus, s.TotalAmount })
            .ToListAsync(ct);

        var pendingSettlements     = settlementsKpi.Count(s => s.ValidationStatus == "Pending");
        var totalApprovedAmountYear = settlementsKpi
            .Where(s => s.ValidationStatus == "Approved")
            .Sum(s => s.TotalAmount);

        // ── Gráfico cobertura por CCAA ────────────────────────────────────────
        var coverageRaw = agreements
            .Where(a => a.Status == "Active")
            .GroupBy(a => new { a.AutonomousCommunity, a.IdScrap, a.ScrapName });

        // Toneladas gestionadas por SCRAP y CCAA (año actual)
        var tonnesByScrap = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && ep.WasteMove.IdScrap.HasValue && scrapIds.Contains(ep.WasteMove.IdScrap)
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
            .GroupBy(ep => ep.WasteMove.IdScrap)
            .Select(g => new { ScrapId = g.Key, Tonnes = g.SelectMany(ep => ep.EntryPlantResidues).Sum(epr => epr.Weight ?? 0m) / 1000m })
            .ToDictionaryAsync(x => x.ScrapId, x => x.Tonnes, ct);

        var coverageByRegion = coverageRaw.Select(g => new AgreementCoverageByRegionDto(
            g.Key.AutonomousCommunity ?? "",
            g.Key.IdScrap ?? Guid.Empty,
            g.Key.ScrapName,
            g.Count(),
            tonnesByScrap.GetValueOrDefault(g.Key.IdScrap, 0m)))
            .OrderBy(x => x.AutonomousCommunity)
            .ToList();

        // ── Tabla de convenios ────────────────────────────────────────────────
        var agreementRows = agreements.Select(a => new AgreementDetailRowDto(
            a.Id,
            a.AgreementNumber ?? "",
            a.ScrapName,
            a.PublicEntityName,
            a.AutonomousCommunity ?? "",
            a.ProvinceName ?? "",
            a.MunicipalityName ?? "",
            a.WasteStream ?? "",
            a.SubStream,
            a.Status ?? "",
            a.EffectiveFrom,
            a.EffectiveTo,
            a.TariffModelType ?? ""
        )).ToList();

        // ── Liquidaciones mensuales por SCRAP ─────────────────────────────────
        var settlementsMonthly = await _db.Settlements.AsNoTracking()
            .Where(s => s.OwnerId == ownerId
                     && scrapIds.Contains(s.IdScrap)
                     && s.Year    == request.Year)
            .Select(s => new
            {
                s.Id, s.SettlementNumber, s.Year, s.Month, s.IdScrap,
                s.BaseAmount, s.AdjustmentsAmount, s.TaxAmount, s.TotalAmount, s.ValidationStatus,
                ScrapName        = s.Scrap!.Name,
                PublicEntityName = s.PublicEntity!.Name
            })
            .ToListAsync(ct);

        var settlementMonthlyByScrap = settlementsMonthly
            .GroupBy(s => new { s.Year, s.Month, s.IdScrap, s.ScrapName })
            .Select(g => new SettlementMonthlyByScrapDto(g.Key.Year, g.Key.Month ?? 0, g.Key.IdScrap ?? Guid.Empty, g.Key.ScrapName, g.Sum(s => s.TotalAmount)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.ScrapName)
            .ToList();

        var settlementRows = settlementsMonthly.Select(s => new SettlementDetailRowDto(
            s.Id, s.SettlementNumber ?? "", s.ScrapName, s.PublicEntityName,
            s.Year, s.Month ?? 0, s.BaseAmount, s.AdjustmentsAmount, s.TaxAmount, s.TotalAmount, s.ValidationStatus ?? ""
        )).ToList();

        // ── Servicios vs compromisos ──────────────────────────────────────────
        var serviceVsCommitments = new List<ServiceVsCommitmentsDto>();
        foreach (var ag in agreements.Where(a => a.Status == "Active"))
        {
            var servicesCompleted = await _db.WasteMoves.AsNoTracking()
                .CountAsync(wm => wm.OwnerId == ownerId
                               && wm.IdScrap == ag.IdScrap
                               && wm.ServiceOrderId.HasValue
                               && (wm.ServiceStatus == "EN PLANTA" || wm.ServiceStatus == "CLASIFICADO"), ct);

            var tonnesManaged = await _db.EntryPlants.AsNoTracking()
                .Where(ep => ep.WasteMove.OwnerId == ownerId
                          && ep.WasteMove.IdScrap == ag.IdScrap
                          && ep.WasteMove.ActualPickupStart.HasValue
                          && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
                .SelectMany(ep => ep.EntryPlantResidues)
                .SumAsync(epr => (decimal?)epr.Weight ?? 0m, ct) / 1000m;

            serviceVsCommitments.Add(new ServiceVsCommitmentsDto(
                ag.Id, ag.AgreementNumber ?? "", ag.ScrapName, ag.PublicEntityName,
                servicesCompleted, tonnesManaged, null, null,
                servicesCompleted > 0 ? "GREEN" : "RED"));
        }

        // ── Alertas ───────────────────────────────────────────────────────────
        var alerts = coordinatorId.HasValue
            ? (await _monitor.GetCoordinatorAlertsAsync(coordinatorId.Value, ownerId, request.Year, ct)).ToList()
            : [];

        return new AgreementComplianceMonitoringDto(
            Year:                       request.Year,
            Month:                      request.Month,
            TotalActiveAgreements:      activeAgreements,
            AgreementsExpiringSoon:     expiringSoon,
            PendingSettlements:         pendingSettlements,
            TotalApprovedAmountYear:    totalApprovedAmountYear,
            VariationVsPrevYearPct:     0d,
            CoverageByRegion:           coverageByRegion,
            AgreementRows:              agreementRows,
            SettlementMonthlyByScrap:   settlementMonthlyByScrap,
            SettlementRows:             settlementRows,
            ServiceVsCommitments:       serviceVsCommitments,
            Alerts:                     alerts);
    }
}
