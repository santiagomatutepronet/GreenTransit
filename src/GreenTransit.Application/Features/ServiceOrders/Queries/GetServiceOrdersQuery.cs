using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ServiceOrders.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
/// <summary>
/// Devuelve todas las ServiceOrders visibles para el tenant activo.
/// El filtro multi-tenant se aplica automáticamente a través de
/// HasQueryFilter en AppDbContext (basado en ICurrentUserService.OwnerId).
/// </summary>
public sealed record GetServiceOrdersQuery : IRequest<IReadOnlyList<ServiceOrder>>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class GetServiceOrdersQueryHandler
    : IRequestHandler<GetServiceOrdersQuery, IReadOnlyList<ServiceOrder>>
{
    private readonly IApplicationDbContext _context;

    public GetServiceOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceOrder>> Handle(
        GetServiceOrdersQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .OrderByDescending(so => so.IssuedAt)
            .ToListAsync(cancellationToken);
    }
}
