using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.TreatmentPlants.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        var isScrap        = _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap);
        var linkedEntityId = _currentUser.LinkedEntityId;

        var query = _context.TreatmentPlants
            .AsNoTracking()
            .Where(t => ownerId == Guid.Empty || t.OwnerId == ownerId);

        // SCRAP: solo tratamientos cuyo traslado lo tiene asignado
        if (isScrap && linkedEntityId.HasValue)
            query = query.Where(t =>
                t.WasteMove != null &&
                (t.WasteMove.IdScrap == linkedEntityId.Value ||
                 t.WasteMove.IdScrap2 == linkedEntityId.Value));

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
