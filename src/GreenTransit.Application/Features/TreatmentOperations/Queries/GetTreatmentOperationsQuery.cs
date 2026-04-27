using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.TreatmentOperations.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.TreatmentOperations.Queries;

/// <summary>
/// Devuelve la lista de operaciones de tratamiento R/D.
/// Catálogo global: sin filtro OwnerId.
/// </summary>
public sealed record GetTreatmentOperationsQuery(
    string? OperationType = null
) : IRequest<List<TreatmentOperationDto>>;

public sealed class GetTreatmentOperationsQueryHandler
    : IRequestHandler<GetTreatmentOperationsQuery, List<TreatmentOperationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTreatmentOperationsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<TreatmentOperationDto>> Handle(
        GetTreatmentOperationsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.TreatmentOperations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.OperationType))
            query = query.Where(t => t.OperationType == request.OperationType);

        return await query
            .OrderBy(t => t.OperationType)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Code)
            .Select(t => new TreatmentOperationDto(
                t.Id, t.Code, t.OperationType, t.Description,
                t.ShortDescription, t.IsRecycling, t.IsEnergyRecovery,
                t.IsPreparationForReuse, t.SortOrder, t.IsActive))
            .ToListAsync(cancellationToken);
    }
}
