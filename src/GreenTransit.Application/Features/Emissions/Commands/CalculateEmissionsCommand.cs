using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Emissions.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Calcula la huella de carbono (kgCO₂e) de cada línea de residuo de un traslado.
/// Se dispara automáticamente al confirmar la recogida (RECOGIDO).
/// Si no existe factor para una combinación, loguea Warning y continúa.
/// Nunca lanza excepción que bloquee el flujo de recogida.
/// </summary>
public sealed record CalculateEmissionsCommand(Guid WasteMoveId) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CalculateEmissionsCommandHandler
    : IRequestHandler<CalculateEmissionsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CalculateEmissionsCommandHandler> _logger;

    public CalculateEmissionsCommandHandler(
        IApplicationDbContext context,
        ILogger<CalculateEmissionsCommandHandler> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task Handle(CalculateEmissionsCommand request, CancellationToken ct)
    {
        var residues = await _context.WasteMoveResidues
            .Where(r => r.IdWasteMove == request.WasteMoveId)
            .ToListAsync(ct);

        if (residues.Count == 0) return;

        // ── Obtener el EmissionFactorSet activo más reciente (catálogo global) ──
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
                "No existe ningún EmissionFactorSet activo. " +
                "No se calcularon emisiones para WasteMove {WasteMoveId}.",
                request.WasteMoveId);
            return;
        }

        // ── Cargar todos los factores del set activo en memoria ───────────────
        var factors = await _context.EmissionFactors
            .AsNoTracking()
            .Where(f => f.FactorSetId == activeSet.Id)
            .ToListAsync(ct);

        foreach (var line in residues)
        {
            var distance = line.TransportInfo_TransportDistance ?? 0m;

            if (distance <= 0)
            {
                _logger.LogWarning(
                    "Línea {ResidueId} del WasteMove {WasteMoveId}: " +
                    "TransportInfo_TransportDistance es 0 o nulo. No se calcula emisión.",
                    line.Id, request.WasteMoveId);
                continue;
            }

            // Buscar factor exacto (VehicleType + FuelType + EuroClass)
            var factor = factors.FirstOrDefault(f =>
                f.VehicleType == line.VehicleType &&
                f.FuelType    == line.FuelType    &&
                (f.EuroClass  == line.EuroClass || f.EuroClass is null));

            if (factor is null)
            {
                _logger.LogWarning(
                    "Factor de emisión no encontrado para " +
                    "FactorSetId={FactorSetId} VehicleType={VehicleType} " +
                    "FuelType={FuelType} EuroClass={EuroClass}. Línea {ResidueId} omitida.",
                    activeSet.Id, line.VehicleType, line.FuelType, line.EuroClass, line.Id);
                continue;
            }

            var emissions = distance * factor.Value;

            line.TransportInfo_TransportCarbonEmissions = emissions;
            line.EmissionFactorSetId                    = activeSet.Id;
            line.EmissionFactorVersion                  = activeSet.Version;

            _logger.LogInformation(
                "Emisiones calculadas para línea {ResidueId}: " +
                "{Distance} km × {Factor} kgCO₂e/km = {Emissions} kgCO₂e. " +
                "EmissionFactorVersion={Version}",
                line.Id, distance, factor.Value, emissions, activeSet.Version);
        }

        await _context.SaveChangesAsync(ct);
    }
}
