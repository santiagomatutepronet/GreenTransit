using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Emissions.Commands;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using GreenTransit.Domain.Constants;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.WasteMoves.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Transiciona un WasteMove de PLANIFICADO → RECOGIDO.
/// Registra tiempos reales, documentación y números NT/DI por línea.
/// Dispara el cálculo de emisiones CO₂ de forma asíncrona y no bloqueante.
/// </summary>
public sealed record ConfirmPickupCommand(
    Guid                      WasteMoveId,
    DateTime                  ActualPickupStart,
    DateTime?                 ActualPickupEnd,
    DateTime                  GatheredDate,
    string?                   DocumentId,
    string?                   DocumentHash,
    string?                   SignatureStatus,
    ConfirmPickupLineInput[]   Lines
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ConfirmPickupCommandHandler : IRequestHandler<ConfirmPickupCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly IMediator             _mediator;
    private readonly ILogger<ConfirmPickupCommandHandler> _logger;

    public ConfirmPickupCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IMediator             mediator,
        ILogger<ConfirmPickupCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _mediator    = mediator;
        _logger      = logger;
    }

    public async Task Handle(ConfirmPickupCommand request, CancellationToken ct)
    {
        // ── 1. Cargar traslado con líneas y residuos ──────────────────────────
        var wm = await _context.WasteMoves
            .Include(w => w.WasteMoveResidues)
                .ThenInclude(r => r.Residue)
            .FirstOrDefaultAsync(w => w.Id == request.WasteMoveId, ct)
            ?? throw new KeyNotFoundException(
                $"Traslado {request.WasteMoveId} no encontrado.");

        // ── 2. Validar estado ─────────────────────────────────────────────────
        if (wm.ServiceStatus != WasteMoveStatuses.Planificado)
            throw new InvalidOperationException(
                $"El traslado está en estado '{wm.ServiceStatus}' " +
                $"y no puede confirmarse como recogido. Se requiere '{WasteMoveStatuses.Planificado}'.");

        // ── 3. Validar NT/DI obligatorios en líneas peligrosas ────────────────
        var lineMap = request.Lines.ToDictionary(l => l.WasteMoveResidueId);

        var errors = new List<string>();
        foreach (var residueLine in wm.WasteMoveResidues)
        {
            if (residueLine.Residue?.IsDangerous != true) continue;

            if (!lineMap.TryGetValue(residueLine.Id, out var input))
            {
                errors.Add($"Línea {residueLine.Id}: residuo peligroso sin datos NT/DI.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(input.NTNumber))
                errors.Add($"Línea {residueLine.Id} ({residueLine.Residue.Name}): NTNumber es obligatorio para residuos peligrosos.");
            if (string.IsNullOrWhiteSpace(input.DINumber))
                errors.Add($"Línea {residueLine.Id} ({residueLine.Residue.Name}): DINumber es obligatorio para residuos peligrosos.");
            if (string.IsNullOrWhiteSpace(input.DIPhase))
                errors.Add($"Línea {residueLine.Id} ({residueLine.Residue.Name}): DIPhase es obligatorio para residuos peligrosos.");
        }

        if (errors.Count > 0)
            throw new ValidationException(string.Join(Environment.NewLine, errors));

        // ── 4. Actualizar cabecera del WasteMove ──────────────────────────────
        wm.ActualPickupStart  = request.ActualPickupStart;
        wm.ActualPickupEnd    = request.ActualPickupEnd;
        wm.GatheredDate       = request.GatheredDate;
        wm.DocumentId         = request.DocumentId;
        wm.DocumentHash       = request.DocumentHash;
        wm.SignatureStatus    = request.SignatureStatus;
        wm.ServiceStatus      = WasteMoveStatuses.Recogido;
        wm.DateModifiedSys    = DateTime.UtcNow;
        wm.IdUser             = _currentUser.IdUser;
        wm.Version++;

        // ── 5. Actualizar líneas NT/DI ────────────────────────────────────────
        foreach (var residueLine in wm.WasteMoveResidues)
        {
            if (!lineMap.TryGetValue(residueLine.Id, out var input)) continue;

            residueLine.NTNumber = input.NTNumber;
            residueLine.DINumber = input.DINumber;
            residueLine.DIPhase  = input.DIPhase;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "WasteMove {WasteMoveId} confirmado como RECOGIDO por usuario {IdUser}.",
            wm.Id, _currentUser.IdUser);

        // ── 6. Disparar cálculo de emisiones de forma asíncrona ───────────────
        _ = Task.Run(async () =>
        {
            try
            {
                await _mediator.Send(
                    new CalculateEmissionsCommand(request.WasteMoveId),
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error al calcular emisiones CO₂ para WasteMove {WasteMoveId}. " +
                    "El flujo no se ha bloqueado.", request.WasteMoveId);
            }
        });
    }
}

// ── Excepción de validación de dominio ────────────────────────────────────────

/// <summary>Excepción lanzada cuando faltan campos obligatorios en la confirmación de recogida.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
