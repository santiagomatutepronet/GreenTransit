using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Incidents.DTOs;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Incidents.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Abre una nueva incidencia. Cualquier perfil autenticado puede ejecutarlo.
/// Si Severity ∈ {High, Critical} y WasteMoveReference está informado,
/// bloquea el WasteMove y persiste su estado previo en ResolutionJson.
/// </summary>
public sealed record OpenIncidentCommand(
    string   Type,
    string   Severity,
    Guid?    ServiceOrderId,
    string?  WasteMoveReference,
    string?  TicketScale,
    string   ReportedByName,
    string?  ReportedByNationalId,
    string?  ReportedByCenterCode,
    string   Description
) : IRequest<Guid>;

// ── Validación ────────────────────────────────────────────────────────────────

public sealed class OpenIncidentCommandValidator : AbstractValidator<OpenIncidentCommand>
{
    private static readonly string[] ValidSeverities = ["Low", "Medium", "High", "Critical"];

    public OpenIncidentCommandValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("El tipo de incidencia es obligatorio.");

        RuleFor(x => x.Severity)
            .NotEmpty()
            .Must(s => ValidSeverities.Contains(s))
            .WithMessage("La severidad debe ser: Low, Medium, High o Critical.");

        RuleFor(x => x.ReportedByName)
            .NotEmpty().WithMessage("El nombre del notificador es obligatorio.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(2000);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class OpenIncidentCommandHandler
    : IRequestHandler<OpenIncidentCommand, Guid>
{
    private readonly IApplicationDbContext               _context;
    private readonly ICurrentUserService                 _currentUser;
    private readonly ILogger<OpenIncidentCommandHandler> _logger;

    public OpenIncidentCommandHandler(
        IApplicationDbContext               context,
        ICurrentUserService                 currentUser,
        ILogger<OpenIncidentCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<Guid> Handle(OpenIncidentCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        var now     = DateTime.UtcNow;

        string? resolutionJson = null;

        if (request.Severity is "High" or "Critical" &&
            !string.IsNullOrWhiteSpace(request.WasteMoveReference))
        {
            var wasteMove = await _context.WasteMoves
                .Where(w => w.WasteMoveReference == request.WasteMoveReference &&
                            (ownerId == Guid.Empty || w.OwnerId == ownerId))
                .FirstOrDefaultAsync(ct);

            if (wasteMove is not null)
            {
                var snapshot = new IncidentResolutionDto(
                    PreviousStatus:        wasteMove.ServiceStatus,
                    ResolutionType:        null,
                    ResolutionDescription: null,
                    ResolvedByName:        null,
                    ResolvedAt:            null);

                resolutionJson         = JsonSerializer.Serialize(snapshot);
                wasteMove.ServiceStatus = "BLOQUEADO";

                _logger.LogWarning(
                    "WasteMove {Ref} bloqueado por incidencia Severity={Severity}.",
                    request.WasteMoveReference, request.Severity);
            }
        }

        var incident = new Incident
        {
            Id                   = Guid.NewGuid(),
            OwnerId              = ownerId == Guid.Empty ? null : ownerId,
            Type                 = request.Type,
            Severity             = request.Severity,
            OpenedAt             = now,
            ClosedAt             = null,
            ServiceOrderId       = request.ServiceOrderId,
            WasteMoveReference   = request.WasteMoveReference,
            TicketScale          = request.TicketScale,
            ReportedByName       = request.ReportedByName,
            ReportedByNationalId = request.ReportedByNationalId,
            ReportedByCenterCode = request.ReportedByCenterCode,
            Description          = request.Description,
            ResolutionJson       = resolutionJson,
            SourceSystem         = "GreenTransit",
            Version              = 1,
            CreatedAt            = now,
            UpdatedAt            = now,
            IdUser               = _currentUser.IdUser
        };

        incident.Hash = ComputeHash(incident);

        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Incidencia {Id} abierta (Severity={Severity}, Type={Type}) por usuario {IdUser}.",
            incident.Id, incident.Severity, incident.Type, _currentUser.IdUser);

        return incident.Id;
    }

    private static string ComputeHash(Incident i)
    {
        var raw   = $"{i.Id}|{i.Type}|{i.Severity}|{i.OpenedAt:O}|{i.Description}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
