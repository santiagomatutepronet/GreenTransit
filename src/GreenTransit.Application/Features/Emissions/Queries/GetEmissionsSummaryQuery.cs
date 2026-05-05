using GreenTransit.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Emissions.Queries;

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>Línea de resumen de emisiones CO₂ por traslado.</summary>
public sealed record EmissionSummaryLineDto(
    Guid    WasteMoveId,
    string? WasteMoveReference,
    string? ServiceStatus,
    int     ResidueLines,
    decimal TotalKgCO2e,
    decimal TotalDistanceKm,
    string? EmissionFactorVersion,
    DateTime? GatheredDate
);

/// <summary>Totales globales del panel de emisiones.</summary>
public sealed record EmissionsSummaryDto(
    decimal                          TotalKgCO2e,
    int                              TotalMoves,
    int                              TotalLines,
    IReadOnlyList<EmissionSummaryLineDto> Lines
);

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve el resumen de emisiones CO₂ de los traslados del tenant,
/// agrupado por WasteMove. Solo incluye líneas con emisión calculada.
/// </summary>
public sealed record GetEmissionsSummaryQuery(
    DateTime? DateFrom = null,
    DateTime? DateTo   = null,
    int       Take     = 100
) : IRequest<EmissionsSummaryDto>;

public sealed class GetEmissionsSummaryQueryHandler
    : IRequestHandler<GetEmissionsSummaryQuery, EmissionsSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEmissionsSummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<EmissionsSummaryDto> Handle(
        GetEmissionsSummaryQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var residuesQuery = _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => r.TransportInfo_TransportCarbonEmissions != null
                     && r.TransportInfo_TransportCarbonEmissions > 0);

        if (request.DateFrom.HasValue)
            residuesQuery = residuesQuery.Where(r =>
                r.WasteMove!.GatheredDate >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            residuesQuery = residuesQuery.Where(r =>
                r.WasteMove!.GatheredDate <= request.DateTo.Value);

        // EF Core no puede traducir GroupBy cuando la clave incluye propiedades
        // de navegación (genera TransparentIdentifier internamente). Se proyecta
        // primero a un tipo anónimo plano (SQL simple con JOIN) y se agrupa en cliente.
        var flat = await residuesQuery
            .Select(r => new
            {
                r.IdWasteMove,
                r.WasteMove!.WasteMoveReference,
                r.WasteMove.ServiceStatus,
                r.WasteMove.GatheredDate,
                r.EmissionFactorVersion,
                r.TransportInfo_TransportCarbonEmissions,
                r.TransportInfo_TransportDistance
            })
            .ToListAsync(ct);

        var lines = flat
            .GroupBy(r => new
            {
                r.IdWasteMove,
                r.WasteMoveReference,
                r.ServiceStatus,
                r.GatheredDate,
                r.EmissionFactorVersion
            })
            .Select(g => new EmissionSummaryLineDto(
                g.Key.IdWasteMove,
                g.Key.WasteMoveReference,
                g.Key.ServiceStatus,
                g.Count(),
                g.Sum(r => r.TransportInfo_TransportCarbonEmissions!.Value),
                g.Sum(r => r.TransportInfo_TransportDistance ?? 0m),
                g.Key.EmissionFactorVersion,
                g.Key.GatheredDate))
            .OrderByDescending(l => l.GatheredDate)
            .Take(request.Take)
            .ToList();

        return new EmissionsSummaryDto(
            lines.Sum(l => l.TotalKgCO2e),
            lines.Select(l => l.WasteMoveId).Distinct().Count(),
            lines.Sum(l => l.ResidueLines),
            lines);
    }
}
