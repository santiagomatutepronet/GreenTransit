using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.Residues.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Residues.Queries;

/// <summary>
/// Listado paginado de residuos/productos. Filtra por OwnerId implícito
/// (los residuos tienen IdUser y son del tenant del usuario autenticado).
/// NOTA: Residue no implementa ITenantEntity todavía — el filtro de tenant
/// global del DbContext no aplica. Se filtra explícitamente por IdProducer
/// hasta que se añada OwnerId a la entidad y su migración correspondiente.
/// </summary>
public sealed record GetResiduesQuery(
    string? ResidueType = null,
    Guid?   IdLERCode   = null,
    bool?   IsDangerous = null,
    bool?   IsRAEE      = null,
    Guid?   IdProducer  = null,
    string? SearchTerm  = null,
    int     PageNumber  = 1,
    int     PageSize    = 15
) : IRequest<PaginatedResult<ResidueDto>>;

public sealed class GetResiduesQueryHandler
    : IRequestHandler<GetResiduesQuery, PaginatedResult<ResidueDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetResiduesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ResidueDto>> Handle(
        GetResiduesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Residues.AsNoTracking();

        // ── Filtro por perfil ──────────────────────────────────────────────────
        // PRODUCER: sólo ve sus propios residuos (IdProducer == su LinkedEntityId).
        // Resto de perfiles autenticados: ven el catálogo completo (sin filtro por productor).
        if (_currentUser.IsAuthenticated &&
            _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Producer) &&
            _currentUser.LinkedEntityId.HasValue)
        {
            query = query.Where(r => r.IdProducer == null ||
                                     r.IdProducer == _currentUser.LinkedEntityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ResidueType))
            query = query.Where(r => r.ResidueType == request.ResidueType);

        if (request.IdLERCode.HasValue)
            query = query.Where(r => r.IdLERCode == request.IdLERCode.Value);

        if (request.IsDangerous.HasValue)
            query = query.Where(r => r.IsDangerous == request.IsDangerous.Value);

        if (request.IsRAEE.HasValue)
            query = query.Where(r => r.IsRAEE == request.IsRAEE.Value);

        if (request.IdProducer.HasValue)
            query = query.Where(r => r.IdProducer == request.IdProducer.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(term) ||
                (r.Reference != null && r.Reference.ToLower().Contains(term)) ||
                (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderBy(r => r.ResidueType)
            .ThenBy(r => r.Name)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ResidueDto(
                r.Id,
                r.ResidueType,
                r.Name,
                r.Reference,
                r.LerCode != null ? r.LerCode.Code : null,
                r.LerCode != null ? r.LerCode.Description : null,
                r.IsDangerous,
                r.IsRAEE,
                r.Producer != null ? r.Producer.Name : null,
                r.IsActive))
            .ToListAsync(cancellationToken);

        return PaginatedResult<ResidueDto>.Create(items, totalCount, request.PageNumber, pageSize);
    }
}
