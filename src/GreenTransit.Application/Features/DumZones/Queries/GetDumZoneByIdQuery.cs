using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.DumZones.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.DumZones.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>Devuelve el detalle de una zona DUM con su GeometryJson y todas sus reglas.</summary>
public sealed record GetDumZoneByIdQuery(Guid Id) : IRequest<DumZoneDetailDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetDumZoneByIdQueryHandler
    : IRequestHandler<GetDumZoneByIdQuery, DumZoneDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetDumZoneByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<DumZoneDetailDto?> Handle(
        GetDumZoneByIdQuery request, CancellationToken ct)
    {
        var zone = await _context.DumZones
            .AsNoTracking()
            .Include(z => z.DumRestrictionRules.OrderBy(r => r.ValidFrom))
            .FirstOrDefaultAsync(z => z.Id == request.Id, ct);

        if (zone is null) return null;

        var rules = zone.DumRestrictionRules.Select(r => new DumRestrictionRuleDto(
            r.Id,
            r.RuleCode,
            r.Status,
            r.ValidFrom,
            r.ValidTo,
            r.ConditionsJson,
            r.ActionType,
            r.ActionReason
        )).ToList();

        return new DumZoneDetailDto(
            zone.Id,
            zone.ZoneCode,
            zone.Name,
            zone.Description,
            zone.Status,
            zone.GeometryJson,
            zone.Version,
            zone.CreatedAt,
            zone.UpdatedAt,
            rules
        );
    }
}
