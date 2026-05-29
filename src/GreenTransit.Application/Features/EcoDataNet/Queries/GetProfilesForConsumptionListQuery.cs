using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve todos los perfiles del sistema para la vista ADMIN de consumo de datos.
/// Solo ADMIN puede ejecutar esta query.
/// </summary>
public sealed record GetProfilesForConsumptionListQuery
    : IRequest<List<ProfileEDCConsumerDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetProfilesForConsumptionListQueryHandler
    : IRequestHandler<GetProfilesForConsumptionListQuery, List<ProfileEDCConsumerDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetProfilesForConsumptionListQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<List<ProfileEDCConsumerDto>> Handle(
        GetProfilesForConsumptionListQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            return [];

        return await _context.UserProfiles
            .AsNoTracking()
            .OrderBy(p => p.Reference)
            .Select(p => new ProfileEDCConsumerDto
            {
                ProfileId          = p.Id,
                ProfileReference   = p.Reference,
                ProfileDescription = p.Description ?? string.Empty
            })
            .ToListAsync(ct);
    }
}
