using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.WasteMoves.Queries;

// ── GetWasteMovesByServiceOrderQuery ─────────────────────────────────────────

public sealed record GetWasteMovesByServiceOrderQuery(Guid ServiceOrderId)
    : IRequest<IReadOnlyList<WasteMoveDto>>;

public sealed class GetWasteMovesByServiceOrderQueryHandler
    : IRequestHandler<GetWasteMovesByServiceOrderQuery, IReadOnlyList<WasteMoveDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWasteMovesByServiceOrderQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<WasteMoveDto>> Handle(
        GetWasteMovesByServiceOrderQuery request, CancellationToken ct)
    {
        return await _context.WasteMoves
            .AsNoTracking()
            .Where(w => w.ServiceOrderId == request.ServiceOrderId)
            .Select(w => new WasteMoveDto(
                w.Id,
                w.WasteMoveReference,
                w.ServiceStatus,
                w.IdSource,
                w.Source != null ? w.Source.Name : null,
                w.IdDestination,
                w.Destination != null ? w.Destination.Name : null,
                w.PlannedPickupStart,
                w.RequestDate,
                w.WasteMoveResidues.Count
            ))
            .ToListAsync(ct);
    }
}
