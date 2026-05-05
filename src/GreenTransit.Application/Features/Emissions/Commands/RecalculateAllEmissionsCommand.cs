using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Emissions.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Re-calcula las emisiones CO₂ de todas las líneas de traslados en estado
/// RECOGIDO o posterior usando el EmissionFactorSet activo actual.
/// Solo disponible para perfil ADMIN.
/// Procesa en lotes de 100 para no saturar la base de datos.
/// </summary>
public sealed record RecalculateAllEmissionsCommand : IRequest<RecalculateAllEmissionsResult>;

public sealed record RecalculateAllEmissionsResult(
    int LinesProcessed,
    int LinesUpdated,
    int LinesSkipped
);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class RecalculateAllEmissionsCommandHandler
    : IRequestHandler<RecalculateAllEmissionsCommand, RecalculateAllEmissionsResult>
{
    private const int BatchSize = 100;

    private static readonly IReadOnlyList<string> PostPickupStatuses =
    [
        WasteMoveStatuses.Recogido,
        WasteMoveStatuses.EnCAC,
        WasteMoveStatuses.EnPlanta,
        WasteMoveStatuses.Clasificado
    ];

    private readonly IApplicationDbContext _context;
    private readonly ILogger<RecalculateAllEmissionsCommandHandler> _logger;

    public RecalculateAllEmissionsCommandHandler(
        IApplicationDbContext context,
        ILogger<RecalculateAllEmissionsCommandHandler> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task<RecalculateAllEmissionsResult> Handle(
        RecalculateAllEmissionsCommand request, CancellationToken ct)
    {
        // ── Obtener EmissionFactorSet activo (catálogo global, sin filtro de tenant) ──
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
        {
            _logger.LogWarning(
                "RecalculateAllEmissions: no existe EmissionFactorSet activo. Operación cancelada.");
            return new RecalculateAllEmissionsResult(0, 0, 0);
        }

        var factors = await _context.EmissionFactors
            .AsNoTracking()
            .Where(f => f.FactorSetId == activeSet.Id)
            .ToListAsync(ct);

        // ── Obtener IDs de WasteMoves elegibles ───────────────────────────────
        var eligibleIds = await _context.WasteMoves
            .AsNoTracking()
            .Where(w => PostPickupStatuses.Contains(w.ServiceStatus!))
            .Select(w => w.Id)
            .ToListAsync(ct);

        _logger.LogInformation(
            "RecalculateAllEmissions: {Count} traslados elegibles. FactorSet={FactorSetId} v{Version}.",
            eligibleIds.Count, activeSet.Id, activeSet.Version);

        int processed = 0, updated = 0, skipped = 0;

        for (int offset = 0; offset < eligibleIds.Count; offset += BatchSize)
        {
            ct.ThrowIfCancellationRequested();

            var batchIds = eligibleIds.Skip(offset).Take(BatchSize).ToList();

            var lines = await _context.WasteMoveResidues
                .Where(r => batchIds.Contains(r.IdWasteMove))
                .ToListAsync(ct);

            foreach (var line in lines)
            {
                processed++;
                var distance = line.TransportInfo_TransportDistance ?? 0m;

                if (distance <= 0) { skipped++; continue; }

                var factor = factors.FirstOrDefault(f =>
                    f.VehicleType == line.VehicleType &&
                    f.FuelType    == line.FuelType    &&
                    (f.EuroClass  == line.EuroClass || f.EuroClass is null));

                if (factor is null)
                {
                    _logger.LogWarning(
                        "RecalculateAllEmissions: factor no encontrado para " +
                        "VehicleType={VehicleType} FuelType={FuelType} EuroClass={EuroClass}. " +
                        "Línea {LineId} omitida.",
                        line.VehicleType, line.FuelType, line.EuroClass, line.Id);
                    skipped++;
                    continue;
                }

                line.TransportInfo_TransportCarbonEmissions = distance * factor.Value;
                line.EmissionFactorSetId                    = activeSet.Id;
                line.EmissionFactorVersion                  = activeSet.Version;
                updated++;
            }

            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "RecalculateAllEmissions: lote {Offset}-{End} procesado.",
                offset, offset + batchIds.Count - 1);
        }

        _logger.LogInformation(
            "RecalculateAllEmissions completado: {Processed} procesadas, " +
            "{Updated} actualizadas, {Skipped} omitidas.",
            processed, updated, skipped);

        return new RecalculateAllEmissionsResult(processed, updated, skipped);
    }
}
