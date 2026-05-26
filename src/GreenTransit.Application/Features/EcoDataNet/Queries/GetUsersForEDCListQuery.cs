using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve una lista paginada de usuarios del tenant para que el ADMIN
/// gestione la configuración de conector EDC de cada uno.
/// Solo ADMIN puede ejecutar esta query.
/// </summary>
public sealed record GetUsersForEDCListQuery(
    string? SearchTerm = null,
    int     PageNumber = 1,
    int     PageSize   = 15
) : IRequest<PaginatedResult<UserForEDCListDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetUsersForEDCListQueryHandler
    : IRequestHandler<GetUsersForEDCListQuery, PaginatedResult<UserForEDCListDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetUsersForEDCListQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<UserForEDCListDto>> Handle(
        GetUsersForEDCListQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            return PaginatedResult<UserForEDCListDto>.Create([], 0, request.PageNumber, request.PageSize);

        var ownerId = _currentUser.OwnerId;

        var query = _context.AppUsers
            .AsNoTracking()
            .Where(u => u.OwnerId == ownerId && u.IsActive);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(u =>
                (u.CompleteName != null && u.CompleteName.ToLower().Contains(term)) ||
                u.Login.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);

        var connectorUserIds = await _context.UserEDCConnectors
            .Select(c => c.UserId)
            .ToHashSetAsync(ct);

        var items = await query
            .OrderBy(u => u.CompleteName ?? u.Login)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserForEDCListDto
            {
                Id               = u.Id,
                CompleteName     = u.CompleteName ?? u.Login,
                Login            = u.Login,
                ProfileReference = u.Profile.Reference,
                HasEDCConnector  = connectorUserIds.Contains(u.Id)
            })
            .ToListAsync(ct);

        return PaginatedResult<UserForEDCListDto>.Create(items, total, request.PageNumber, request.PageSize);
    }
}
