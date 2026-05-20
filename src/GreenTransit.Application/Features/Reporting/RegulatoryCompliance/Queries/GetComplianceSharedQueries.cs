using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Services;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Queries;

/// <summary>
/// Widget compartido — Resumen de alertas de incumplimiento para cualquier perfil.
/// </summary>
public sealed record GetComplianceAlertsSummaryQuery(
    int  Year,
    Guid? IdScrap = null
) : IRequest<IReadOnlyList<ComplianceAlertDto>>;

public sealed class GetComplianceAlertsSummaryQueryHandler
    : IRequestHandler<GetComplianceAlertsSummaryQuery, IReadOnlyList<ComplianceAlertDto>>
{
    private readonly IApplicationDbContext       _db;
    private readonly ICurrentUserService         _currentUser;
    private readonly ComplianceMonitoringService _monitor;

    public GetComplianceAlertsSummaryQueryHandler(
        IApplicationDbContext       db,
        ICurrentUserService         currentUser,
        ComplianceMonitoringService monitor)
    {
        _db          = db;
        _currentUser = currentUser;
        _monitor     = monitor;
    }

    public async Task<IReadOnlyList<ComplianceAlertDto>> Handle(
        GetComplianceAlertsSummaryQuery request, CancellationToken ct)
    {
        var ownerId  = _currentUser.OwnerId;
        var entityId = request.IdScrap ?? _currentUser.LinkedEntityId;
        if (!entityId.HasValue) return [];
        return await _monitor.GetScrapAlertsAsync(entityId.Value, ownerId, request.Year, ct);
    }
}

/// <summary>
/// Widget compartido — Tasa de reciclaje por flujo de residuo.
/// </summary>
public sealed record GetRecyclingRateByFlowQuery(
    int    Year,
    Guid?  IdScrap  = null,
    string? FlowType = null
) : IRequest<IReadOnlyList<RecyclingRateByFlowDto>>;

public sealed class GetRecyclingRateByFlowQueryHandler
    : IRequestHandler<GetRecyclingRateByFlowQuery, IReadOnlyList<RecyclingRateByFlowDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public GetRecyclingRateByFlowQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RecyclingRateByFlowDto>> Handle(
        GetRecyclingRateByFlowQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var msQuery = _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId && ms.Year == request.Year);

        if (request.IdScrap.HasValue)
            msQuery = msQuery.Where(ms => ms.IdScrap == request.IdScrap.Value);
        if (!string.IsNullOrEmpty(request.FlowType))
            msQuery = msQuery.Where(ms => ms.FlowType == request.FlowType);

        var flows = await msQuery
            .Select(ms => new { ms.FlowType, ms.Category })
            .Distinct()
            .ToListAsync(ct);

        var targets = await _db.RegulatoryTargets.AsNoTracking()
            .Where(t => t.OwnerId == ownerId && t.Year == request.Year)
            .ToListAsync(ct);

        var result = new List<RecyclingRateByFlowDto>();
        foreach (var flow in flows)
        {
            var entryW = await _db.EntryPlants.AsNoTracking()
                .Where(ep => ep.WasteMove.OwnerId == ownerId
                          && (!request.IdScrap.HasValue || ep.WasteMove.IdScrap == request.IdScrap.Value)
                          && ep.WasteMove.ActualPickupStart.HasValue
                          && ep.WasteMove.ActualPickupStart.Value.Year == request.Year)
                .SelectMany(ep => ep.EntryPlantResidues)
                .Where(epr => epr.Residue.ProductCategory == flow.Category)
                .SumAsync(epr => (decimal?)epr.Weight ?? 0m, ct);

            if (entryW == 0) continue;

            var treatBase = _db.TreatmentPlants.AsNoTracking()
                .Where(tp => tp.WasteMove!.OwnerId == ownerId
                          && (!request.IdScrap.HasValue || tp.WasteMove.IdScrap == request.IdScrap.Value)
                          && tp.WasteMove.ActualPickupStart.HasValue
                          && tp.WasteMove.ActualPickupStart.Value.Year == request.Year);

            var recyclingW    = await treatBase.Where(tp => tp.TreatmentOperation!.IsRecycling)
                .SelectMany(tp => tp.TreatmentPlantResidues).SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);
            var valorizationW = await treatBase.Where(tp => tp.TreatmentOperation!.IsEnergyRecovery)
                .SelectMany(tp => tp.TreatmentPlantResidues).SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);
            var reuseW        = await treatBase.Where(tp => tp.TreatmentOperation!.IsPreparationForReuse)
                .SelectMany(tp => tp.TreatmentPlantResidues).SumAsync(r => (decimal?)r.WeightTotal ?? 0m, ct);

            var targetPct = (decimal)(targets.FirstOrDefault(t => t.Category == flow.Category)?.MinRecyclingPercent ?? 65);

            var recyclingPct = recyclingW / entryW * 100m;
            result.Add(new RecyclingRateByFlowDto(
                flow.FlowType ?? "",
                flow.Category ?? "",
                recyclingPct,
                valorizationW / entryW * 100m,
                reuseW        / entryW * 100m,
                targetPct,
                recyclingPct < targetPct));
        }
        return result;
    }
}

/// <summary>
/// Opciones de filtro para los dashboards CN — valores únicos de CCAA, Flujo y Categoría.
/// </summary>
public sealed record GetComplianceFilterOptionsQuery : IRequest<ComplianceFilterOptionsDto>;

public sealed record ComplianceFilterOptionsDto(
    IReadOnlyList<string> AutonomousCommunities,
    IReadOnlyList<string> FlowTypes,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> WasteStreams
);

public sealed class GetComplianceFilterOptionsQueryHandler
    : IRequestHandler<GetComplianceFilterOptionsQuery, ComplianceFilterOptionsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public GetComplianceFilterOptionsQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<ComplianceFilterOptionsDto> Handle(
        GetComplianceFilterOptionsQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // Una sola query a MarketShares para extraer CCAA, Flujo y Categoría
        var msRows = await _db.MarketShares.AsNoTracking()
            .Where(ms => ms.OwnerId == ownerId)
            .Select(ms => new { ms.AutonomousCommunity, ms.FlowType, ms.Category })
            .ToListAsync(ct);

        var ccaa    = msRows.Where(x => x.AutonomousCommunity != null).Select(x => x.AutonomousCommunity!).Distinct().OrderBy(x => x).ToList();
        var flows   = msRows.Where(x => x.FlowType            != null).Select(x => x.FlowType!).Distinct().OrderBy(x => x).ToList();
        var cats    = msRows.Where(x => x.Category            != null).Select(x => x.Category!).Distinct().OrderBy(x => x).ToList();

        // Una sola query a Agreements para WasteStream
        var streams = await _db.Agreements.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.WasteStream != null)
            .Select(a => a.WasteStream!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);

        return new ComplianceFilterOptionsDto(ccaa, flows, cats, streams);
    }
}

