using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.TreatmentPlants.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.TreatmentPlants.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve una página de tratamientos en planta filtrados por el OwnerId del usuario activo.
/// </summary>
public sealed record GetTreatmentPlantsQuery(
    string?   WasteMoveReference        = null,
    DateTime? PlantTreatmentDateFrom    = null,
    DateTime? PlantTreatmentDateTo      = null,
    Guid?     IdTreatmentOperation      = null,
    int       PageNumber                = 1,
    int       PageSize                  = 15
) : IRequest<PaginatedResult<TreatmentPlantDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetTreatmentPlantsQueryHandler
    : IRequestHandler<GetTreatmentPlantsQuery, PaginatedResult<TreatmentPlantDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetTreatmentPlantsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<TreatmentPlantDto>> Handle(
        GetTreatmentPlantsQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;

        var query = _context.TreatmentPlants
            .AsNoTracking()
            .Where(t => ownerId == Guid.Empty || t.OwnerId == ownerId);

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.PlantOp))
        {
            // PLANT_OP: solo tratamientos de su planta
            var wmIds = _context.WasteMoves
                .Where(wm => wm.IdDestination == linkedEntityId && (ownerId == Guid.Empty || wm.OwnerId == ownerId))
                .Select(wm => wm.Id);
            query = query.Where(t => t.IdWasteMove != null && wmIds.Contains(t.IdWasteMove.Value));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: tratamientos cuyo traslado lo tiene asignado
            query = query.Where(t =>
                t.WasteMove != null &&
                (t.WasteMove.IdScrap == linkedEntityId ||
                 t.WasteMove.IdScrap2 == linkedEntityId));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            // PUBLIC_ENT: tratamientos de traslados de SOs emitidas por su entidad
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            var wmIds = _context.WasteMoves
                .Where(wm => wm.ServiceOrderId != null && soIds.Contains(wm.ServiceOrderId.Value))
                .Select(wm => wm.Id);
            query = query.Where(t => t.IdWasteMove != null && wmIds.Contains(t.IdWasteMove.Value));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: tratamientos de traslados de SCRAPs de sus acuerdos
            var scrapIds = _context.Agreements
                .Where(a => a.IdCoordinator == linkedEntityId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.IdScrap);
            var wmIds = _context.WasteMoves
                .Where(wm => scrapIds.Contains(wm.IdScrap) && (ownerId == Guid.Empty || wm.OwnerId == ownerId))
                .Select(wm => wm.Id);
            query = query.Where(t => t.IdWasteMove != null && wmIds.Contains(t.IdWasteMove.Value));
        }
        // DISPATCH_OFFICE / ADMIN: sin filtro adicional

        if (!string.IsNullOrWhiteSpace(request.WasteMoveReference))
            query = query.Where(t =>
                t.WasteMoveReference!.Contains(request.WasteMoveReference));

        if (request.PlantTreatmentDateFrom.HasValue)
            query = query.Where(t => t.PlantTreatmentDate >= request.PlantTreatmentDateFrom);

        if (request.PlantTreatmentDateTo.HasValue)
            query = query.Where(t => t.PlantTreatmentDate <= request.PlantTreatmentDateTo);

        if (request.IdTreatmentOperation.HasValue)
            query = query.Where(t => t.IdTreatmentOperation == request.IdTreatmentOperation);

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderByDescending(t => t.PlantTreatmentDate)
            .ThenByDescending(t => t.DateCreateSys)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TreatmentPlantDto(
                t.Id,
                t.IdWasteMove,
                t.WasteMoveReference,
                t.TicketScale,
                t.PlantTreatmentDate,
                t.TreatmentOperation != null ? t.TreatmentOperation.Code : null,
                t.TreatmentOperation != null ? t.TreatmentOperation.ShortDescription ?? t.TreatmentOperation.Description : null,
                t.TreatmentOperation != null ? t.TreatmentOperation.OperationType : null,
                t.TreatmentPlantResidues.Count,
                t.IncidentId != null))
            .ToListAsync(ct);

        return PaginatedResult<TreatmentPlantDto>.Create(items, total, request.PageNumber, pageSize);
    }
}
