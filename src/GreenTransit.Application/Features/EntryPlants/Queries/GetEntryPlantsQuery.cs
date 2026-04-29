using GreenTransit.Application.Common.Interfaces;
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
    int       Page                 = 1,
    int       PageSize             = 20
) : IRequest<GetEntryPlantsResult>;

public sealed record GetEntryPlantsResult(
    IReadOnlyList<EntryPlantDto> Items,
    int                          TotalCount,
    int                          Page,
    int                          PageSize
);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetEntryPlantsQueryHandler
    : IRequestHandler<GetEntryPlantsQuery, GetEntryPlantsResult>
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

    public async Task<GetEntryPlantsResult> Handle(
        GetEntryPlantsQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var query = _context.EntryPlants
            .AsNoTracking()
            .Where(e => ownerId == Guid.Empty || e.OwnerId == ownerId);

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

        var items = await query
            .OrderByDescending(e => e.PlantEntryDate)
            .ThenByDescending(e => e.DateCreateSys)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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

        return new GetEntryPlantsResult(items, total, request.Page, request.PageSize);
    }
}
