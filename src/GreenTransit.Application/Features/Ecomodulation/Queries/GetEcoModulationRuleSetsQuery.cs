using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

// ── GetEcoModulationRuleSetsQuery ─────────────────────────────────────────────

/// <summary>Devuelve los conjuntos de reglas de ecomodulación paginados.</summary>
public sealed record GetEcoModulationRuleSetsQuery(
    string? Status     = null,
    int     PageNumber = 1,
    int     PageSize   = 15
) : IRequest<PaginatedResult<EcoModulationRuleSetDto>>;

public sealed class GetEcoModulationRuleSetsQueryHandler
    : IRequestHandler<GetEcoModulationRuleSetsQuery, PaginatedResult<EcoModulationRuleSetDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEcoModulationRuleSetsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<EcoModulationRuleSetDto>> Handle(
        GetEcoModulationRuleSetsQuery request, CancellationToken ct)
    {
        var q = _context.EcoModulationRuleSets
            .AsNoTracking()
            .Where(rs => rs.OwnerId == _currentUser.OwnerId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(rs => rs.Status == request.Status);

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await q
            .OrderByDescending(rs => rs.ValidFrom)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(rs => new EcoModulationRuleSetDto(
                rs.Id,
                rs.RuleSetName,
                rs.Version,
                rs.Status,
                rs.ValidFrom,
                rs.ValidTo,
                rs.PublisherName,
                rs.PublisherNationalId,
                rs.PublisherCenterCode,
                rs.EcoModulationRules.Count,
                rs.CreatedAt))
            .ToListAsync(ct);

        return PaginatedResult<EcoModulationRuleSetDto>.Create(items, total, request.PageNumber, pageSize);
    }
}

// ── GetEcoModulationRuleSetByIdQuery ──────────────────────────────────────────

/// <summary>Devuelve el detalle completo de un conjunto de reglas (cabecera + reglas).</summary>
public sealed record GetEcoModulationRuleSetByIdQuery(Guid Id)
    : IRequest<EcoModulationRuleSetDetailDto?>;

public sealed class GetEcoModulationRuleSetByIdQueryHandler
    : IRequestHandler<GetEcoModulationRuleSetByIdQuery, EcoModulationRuleSetDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEcoModulationRuleSetByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<EcoModulationRuleSetDetailDto?> Handle(
        GetEcoModulationRuleSetByIdQuery request, CancellationToken ct)
    {
        var rs = await _context.EcoModulationRuleSets
            .AsNoTracking()
            .Include(x => x.EcoModulationRules)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.OwnerId == _currentUser.OwnerId, ct);

        if (rs is null) return null;

        return new EcoModulationRuleSetDetailDto(
            rs.Id,
            rs.RuleSetName,
            rs.Version,
            rs.Status,
            rs.ValidFrom,
            rs.ValidTo,
            rs.PublisherName,
            rs.PublisherNationalId,
            rs.PublisherCenterCode,
            rs.CreatedAt,
            rs.EcoModulationRules
              .OrderBy(r => r.RuleCode)
              .Select(r => new EcoModulationRuleDto(
                  r.Id,
                  r.RuleCode,
                  r.ProductCategory,
                  r.CriteriaJson,
                  r.FeeImpactType,
                  r.FeeImpactValue))
              .ToList());
    }
}
