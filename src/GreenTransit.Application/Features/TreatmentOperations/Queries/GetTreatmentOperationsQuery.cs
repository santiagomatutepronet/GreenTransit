using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
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

// ── GetTreatmentOperationsPagedQuery — paginada para la página de gestión ────

/// <summary>
/// Versión paginada con filtrado en servidor para la página de listado.
/// Mantiene <see cref="GetTreatmentOperationsQuery"/> intacta para selectores.
/// </summary>
public sealed record GetTreatmentOperationsPagedQuery(
    string? OperationType = null,
    string? SearchTerm    = null,
    int     PageNumber    = 1,
    int     PageSize      = 15
) : IRequest<PaginatedResult<TreatmentOperationDto>>;

public sealed class GetTreatmentOperationsPagedQueryHandler
    : IRequestHandler<GetTreatmentOperationsPagedQuery, PaginatedResult<TreatmentOperationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTreatmentOperationsPagedQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<PaginatedResult<TreatmentOperationDto>> Handle(
        GetTreatmentOperationsPagedQuery request, CancellationToken ct)
    {
        var query = _context.TreatmentOperations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.OperationType))
            query = query.Where(t => t.OperationType == request.OperationType);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(t =>
                t.Code.ToLower().Contains(term) ||
                t.Description.ToLower().Contains(term) ||
                (t.ShortDescription != null && t.ShortDescription.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderBy(t => t.OperationType)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Code)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TreatmentOperationDto(
                t.Id, t.Code, t.OperationType, t.Description,
                t.ShortDescription, t.IsRecycling, t.IsEnergyRecovery,
                t.IsPreparationForReuse, t.SortOrder, t.IsActive))
            .ToListAsync(ct);

        return PaginatedResult<TreatmentOperationDto>.Create(items, total, request.PageNumber, pageSize);
    }
}
