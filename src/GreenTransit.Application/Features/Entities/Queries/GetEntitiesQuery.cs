using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.Entities.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            var term = request.SearchTerm.ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(term)
                || (e.NationalId  != null && e.NationalId.ToLower().Contains(term))
                || (e.CenterCode  != null && e.CenterCode.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Left join con AppUsers para obtener el usuario vinculado (por Email = Login)
        var items = await query
            .OrderBy(e => e.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new
            {
                e.Id, e.Name, e.NationalId, e.CenterCode,
                e.EntityRole, e.ProvinceCode, e.IsActive,
                LinkedUserLogin = _context.AppUsers
                    .IgnoreQueryFilters()
                    .Where(u => u.Email == e.Email || u.Login == e.Email)
                    .Select(u => u.Login)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var dtos = items.Select(e => new EntityDto(
            e.Id, e.Name, e.NationalId, e.CenterCode,
            e.EntityRole, e.ProvinceCode, e.IsActive, e.LinkedUserLogin
        )).ToList();

        return PaginatedResult<EntityDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
