using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.DumZones.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.DumZones.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve la lista de zonas DUM con el nº de reglas activas.
/// Las zonas son globales (sin filtro OwnerId).
/// </summary>
public sealed record GetDumZonesQuery(
    string? Status   = null,
    string? ZoneCode = null
) : IRequest<IReadOnlyList<DumZoneDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetDumZonesQueryHandler
    : IRequestHandler<GetDumZonesQuery, IReadOnlyList<DumZoneDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDumZonesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<DumZoneDto>> Handle(
        GetDumZonesQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var query = _context.DumZones.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(z => z.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.ZoneCode))
            query = query.Where(z => z.ZoneCode.Contains(request.ZoneCode));

        var zones = await query
            .OrderBy(z => z.ZoneCode)
            .Select(z => new
            {
                z.Id,
                z.ZoneCode,
                z.Name,
                z.Description,
                z.Status,
                ActiveRules = z.DumRestrictionRules
                    .Where(r => r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo >= now))
                    .ToList()
            })
            .ToListAsync(ct);

        // Orden de restricción: Block > Restrict > Notify > Allow
        static int ActionOrder(string a) => a switch
        {
            "Block"    => 4,
            "Restrict" => 3,
            "Notify"   => 2,
            _          => 1
        };

        return zones.Select(z =>
        {
            var mostRestrictive = z.ActiveRules
                .OrderByDescending(r => ActionOrder(r.ActionType))
                .Select(r => r.ActionType)
                .FirstOrDefault();

            return new DumZoneDto(
                z.Id,
                z.ZoneCode,
                z.Name,
                z.Description,
                z.Status,
                z.ActiveRules.Count,
                mostRestrictive
            );
        }).ToList();
    }
}
