using GreenTransit.Application.Common.Behaviours;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Security.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.Security.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
/// <summary>Detalle de un usuario con geografía resuelta y entidad vinculada. Solo perfil ADMIN.</summary>
[Authorize(Profiles = ProfileConstants.Admin)]
public sealed record GetUserByIdQuery(int Id) : IRequest<UserDetailDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class GetUserByIdQueryHandler
    : IRequestHandler<GetUserByIdQuery, UserDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetUserByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<UserDetailDto?> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.OwnerId;

        var user = await _context.AppUsers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == request.Id &&
                        (ownerId == Guid.Empty ? u.OwnerId == null : u.OwnerId == ownerId))
            .Select(u => new
            {
                u.Id, u.Login, u.Email, u.CompleteName,
                u.IdProfile, ProfileReference = u.Profile.Reference,
                u.IsActive, u.OwnerId,
                u.NationalId,  CountryName   = u.Country != null ? u.Country.Ref : null,
                u.GeographicalId, StateName  = u.TerritoryState != null ? u.TerritoryState.Name : null,
                u.MunicipalityId, MunicipalityName = u.Municipality != null ? u.Municipality.Name : null,
                u.ZipCode, u.Address,
                u.PortalEDCProvider, u.PortalEDCConsumer,
                u.CreateDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null) return null;

        // Busca la BusinessEntity que tiene IdUser = este usuario
        var linkedEntity = await _context.BusinessEntities
            .AsNoTracking()
            .Where(e => e.IdUser == request.Id)
            .Select(e => new { e.Id, e.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return new UserDetailDto(
            user.Id, user.Login, user.Email, user.CompleteName,
            user.IdProfile, user.ProfileReference,
            user.IsActive, user.OwnerId,
            user.NationalId, user.CountryName,
            user.GeographicalId, user.StateName,
            user.MunicipalityId, user.MunicipalityName,
            user.ZipCode, user.Address,
            user.PortalEDCProvider, user.PortalEDCConsumer,
            user.CreateDate,
            linkedEntity?.Name, linkedEntity?.Id
        );
    }
}
