using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EntryCACs.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EntryCACs.Queries;

// ── Parámetros de paginación y filtros ────────────────────────────────────────

/// <summary>
/// Devuelve una página de Entradas en CAC filtradas por el OwnerId del usuario activo.
/// </summary>
public sealed record GetEntryCACsQuery(
    string?   WasteMoveReference,
    DateTime? CACEntryDateFrom,
    DateTime? CACEntryDateTo,
    int       Page     = 1,
    int       PageSize = 20
) : IRequest<GetEntryCACsResult>;

public sealed record GetEntryCACsResult(
    IReadOnlyList<EntryCACDto> Items,
    int                        TotalCount,
    int                        Page,
    int                        PageSize
);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetEntryCACsQueryHandler
    : IRequestHandler<GetEntryCACsQuery, GetEntryCACsResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEntryCACsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<GetEntryCACsResult> Handle(
        GetEntryCACsQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // Si OwnerId es Guid.Empty (auth pendiente / desarrollo), se muestran todos los registros.
        var query = _context.EntryCACs
            .AsNoTracking()
            .Where(e => ownerId == Guid.Empty || e.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(request.WasteMoveReference))
            query = query.Where(e =>
                e.WasteMoveReference!.Contains(request.WasteMoveReference));

        if (request.CACEntryDateFrom.HasValue)
            query = query.Where(e => e.CACEntryDate >= request.CACEntryDateFrom);

        if (request.CACEntryDateTo.HasValue)
            query = query.Where(e => e.CACEntryDate <= request.CACEntryDateTo);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CACEntryDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new EntryCACDto(
                e.Id,
                e.IdWasteMove,
                e.WasteMoveReference,
                e.CACEntryDate,
                e.TypeContainer,
                e.PriceContainer,
                e.CollectionMethod,
                e.EntryCACResidues.Count))
            .ToListAsync(ct);

        return new GetEntryCACsResult(items, total, request.Page, request.PageSize);
    }
}
