using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve los perfiles que un usuario concreto puede consumir,
/// basándose en el perfil al que pertenece dicho usuario.
/// Solo ADMIN puede consultar usuarios distintos al suyo propio.
/// </summary>
public sealed record GetConsumableProfilesForUserQuery(int UserId)
    : IRequest<List<ProfileEDCConsumerDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetConsumableProfilesForUserQueryHandler
    : IRequestHandler<GetConsumableProfilesForUserQuery, List<ProfileEDCConsumerDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetConsumableProfilesForUserQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<List<ProfileEDCConsumerDto>> Handle(
        GetConsumableProfilesForUserQuery request, CancellationToken ct)
    {
        // No-ADMIN solo puede consultar su propio usuario
        if (!_currentUser.IsInProfile(ProfileConstants.Admin)
            && request.UserId != _currentUser.IdUser)
            return [];

        // Obtener el perfil del usuario solicitado
        var profileId = await _context.AppUsers
            .AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => (int?)u.IdProfile)
            .FirstOrDefaultAsync(ct);

        if (profileId is null)
            return [];

        return await _context.ProfileEDCConsumers
            .AsNoTracking()
            .Where(pc => pc.ProfileId == profileId)
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
