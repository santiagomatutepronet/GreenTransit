using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.Emissions.Queries;

/// <summary>
/// Devuelve una estimación de emisiones CO₂ por línea de residuo para el panel
/// previo a confirmar la recogida. Usa el EmissionFactorSet activo y la distancia
/// planificada de la ServiceOrder. No persiste nada.
/// </summary>
public sealed record GetEmissionEstimatesQuery(Guid WasteMoveId)
    : IRequest<IReadOnlyList<EmissionEstimateDto>>;

public sealed class GetEmissionEstimatesQueryHandler
    : IRequestHandler<GetEmissionEstimatesQuery, IReadOnlyList<EmissionEstimateDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEmissionEstimatesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<EmissionEstimateDto>> Handle(
        GetEmissionEstimatesQuery request, CancellationToken ct)
    {
        var residues = await _context.WasteMoveResidues
            .AsNoTracking()
            .Include(r => r.Residue)
            .Where(r => r.IdWasteMove == request.WasteMoveId)
            .ToListAsync(ct);

        if (residues.Count == 0)
            return [];

        _context.IgnoreTenantFilter();
        EmissionFactorSet? activeSet;
        try
        {
            activeSet = await _context.EmissionFactorSets
                .AsNoTracking()
                .Where(s => s.Status == "Active" && s.ValidFrom <= DateTime.UtcNow)
                .OrderByDescending(s => s.ValidFrom)
                .FirstOrDefaultAsync(ct);
        }
        finally { _context.RestoreTenantFilter(); }

        if (activeSet is null)
            return [];

        var factors = await _context.EmissionFactors
            .AsNoTracking()
            .Where(f => f.FactorSetId == activeSet.Id)
            .ToListAsync(ct);

        var result = new List<EmissionEstimateDto>();

        foreach (var line in residues)
        {
            var distance = line.TransportInfo_TransportDistance ?? 0m;
            if (distance <= 0) continue;

            var factor = factors.FirstOrDefault(f =>
                f.VehicleType == line.VehicleType &&
                f.FuelType    == line.FuelType    &&
                (f.EuroClass  == line.EuroClass || f.EuroClass is null));

            if (factor is null) continue;

            result.Add(new EmissionEstimateDto(
                line.Id,
                line.Residue?.Name,
                distance,
                factor.Value,
                distance * factor.Value,
                activeSet.Version
            ));
        }

        return result;
    }
}
