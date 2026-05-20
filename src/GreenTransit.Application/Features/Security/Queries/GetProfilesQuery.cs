using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Security.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Security.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
/// <summary>Lista todos los perfiles del sistema (catálogo compartido, sin filtro de tenant).</summary>
public sealed record GetProfilesQuery : IRequest<IReadOnlyList<ProfileDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class GetProfilesQueryHandler
    : IRequestHandler<GetProfilesQuery, IReadOnlyList<ProfileDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProfilesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProfileDto>> Handle(
        GetProfilesQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.UserProfiles
            .AsNoTracking()
            .OrderBy(p => p.Reference)
            .Select(p => new ProfileDto(p.Id, p.Reference, p.Description))
            .ToListAsync(cancellationToken);
    }
}
