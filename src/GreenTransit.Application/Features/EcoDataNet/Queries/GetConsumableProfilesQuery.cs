using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve los perfiles que el perfil indicado puede consumir en el dataspace EDC.
/// NO ADMIN: solo puede consultar su propio perfil.
/// ADMIN: puede consultar cualquier perfil.
/// </summary>
public sealed record GetConsumableProfilesQuery(int ProfileId)
    : IRequest<List<ProfileEDCConsumerDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetConsumableProfilesQueryHandler
    : IRequestHandler<GetConsumableProfilesQuery, List<ProfileEDCConsumerDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetConsumableProfilesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<List<ProfileEDCConsumerDto>> Handle(
        GetConsumableProfilesQuery request, CancellationToken ct)
    {
        // NO ADMIN solo puede consultar su propio perfil
        if (!_currentUser.IsInProfile(ProfileConstants.Admin)
            && request.ProfileId != _currentUser.ProfileId)
            return [];

        return await _context.ProfileEDCConsumers
            .AsNoTracking()
            .Where(pc => pc.ProfileId == request.ProfileId)
            .Select(pc => new ProfileEDCConsumerDto
            {
                ProfileId          = pc.ConsumedProfileId,
                ProfileReference   = pc.ConsumedProfile.Reference,
                ProfileDescription = pc.ConsumedProfile.Description ?? string.Empty
            })
            .OrderBy(p => p.ProfileReference)
            .ToListAsync(ct);
    }
}
