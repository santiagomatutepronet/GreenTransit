using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EntryPlants.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.EntryPlants.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetEntryPlantByIdQuery(Guid Id) : IRequest<EntryPlantDetailDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetEntryPlantByIdQueryHandler
    : IRequestHandler<GetEntryPlantByIdQuery, EntryPlantDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetEntryPlantByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<EntryPlantDetailDto?> Handle(
        GetEntryPlantByIdQuery request, CancellationToken ct)
    {
        var e = await _context.EntryPlants
            .AsNoTracking()
            .Include(x => x.EntryPlantResidues)
                .ThenInclude(r => r.Residue)
            .Include(x => x.WasteMove)
                .ThenInclude(w => w.Source)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (e is null) return null;

        // Determina si hubo incidencia de descuadre de peso en este ticket
        var hasDiscrepancy = await _context.Incidents
            .AsNoTracking()
            .AnyAsync(i => i.TicketScale == e.TicketScale
                        && i.Type == "WeightDiscrepancy", ct);

        var residues = e.EntryPlantResidues
            .Select(r => new EntryPlantResidueDto(
                r.Id,
                r.IdEntryPlant,
                r.IdResidue,
                r.Residue?.Name,
                r.Weight,
                r.MeasureUnit,
                r.Units,
                r.PriceWeight,
                r.PriceUnit))
            .ToList();

        return new EntryPlantDetailDto(
            e.Id,
            e.IdWasteMove,
            e.WasteMoveReference,
            e.WasteMove?.ServiceStatus,
            e.WasteMove?.Source?.Name,
            e.OwnerId,
            e.TicketScale,
            e.WeighbridgeId,
            e.PlantEntryDate,
            e.GrossWeight,
            e.TareWeight,
            e.NetWeight,
            e.TypeContainer,
            e.PriceContainer,
            e.ServiceOrderId,
            e.IdUser,
            e.DateCreateSys,
            e.DateModifiedSys,
            hasDiscrepancy,
            residues);
    }
}
