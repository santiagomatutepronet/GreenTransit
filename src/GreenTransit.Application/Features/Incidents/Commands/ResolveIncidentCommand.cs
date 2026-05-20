using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Incidents.DTOs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Incidents.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Cierra una incidencia abierta y restaura el ServiceStatus del WasteMove
/// bloqueado al estado guardado en ResolutionJson.previousStatus.
/// </summary>
public sealed record ResolveIncidentCommand(
    Guid   Id,
    string ResolutionType,
    string ResolutionDescription,
    string ResolvedByName
) : IRequest<bool>;

// ── Validación ────────────────────────────────────────────────────────────────

public sealed class ResolveIncidentCommandValidator : AbstractValidator<ResolveIncidentCommand>
{
    public ResolveIncidentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.ResolutionType)
            .NotEmpty().WithMessage("El tipo de resolución es obligatorio.");

        RuleFor(x => x.ResolutionDescription)
            .NotEmpty().WithMessage("La descripción de la resolución es obligatoria.")
            .MaximumLength(2000);

        RuleFor(x => x.ResolvedByName)
            .NotEmpty().WithMessage("El nombre del resolutor es obligatorio.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ResolveIncidentCommandHandler
    : IRequestHandler<ResolveIncidentCommand, bool>
{
    private readonly IApplicationDbContext                  _context;
    private readonly ICurrentUserService                    _currentUser;
    private readonly ILogger<ResolveIncidentCommandHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public ResolveIncidentCommandHandler(
        IApplicationDbContext                  context,
        ICurrentUserService                    currentUser,
        ILogger<ResolveIncidentCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<bool> Handle(ResolveIncidentCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var incident = await _context.Incidents
            .Where(i => i.Id == request.Id &&
                        (ownerId == Guid.Empty || i.OwnerId == ownerId))
            .FirstOrDefaultAsync(ct);

        if (incident is null)
        {
            _logger.LogWarning("Incidencia {Id} no encontrada para resolver.", request.Id);
            return false;
        }

        if (incident.ClosedAt.HasValue)
        {
            _logger.LogWarning("Incidencia {Id} ya estaba cerrada.", request.Id);
            return false;
        }

        var now = DateTime.UtcNow;

        // Leer el estado previo del WasteMove desde ResolutionJson
        string? previousStatus = null;
        if (!string.IsNullOrWhiteSpace(incident.ResolutionJson))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<IncidentResolutionDto>(
                    incident.ResolutionJson, _jsonOptions);
                previousStatus = existing?.PreviousStatus;
            }
            catch { /* JSON malformado: no restaurar */ }
        }

        // Restaurar el WasteMove si estaba BLOQUEADO y tenemos estado previo
        if (!string.IsNullOrWhiteSpace(incident.WasteMoveReference) &&
            !string.IsNullOrWhiteSpace(previousStatus))
        {
            var wasteMove = await _context.WasteMoves
                .Where(w => w.WasteMoveReference == incident.WasteMoveReference &&
                            w.ServiceStatus == "BLOQUEADO" &&
                            (ownerId == Guid.Empty || w.OwnerId == ownerId))
                .FirstOrDefaultAsync(ct);

            if (wasteMove is not null)
            {
                wasteMove.ServiceStatus = previousStatus;
                _logger.LogInformation(
                    "WasteMove {Ref} restaurado a estado '{Status}' al resolver incidencia {Id}.",
                    incident.WasteMoveReference, previousStatus, request.Id);
            }
        }

        // Actualizar ResolutionJson con todos los datos de cierre
        var resolution = new IncidentResolutionDto(
            PreviousStatus:        previousStatus,
            ResolutionType:        request.ResolutionType,
            ResolutionDescription: request.ResolutionDescription,
            ResolvedByName:        request.ResolvedByName,
            ResolvedAt:            now);

        incident.ClosedAt       = now;
        incident.ResolutionJson = JsonSerializer.Serialize(resolution);
        incident.UpdatedAt      = now;
        incident.Version++;
        incident.IdUser         = _currentUser.IdUser;
        incident.Hash           = ComputeHash(incident);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Incidencia {Id} resuelta por '{User}' (Tipo: {Type}).",
            incident.Id, request.ResolvedByName, request.ResolutionType);

        return true;
    }

    private static string ComputeHash(Domain.Entities.Incident i)
    {
        var raw   = $"{i.Id}|{i.Type}|{i.Severity}|{i.OpenedAt:O}|{i.ClosedAt:O}|{i.ResolutionJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
