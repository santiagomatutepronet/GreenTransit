using GreenTransit.Application.Common.Behaviours;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.Security.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Security.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
/// <summary>Lista paginada de usuarios del tenant. Solo perfil ADMIN.</summary>
[Authorize(Profiles = ProfileConstants.Admin)]
public sealed record GetUsersQuery(
    int?    IdProfile  = null,
    bool?   IsActive   = null,
    string? SearchTerm = null,
    int     PageNumber = 1,
    int     PageSize   = 15
) : IRequest<PaginatedResult<UserDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class GetUsersQueryHandler
    : IRequestHandler<GetUsersQuery, PaginatedResult<UserDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetUsersQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<UserDto>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.OwnerId;

        // Si OwnerId es Guid.Empty el admin no tiene tenant asignado en BD (OwnerId = NULL).
        // En ese caso se muestran los usuarios que también tienen OwnerId = NULL.
        // Con tenant real se filtra por el GUID coincidente.
        var query = _context.AppUsers
            .AsNoTracking()
            .IgnoreQueryFilters();

        query = ownerId == Guid.Empty
            ? query.Where(u => u.OwnerId == null)
            : query.Where(u => u.OwnerId == ownerId);

        if (request.IdProfile.HasValue)
            query = query.Where(u => u.IdProfile == request.IdProfile.Value);

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(u =>
                u.Login.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderBy(u => u.Login)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(
                u.Id,
                u.Login,
                u.Email,
                u.CompleteName,
                u.IdProfile,
                u.Profile.Reference,
                u.IsActive,
                u.OwnerId
            ))
            .ToListAsync(cancellationToken);

        return PaginatedResult<UserDto>.Create(items, totalCount, request.PageNumber, pageSize);
    }
}
