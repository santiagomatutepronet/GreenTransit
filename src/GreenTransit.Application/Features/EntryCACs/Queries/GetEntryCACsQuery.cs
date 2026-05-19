using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.EntryCACs.DTOs;
using GreenTransit.Domain.Authorization;
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
        var linkedEntityId = _currentUser.LinkedEntityId;

        // Si OwnerId es Guid.Empty (auth pendiente / desarrollo), se muestran todos los registros.
        var query = _context.EntryCACs
            .AsNoTracking()
            .Where(e => ownerId == Guid.Empty || e.OwnerId == ownerId);

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.CacOp))
        {
            // CAC_OP: solo entradas de su CAC (traslados con destino u origen = su entidad)
            var wmIds = _context.WasteMoves
                .Where(wm => (wm.IdDestination == linkedEntityId || wm.IdSource == linkedEntityId)
                          && (ownerId == Guid.Empty || wm.OwnerId == ownerId))
                .Select(wm => wm.Id);
            query = query.Where(e => wmIds.Contains(e.IdWasteMove));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: solo entradas cuyo traslado lo tiene asignado
            query = query.Where(e =>
                e.WasteMove.IdScrap == linkedEntityId ||
                e.WasteMove.IdScrap2 == linkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            // PUBLIC_ENT: entradas de traslados de SOs emitidas por su entidad
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            var wmIds = _context.WasteMoves
                .Where(wm => wm.ServiceOrderId != null && soIds.Contains(wm.ServiceOrderId.Value))
                .Select(wm => wm.Id);
            query = query.Where(e => wmIds.Contains(e.IdWasteMove));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: entradas de traslados de SCRAPs de sus acuerdos
            var scrapIds = _context.Agreements
                .Where(a => a.IdCoordinator == linkedEntityId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.IdScrap);
            var wmIds = _context.WasteMoves
                .Where(wm => scrapIds.Contains(wm.IdScrap) && (ownerId == Guid.Empty || wm.OwnerId == ownerId))
                .Select(wm => wm.Id);
            query = query.Where(e => wmIds.Contains(e.IdWasteMove));
        }
        // DISPATCH_OFFICE / ADMIN: sin filtro adicional

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
