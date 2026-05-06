using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.EntryPlants.DTOs;
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
        var isScrap        = _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap);
        var linkedEntityId = _currentUser.LinkedEntityId;

        var query = _context.EntryPlants
            .AsNoTracking()
            .Where(e => ownerId == Guid.Empty || e.OwnerId == ownerId);

        // SCRAP: solo entradas en planta cuyo traslado lo tiene asignado
        if (isScrap && linkedEntityId.HasValue)
            query = query.Where(e =>
                e.WasteMove.IdScrap == linkedEntityId.Value ||
                e.WasteMove.IdScrap2 == linkedEntityId.Value);

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
