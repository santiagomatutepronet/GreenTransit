using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.MarketShares.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.MarketShares.Queries;

// ── Listado de cuotas ─────────────────────────────────────────────────────────

/// <summary>Devuelve las cuotas de mercado paginadas y filtradas.</summary>
public sealed record GetMarketSharesQuery(
    Guid?   IdScrap            = null,
    string? Category           = null,
    string? AutonomousCommunity = null,
    int?    Year               = null,
    int     PageNumber         = 1,
    int     PageSize           = 20
) : IRequest<PaginatedResult<MarketShareDto>>;

public sealed class GetMarketSharesQueryHandler
    : IRequestHandler<GetMarketSharesQuery, PaginatedResult<MarketShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetMarketSharesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<MarketShareDto>> Handle(GetMarketSharesQuery request, CancellationToken ct)
    {
        var q = _context.MarketShares
            .AsNoTracking()
            .Include(ms => ms.Scrap)
            .Where(ms => ms.OwnerId == _currentUser.OwnerId)
            .AsQueryable();

        // SCRAP: solo sus propias cuotas
        if (_currentUser.IsInProfile(ProfileConstants.Scrap) && _currentUser.LinkedEntityId.HasValue)
            q = q.Where(ms => ms.IdScrap == _currentUser.LinkedEntityId.Value);
        else if (request.IdScrap.HasValue)
            q = q.Where(ms => ms.IdScrap == request.IdScrap.Value);

        if (!string.IsNullOrWhiteSpace(request.Category))
            q = q.Where(ms => ms.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.AutonomousCommunity))
            q = q.Where(ms => ms.AutonomousCommunity == request.AutonomousCommunity);

        if (request.Year.HasValue)
            q = q.Where(ms => ms.Year == request.Year.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(ms => ms.Year)
            .ThenBy(ms => ms.Category)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ms => new MarketShareDto(
                ms.Id,
                ms.IdScrap,
                ms.Scrap != null ? ms.Scrap.Name : null,
                ms.Category,
                ms.AutonomousCommunity,
                ms.Year,
                ms.Weight,
                ms.Period,
                ms.FlowType,
                ms.EffectiveFrom,
                ms.EffectiveTo))
            .ToListAsync(ct);

        return PaginatedResult<MarketShareDto>.Create(items, total, request.PageNumber, request.PageSize);
    }
}

// ── Cumplimiento ──────────────────────────────────────────────────────────────

/// <summary>Calcula el cumplimiento de cuotas de mercado para el año indicado.</summary>
public sealed record GetMarketShareComplianceQuery(int Year) : IRequest<IReadOnlyList<MarketShareComplianceDto>>;

public sealed class GetMarketShareComplianceQueryHandler
    : IRequestHandler<GetMarketShareComplianceQuery, IReadOnlyList<MarketShareComplianceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetMarketShareComplianceQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MarketShareComplianceDto>> Handle(
        GetMarketShareComplianceQuery request, CancellationToken ct)
    {
        var sharesQuery = _context.MarketShares
            .AsNoTracking()
            .Include(ms => ms.Scrap)
            .Where(ms => ms.OwnerId == _currentUser.OwnerId && ms.Year == request.Year);

        // SCRAP: solo las suyas
        if (_currentUser.IsInProfile(ProfileConstants.Scrap) && _currentUser.LinkedEntityId.HasValue)
            sharesQuery = sharesQuery.Where(ms => ms.IdScrap == _currentUser.LinkedEntityId.Value);

        var shares = await sharesQuery.ToListAsync(ct);

        if (shares.Count == 0)
            return [];

        // Peso real: EntryPlantResidues del año con WasteMove.IdScrap y Residue.ProductCategory
        var achievedByKey = await _context.EntryPlantResidues
            .AsNoTracking()
            .Include(epr => epr.EntryPlant)
                .ThenInclude(ep => ep.WasteMove)
            .Include(epr => epr.Residue)
            .Where(epr =>
                epr.EntryPlant.OwnerId == _currentUser.OwnerId &&
                epr.EntryPlant.PlantEntryDate.HasValue &&
                epr.EntryPlant.PlantEntryDate!.Value.Year == request.Year &&
                epr.Weight.HasValue)
            .GroupBy(epr => new
            {
                IdScrap  = epr.EntryPlant.WasteMove.IdScrap,
                Category = epr.Residue != null ? epr.Residue.ProductCategory : null,
                FlowType = epr.Residue != null ? epr.Residue.FlowType : null
            })
            .Select(g => new
            {
                g.Key.IdScrap,
                g.Key.Category,
                g.Key.FlowType,
                TotalWeight = g.Sum(x => x.Weight!.Value)
            })
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        // Factor de progreso del año en curso: meses transcurridos / 12
        double yearProgress = request.Year < now.Year ? 1.0
            : request.Year > now.Year ? 0.0
            : now.Month / 12.0;

        var result = shares.Select(ms =>
        {
            var achieved = achievedByKey
                .Where(k => k.IdScrap == ms.IdScrap
                         && k.Category == ms.Category
                         && k.FlowType == ms.FlowType)
                .Sum(k => k.TotalWeight);

            var compliancePct = ms.Weight > 0
                ? Math.Round(achieved / ms.Weight * 100, 2)
                : 0m;

            // IsAtRisk: si el progreso esperado a esta fecha sería >= 80% del objetivo
            // pero el cumplimiento real es < 80%
            var expectedAtRisk = yearProgress > 0 && compliancePct < 80m;

            return new MarketShareComplianceDto(
                ms.Id,
                ms.IdScrap,
                ms.Scrap?.Name,
                ms.Category,
                ms.AutonomousCommunity,
                ms.Year,
                ms.Period,
                ms.Weight,
                achieved,
                compliancePct,
                expectedAtRisk);
        }).ToList();

        return result;
    }
}
