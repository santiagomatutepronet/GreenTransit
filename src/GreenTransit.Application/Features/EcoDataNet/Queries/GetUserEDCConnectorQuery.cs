using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Obtiene la configuración EDC de un usuario concreto.
/// ADMIN puede consultar cualquier usuario del tenant. NO ADMIN solo puede consultar el suyo propio.
/// </summary>
public sealed record GetUserEDCConnectorQuery(int UserId) : IRequest<UserEDCConnectorDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetUserEDCConnectorQueryHandler
    : IRequestHandler<GetUserEDCConnectorQuery, UserEDCConnectorDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetUserEDCConnectorQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<UserEDCConnectorDto?> Handle(
        GetUserEDCConnectorQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // NO ADMIN solo puede consultar su propio conector
        if (!_currentUser.IsInProfile(ProfileConstants.Admin)
            && request.UserId != _currentUser.IdUser)
            return null;

        var user = await _context.AppUsers
            .AsNoTracking()
            .Where(u => u.Id == request.UserId && u.OwnerId == ownerId)
            .Select(u => new
            {
                u.Id,
                u.CompleteName,
                u.Login,
                Connector = _context.UserEDCConnectors
                    .Where(c => c.UserId == u.Id)
                    .Select(c => new { c.Id, c.EDCServerName, c.EDCConnectorId, c.ApiKey })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (user is null) return null;

        return new UserEDCConnectorDto
        {
            Id             = user.Connector?.Id ?? 0,
            UserId         = user.Id,
            UserName       = user.CompleteName ?? user.Login,
            UserLogin      = user.Login,
            EDCServerName  = user.Connector?.EDCServerName  ?? string.Empty,
            EDCConnectorId = user.Connector?.EDCConnectorId ?? string.Empty,
            ApiKey         = user.Connector?.ApiKey
        };
    }
}
