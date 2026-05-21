using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.Entities.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Entities.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
/// <summary>
/// Lista paginada de entidades del ecosistema.
/// BusinessEntity no tiene OwnerId propio — se muestran todas las entidades
/// accesibles para el usuario autenticado (catálogo compartido del ecosistema).
/// </summary>
public sealed record GetEntitiesQuery(
    string? EntityRole   = null,
    string? ProvinceCode = null,
    bool?   IsActive     = null,
    string? SearchTerm   = null,
    int     PageNumber   = 1,
    int     PageSize     = 20
) : IRequest<PaginatedResult<EntityDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class GetEntitiesQueryHandler
    : IRequestHandler<GetEntitiesQuery, PaginatedResult<EntityDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEntitiesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<EntityDto>> Handle(
        GetEntitiesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.BusinessEntities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EntityRole))
            query = query.Where(e => e.EntityRole == request.EntityRole);

        if (!string.IsNullOrWhiteSpace(request.ProvinceCode))
            query = query.Where(e => e.ProvinceCode == request.ProvinceCode);

        if (request.IsActive.HasValue)
            query = query.Where(e => e.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var pattern = $"%{request.SearchTerm.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.Name, pattern)
                || (e.NationalId  != null && EF.Functions.Like(e.NationalId,  pattern))
                || (e.CenterCode  != null && EF.Functions.Like(e.CenterCode,  pattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Left join con AppUsers para obtener el usuario vinculado — evita subconsulta correlacionada por fila
        var items = await query
            .OrderBy(e => e.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .GroupJoin(
                _context.AppUsers.IgnoreQueryFilters(),
                e => e.Email,
                u => u.Email,
                (e, users) => new
                {
                    e.Id, e.Name, e.NationalId, e.CenterCode,
                    e.EntityRole, e.ProvinceCode, e.IsActive,
                    LinkedUserLogin = users.Select(u => u.Login).FirstOrDefault()
                })
            .ToListAsync(cancellationToken);

        var dtos = items.Select(e => new EntityDto(
            e.Id, e.Name, e.NationalId, e.CenterCode,
            e.EntityRole, e.ProvinceCode, e.IsActive, e.LinkedUserLogin
        )).ToList();

        return PaginatedResult<EntityDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
