using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.TreatmentPlants.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.TreatmentPlants.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetTreatmentPlantByIdQuery(Guid Id) : IRequest<TreatmentPlantDetailDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetTreatmentPlantByIdQueryHandler
    : IRequestHandler<GetTreatmentPlantByIdQuery, TreatmentPlantDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetTreatmentPlantByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<TreatmentPlantDetailDto?> Handle(
        GetTreatmentPlantByIdQuery request, CancellationToken ct)
    {
        var t = await _context.TreatmentPlants
            .AsNoTracking()
            .Include(x => x.TreatmentOperation)
            .Include(x => x.WasteMove)
            .Include(x => x.TreatmentPlantResidues)
                .ThenInclude(r => r.Residue)
            .Include(x => x.TreatmentPlantResidues)
                .ThenInclude(r => r.ResidueReused)
            .Include(x => x.TreatmentPlantResidues)
                .ThenInclude(r => r.ResidueValued)
            .Include(x => x.TreatmentPlantResidues)
                .ThenInclude(r => r.ResidueRemove)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (t is null) return null;

        // ── KPIs de balance de masas ──────────────────────────────────────────
        var totalIn      = t.TreatmentPlantResidues.Sum(r => r.WeightTotal  ?? 0m);
        var totalReused  = t.TreatmentPlantResidues.Sum(r => r.WeightReused ?? 0m);
        var totalValued  = t.TreatmentPlantResidues.Sum(r => r.WeightValued ?? 0m);
        var totalRemove  = t.TreatmentPlantResidues.Sum(r => r.WeightRemove ?? 0m);

        var recyclingRate    = totalIn > 0 ? Math.Round(totalReused  / totalIn * 100, 2) : 0m;
        var valorizationRate = totalIn > 0 ? Math.Round(totalValued  / totalIn * 100, 2) : 0m;
        var rejectionRate    = totalIn > 0 ? Math.Round(totalRemove  / totalIn * 100, 2) : 0m;

        // ── Líneas ────────────────────────────────────────────────────────────
        const decimal tolerance = 0.01m; // 1%

        var residues = t.TreatmentPlantResidues
            .Select(r =>
            {
                var sum   = (r.WeightReused ?? 0m) + (r.WeightValued ?? 0m) + (r.WeightRemove ?? 0m)
                          + (t.ImproperWeight ?? 0m);
                var diff  = Math.Abs((r.WeightTotal ?? 0m) - sum);
                var total = r.WeightTotal ?? 0m;
                var ok    = total == 0 || diff <= total * tolerance;

                return new TreatmentPlantResidueDto(
                    r.Id,
                    r.IdTreatmentPlant,
                    r.IdResidue,
                    r.Residue?.Name,
                    r.Category,
                    r.WeightTotal,
                    r.MeasureUnit,
                    r.Units,
                    r.PriceWeight,
                    r.PriceUnit,
                    r.IdResidueReused,
                    r.ResidueReused?.Name,
                    r.WeightReused,
                    r.MeasureUnitReused,
                    r.UnitsReused,
                    r.IdResidueValued,
                    r.ResidueValued?.Name,
                    r.WeightValued,
                    r.MeasureUnitValued,
                    r.UnitsValued,
                    r.IdResidueRemove,
                    r.ResidueRemove?.Name,
                    r.WeightRemove,
                    r.MeasureUnitRemove,
                    r.UnitsRemove,
                    sum,
                    diff,
                    ok);
            })
            .ToList();

        return new TreatmentPlantDetailDto(
            t.Id,
            t.IdWasteMove,
            t.WasteMoveReference,
            t.WasteMove?.ServiceStatus,
            t.OwnerId,
            t.TicketScale,
            t.PlantTreatmentDate,
            t.IdTreatmentOperation,
            t.TreatmentOperation?.Code,
            t.TreatmentOperation?.Description,
            t.TreatmentOperation?.OperationType,
            t.TreatmentOperation?.IsRecycling ?? false,
            t.TreatmentOperation?.IsEnergyRecovery ?? false,
            t.TreatmentOperation?.IsPreparationForReuse ?? false,
            t.ImproperWeight,
            t.QualityMetricsJson,
            t.TypeContainer,
            t.PriceContainer,
            t.ServiceOrderId,
            t.IncidentId,
            t.IdUser,
            t.DateCreateSys,
            t.DateModifiedSys,
            totalIn,
            totalReused,
            totalValued,
            totalRemove,
            recyclingRate,
            valorizationRate,
            rejectionRate,
            residues);
    }
}
