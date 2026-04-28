using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Entities.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Entities.Queries;

/// <summary>
/// Devuelve la lista completa de entidades activas para uso en selectores de formularios.
/// Opcionalmente filtrada por uno o varios roles.
/// </summary>
/// <summary>
/// Devuelve entidades activas para usar en selectores de formularios.
/// Si Roles está vacío o es null, devuelve todas las entidades sin filtrar por rol.
/// </summary>
public sealed record GetEntitiesForSelectorQuery(string[]? Roles = null)
    : IRequest<IReadOnlyList<EntitySelectorDto>>;

public sealed class GetEntitiesForSelectorQueryHandler
    : IRequestHandler<GetEntitiesForSelectorQuery, IReadOnlyList<EntitySelectorDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEntitiesForSelectorQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<EntitySelectorDto>> Handle(
        GetEntitiesForSelectorQuery request, CancellationToken ct)
    {
        var q = _context.BusinessEntities
            .AsNoTracking()
            .Where(e => e.IsActive);

        if (request.Roles is { Length: > 0 })
            q = q.Where(e => request.Roles.Contains(e.EntityRole));

        return await q
            .OrderBy(e => e.EntityRole)
            .ThenBy(e => e.Name)
            .Select(e => new EntitySelectorDto(e.Id, e.Name, e.EntityRole, e.NationalId))
            .ToListAsync(ct);
    }
}
