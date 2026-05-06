using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.EntryCACs.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EntryCACs.Queries;

// ── Parámetros de paginación y filtros ────────────────────────────────────────

/// <summary>
/// Devuelve una página de Entradas en CAC filtradas por el OwnerId del usuario activo.
/// </summary>
public sealed record GetEntryCACsQuery(
    string?   WasteMoveReference = null,
    DateTime? CACEntryDateFrom   = null,
    DateTime? CACEntryDateTo     = null,
    int       PageNumber         = 1,
    int       PageSize           = 15
) : IRequest<PaginatedResult<EntryCACDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetEntryCACsQueryHandler
    : IRequestHandler<GetEntryCACsQuery, PaginatedResult<EntryCACDto>>
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

    public async Task<PaginatedResult<EntryCACDto>> Handle(
        GetEntryCACsQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var isScrap        = _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap);
        var linkedEntityId = _currentUser.LinkedEntityId;

        // Si OwnerId es Guid.Empty (auth pendiente / desarrollo), se muestran todos los registros.
        var query = _context.EntryCACs
            .AsNoTracking()
            .Where(e => ownerId == Guid.Empty || e.OwnerId == ownerId);

        // SCRAP: solo entradas en CAC cuyo traslado lo tiene asignado
        if (isScrap && linkedEntityId.HasValue)
            query = query.Where(e =>
                e.WasteMove.IdScrap == linkedEntityId.Value ||
                e.WasteMove.IdScrap2 == linkedEntityId.Value);

        if (!string.IsNullOrWhiteSpace(request.WasteMoveReference))
            query = query.Where(e =>
                e.WasteMoveReference!.Contains(request.WasteMoveReference));

        if (request.CACEntryDateFrom.HasValue)
            query = query.Where(e => e.CACEntryDate >= request.CACEntryDateFrom);

        if (request.CACEntryDateTo.HasValue)
            query = query.Where(e => e.CACEntryDate <= request.CACEntryDateTo);

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderByDescending(e => e.CACEntryDate)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
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

        return PaginatedResult<EntryCACDto>.Create(items, total, request.PageNumber, pageSize);
    }
}
