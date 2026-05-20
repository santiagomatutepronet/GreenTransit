using GreenTransit.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Logistics.Queries;

/// <summary>
/// Devuelve la lista de SCRAPs vinculados a los acuerdos de un coordinador dado.
/// Usado para poblar el selector de filtro en el panel de optimización logística.
/// </summary>
public sealed record GetCoordinatorScrapsQuery(
    Guid CoordinatorId,
    Guid OwnerId
) : IRequest<List<CoordinatorScrapDto>>;

public sealed record CoordinatorScrapDto(Guid Id, string Name);

public sealed class GetCoordinatorScrapsQueryHandler
    : IRequestHandler<GetCoordinatorScrapsQuery, List<CoordinatorScrapDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCoordinatorScrapsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<CoordinatorScrapDto>> Handle(
        GetCoordinatorScrapsQuery request, CancellationToken ct)
    {
        var result = await _context.Agreements
            .AsNoTracking()
            .Where(a => a.IdCoordinator == request.CoordinatorId
                     && (request.OwnerId == Guid.Empty || a.OwnerId == request.OwnerId)
                     && a.IdScrap != null)
            .Join(_context.BusinessEntities,
                a => a.IdScrap,
                e => e.Id,
                (a, e) => new { Id = a.IdScrap!.Value, e.Name })
            .Distinct()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        return result
            .Select(x => new CoordinatorScrapDto(x.Id, x.Name ?? x.Id.ToString()))
            .ToList();
    }
}
