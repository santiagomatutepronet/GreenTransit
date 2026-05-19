using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.EntryPlants.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EntryPlants.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve una página de Entradas en Planta filtradas por el OwnerId del usuario activo.
/// </summary>
public sealed record GetEntryPlantsQuery(
    string?   WasteMoveReference   = null,
    DateTime? PlantEntryDateFrom   = null,
    DateTime? PlantEntryDateTo     = null,
    string?   WeighbridgeId        = null,
    int       PageNumber           = 1,
    int       PageSize             = 15
) : IRequest<PaginatedResult<EntryPlantDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetEntryPlantsQueryHandler
    : IRequestHandler<GetEntryPlantsQuery, PaginatedResult<EntryPlantDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEntryPlantsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<EntryPlantDto>> Handle(
        GetEntryPlantsQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;

        var query = _context.EntryPlants
            .AsNoTracking()
            .Where(e => ownerId == Guid.Empty || e.OwnerId == ownerId);

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.PlantOp))
        {
            // PLANT_OP: solo entradas de su planta (traslados con IdDestination = su entidad)
            var wmIds = _context.WasteMoves
                .Where(wm => wm.IdDestination == linkedEntityId && (ownerId == Guid.Empty || wm.OwnerId == ownerId))
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

        if (request.PlantEntryDateFrom.HasValue)
            query = query.Where(e => e.PlantEntryDate >= request.PlantEntryDateFrom);

        if (request.PlantEntryDateTo.HasValue)
            query = query.Where(e => e.PlantEntryDate <= request.PlantEntryDateTo);

        if (!string.IsNullOrWhiteSpace(request.WeighbridgeId))
            query = query.Where(e => e.WeighbridgeId == request.WeighbridgeId);

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderByDescending(e => e.PlantEntryDate)
            .ThenByDescending(e => e.DateCreateSys)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EntryPlantDto(
                e.Id,
                e.IdWasteMove,
                e.WasteMoveReference,
                e.TicketScale,
                e.WeighbridgeId,
                e.PlantEntryDate,
                e.GrossWeight,
                e.TareWeight,
                e.NetWeight,
                e.TypeContainer,
                e.EntryPlantResidues.Count))
            .ToListAsync(ct);

        return PaginatedResult<EntryPlantDto>.Create(items, total, request.PageNumber, pageSize);
    }
}
