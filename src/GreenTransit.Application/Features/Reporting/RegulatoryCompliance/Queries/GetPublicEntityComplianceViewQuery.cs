using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Queries;

/// <summary>
/// Dashboard CN-D — Panel de Cumplimiento Normativo — Entidad Pública.
/// Perfiles: PUBLIC_ENT, ADMIN.
/// </summary>
public sealed record GetPublicEntityComplianceViewQuery(
    int     Year,
    int?    Month       = null,
    Guid?   IdScrap     = null,
    string? WasteStream = null,
    string? Category    = null
) : IRequest<PublicEntityComplianceViewDto>;

public sealed class GetPublicEntityComplianceViewQueryHandler
    : IRequestHandler<GetPublicEntityComplianceViewQuery, PublicEntityComplianceViewDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public GetPublicEntityComplianceViewQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<PublicEntityComplianceViewDto> Handle(
        GetPublicEntityComplianceViewQuery request, CancellationToken ct)
    {
        var ownerId    = _currentUser.OwnerId;
        var entityId   = _currentUser.LinkedEntityId;
        var isAdmin    = _currentUser.IsInProfile(ProfileConstants.Admin);

        // ── Base WasteMoves para la entidad pública ───────────────────────────
        var wmBase = _db.WasteMoves.AsNoTracking()
            .Where(wm => wm.OwnerId == ownerId
                      && wm.ActualPickupStart.HasValue
                      && wm.ActualPickupStart.Value.Year == request.Year
                      && (isAdmin || wm.ServiceOrder!.IdIssuedBy == entityId));

        if (request.IdScrap.HasValue)
            wmBase = wmBase.Where(wm => wm.IdScrap == request.IdScrap.Value);
        if (request.Month.HasValue)
            wmBase = wmBase.Where(wm => wm.ActualPickupStart!.Value.Month == request.Month.Value);

        var totalKg = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year
                      && (isAdmin || ep.WasteMove.ServiceOrder!.IdIssuedBy == entityId)
                      && (!request.IdScrap.HasValue || ep.WasteMove.IdScrap == request.IdScrap.Value)
                      && (!request.Month.HasValue || ep.WasteMove.ActualPickupStart!.Value.Month == request.Month.Value))
            .SelectMany(ep => ep.EntryPlantResidues)
            .SumAsync(epr => (decimal?)epr.Weight ?? 0m, ct);

        var servicesCompleted = await wmBase
            .CountAsync(wm => wm.ServiceStatus == "EN PLANTA" || wm.ServiceStatus == "CLASIFICADO", ct);

        var scrapCount = await wmBase
            .Select(wm => wm.IdScrap)
            .Distinct()
            .CountAsync(ct);

        // ── Liquidaciones de compensación ─────────────────────────────────────
        var settlementsQ = _db.Settlements.AsNoTracking()
            .Where(s => s.OwnerId == ownerId
                     && (isAdmin || s.IdPublicEntity == entityId)
                     && s.Year    == request.Year);

        var totalCompensated = await settlementsQ
            .Where(s => s.ValidationStatus == "Approved")
            .SumAsync(s => (decimal?)s.TotalAmount ?? 0m, ct);

        var compensationRows = await settlementsQ
            .Select(s => new
            {
                s.Id, s.SettlementNumber, s.Year, s.Month,
                s.BaseAmount, s.AdjustmentsAmount, s.TotalAmount, s.ValidationStatus, s.ValidatedAt,
                ScrapName = s.Scrap!.Name
            })
            .ToListAsync(ct);

        var compensationSettlements = compensationRows.Select(s => new CompensationSettlementRowDto(
            s.Id, s.ScrapName, s.SettlementNumber ?? "", s.Year, s.Month ?? 0,
            s.BaseAmount, s.AdjustmentsAmount, s.TotalAmount, s.ValidationStatus ?? "", s.ValidatedAt
        )).ToList();

        // ── Evolución mensual por SCRAP ───────────────────────────────────────
        var monthlyRaw = await _db.EntryPlants.AsNoTracking()
            .Where(ep => ep.WasteMove.OwnerId == ownerId
                      && ep.WasteMove.ActualPickupStart.HasValue
                      && ep.WasteMove.ActualPickupStart.Value.Year == request.Year
                      && (isAdmin || ep.WasteMove.ServiceOrder!.IdIssuedBy == entityId)
                      && (!request.IdScrap.HasValue || ep.WasteMove.IdScrap == request.IdScrap.Value)
                      && (!request.Month.HasValue || ep.WasteMove.ActualPickupStart!.Value.Month == request.Month.Value))
            .SelectMany(ep => ep.EntryPlantResidues
                .Select(epr => new { ep.WasteMove.IdScrap, Month = ep.WasteMove.ActualPickupStart!.Value.Month, epr.Weight }))
            .GroupBy(x => new { x.IdScrap, x.Month })
            .Select(g => new { g.Key.IdScrap, g.Key.Month, WeightKg = g.Sum(x => x.Weight) })
            .ToListAsync(ct);

        var scrapNames = await _db.BusinessEntities.AsNoTracking()
            .Where(e => monthlyRaw.Select(r => r.IdScrap).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var monthlyByScrap = monthlyRaw.Select(x => new MonthlyCollectionByScrapDto(
            request.Year, x.Month, x.IdScrap ?? Guid.Empty,
            scrapNames.GetValueOrDefault(x.IdScrap ?? Guid.Empty, ""), x.WeightKg ?? 0m, 0m))
            .OrderBy(x => x.Month).ThenBy(x => x.ScrapName)
            .ToList();

        // ── Cumplimiento por SCRAP en territorio ──────────────────────────────
        var entityInfo = await _db.BusinessEntities.AsNoTracking()
            .Where(e => e.Id == entityId)
            .Select(e => new { AutonomousCommunity = e.StateCode })
            .FirstOrDefaultAsync(ct);

        var msQuery = _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId && ms.Year == request.Year);

        if (!string.IsNullOrEmpty(entityInfo?.AutonomousCommunity))
            msQuery = msQuery.Where(ms => ms.AutonomousCommunity == entityInfo.AutonomousCommunity);

        var marketShares = await msQuery
            .Select(ms => new { ms.IdScrap, ms.Category, ms.FlowType, ms.Weight, ScrapName = ms.Scrap!.Name })
            .ToListAsync(ct);

        var scrapComplianceRows = new List<ScrapTerritorialComplianceDto>();
        foreach (var msGroup in marketShares.GroupBy(ms => new { ms.IdScrap, ms.ScrapName, ms.Category, ms.FlowType }))
        {
            var target = msGroup.Sum(ms => ms.Weight);
            var real   = monthlyRaw.Where(r => r.IdScrap == msGroup.Key.IdScrap).Sum(r => r.WeightKg ?? 0m);
            var pct    = target > 0 ? real / target * 100m : 0m;
            scrapComplianceRows.Add(new ScrapTerritorialComplianceDto(
                msGroup.Key.IdScrap ?? Guid.Empty, msGroup.Key.ScrapName,
                msGroup.Key.Category ?? "", msGroup.Key.FlowType ?? "",
                target, real, pct, Services.ComplianceMonitoringService.GetStatus(pct, 100m)));
        }

        // ── Métodos de recolección (donut) ────────────────────────────────────
        var collectionMethods = new List<CollectionMethodSliceDto> {
            new("No disponible", totalKg, 100d)
        };

        // ── Incidencias ───────────────────────────────────────────────────────
        var incidentsRaw = await _db.Incidents.AsNoTracking()
            .Where(i => i.OwnerId == ownerId && i.ClosedAt == null)
            .Select(i => new
            {
                i.Id, i.Type, i.Severity, i.OpenedAt, i.WasteMoveReference,
                ScrapName = (string?)null   // sin join directo en el modelo
            })
            .Take(50)
            .ToListAsync(ct);

        var openIncidents = incidentsRaw.Count;
        var incidentRows  = incidentsRaw.Select(i => new IncidentRowDto(
            i.Id, i.Type ?? "", i.Severity ?? "", i.WasteMoveReference, i.ScrapName,
            i.OpenedAt, (int)(DateTime.UtcNow - i.OpenedAt).TotalDays
        )).ToList();

        return new PublicEntityComplianceViewDto(
            Year:                       request.Year,
            Month:                      request.Month,
            TotalTonnesCollected:       totalKg / 1000m,
            ServicesCompleted:          servicesCompleted,
            ScrapCount:                 scrapCount,
            TotalCompensatedAmount:     totalCompensated,
            VariationVsPrevPeriodPct:   0d,
            MonthlyByScrap:             monthlyByScrap,
            ScrapCompliance:            scrapComplianceRows,
            CompensationSettlements:    compensationSettlements,
            CollectionMethods:          collectionMethods,
            Incidents:                  incidentRows,
            OpenIncidentsCount:         openIncidents);
    }
}
