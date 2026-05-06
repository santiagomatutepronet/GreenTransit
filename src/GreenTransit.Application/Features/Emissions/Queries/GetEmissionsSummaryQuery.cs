using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
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

/// <summary>Totales globales del panel de emisiones (para todo el dataset filtrado).</summary>
public sealed record EmissionsSummaryDto(
    decimal                                    TotalKgCO2e,
    int                                        TotalMoves,
    int                                        TotalLines,
    PaginatedResult<EmissionSummaryLineDto>    Page
);

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve el resumen paginado de emisiones CO₂ de los traslados del tenant,
/// agrupado por WasteMove. Solo incluye traslados con emisión calculada.
/// Paginación real en BD mediante WasteMoves como entidad base.
/// </summary>
public sealed record GetEmissionsSummaryQuery(
    DateTime? DateFrom   = null,
    DateTime? DateTo     = null,
    int       PageNumber = 1,
    int       PageSize   = 15
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
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // Consulta base: WasteMoves con al menos una línea con emisión calculada.
        // Usar WasteMoves como entidad base permite paginación real en BD.
        var wmQuery = _context.WasteMoves
            .AsNoTracking()
            .Where(wm => wm.WasteMoveResidues.Any(r =>
                r.TransportInfo_TransportCarbonEmissions != null &&
                r.TransportInfo_TransportCarbonEmissions > 0));

        if (request.DateFrom.HasValue)
            wmQuery = wmQuery.Where(wm => wm.GatheredDate >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            wmQuery = wmQuery.Where(wm => wm.GatheredDate <= request.DateTo.Value);

        // ── Totales globales (todo el dataset filtrado, no solo la página) ────
        var totals = await wmQuery
            .Select(wm => new
            {
                KgCO2e = wm.WasteMoveResidues
                    .Where(r => r.TransportInfo_TransportCarbonEmissions != null && r.TransportInfo_TransportCarbonEmissions > 0)
                    .Sum(r => r.TransportInfo_TransportCarbonEmissions ?? 0m),
                Lines = wm.WasteMoveResidues
                    .Count(r => r.TransportInfo_TransportCarbonEmissions != null && r.TransportInfo_TransportCarbonEmissions > 0)
            })
            .ToListAsync(ct);

        var totalMoves  = totals.Count;
        var totalKgCO2e = totals.Sum(x => x.KgCO2e);
        var totalLines  = totals.Sum(x => x.Lines);

        // ── Página de traslados ───────────────────────────────────────────────
        var lines = await wmQuery
            .OrderByDescending(wm => wm.GatheredDate)
            .ThenBy(wm => wm.Id)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(wm => new EmissionSummaryLineDto(
                wm.Id,
                wm.WasteMoveReference,
                wm.ServiceStatus,
                wm.WasteMoveResidues.Count(r =>
                    r.TransportInfo_TransportCarbonEmissions != null &&
                    r.TransportInfo_TransportCarbonEmissions > 0),
                wm.WasteMoveResidues.Sum(r => r.TransportInfo_TransportCarbonEmissions ?? 0m),
                wm.WasteMoveResidues.Sum(r => r.TransportInfo_TransportDistance ?? 0m),
                wm.WasteMoveResidues
                    .Where(r => r.EmissionFactorVersion != null)
                    .Select(r => r.EmissionFactorVersion)
                    .FirstOrDefault(),
                wm.GatheredDate))
            .ToListAsync(ct);

        var page = PaginatedResult<EmissionSummaryLineDto>.Create(
            lines, totalMoves, request.PageNumber, pageSize);

        return new EmissionsSummaryDto(totalKgCO2e, totalMoves, totalLines, page);
    }
}
