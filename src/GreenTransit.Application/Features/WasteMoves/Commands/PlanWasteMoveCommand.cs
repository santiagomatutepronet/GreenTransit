using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.WasteMoves.Commands;

// ── Input de línea de residuo para planificación ─────────────────────────────

/// <summary>Datos de transporte asignados a cada línea de residuo al planificar.</summary>
public sealed record PlanWasteMoveLineInput(
    Guid    WasteMoveResidueId,
    Guid    IdCarrier,
    string  VehicleRegistration,
    string? VehicleRegistrationTrailer,
    string? VehicleType,
    string? FuelType,
    string? EuroClass
);

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Transiciona un WasteMove de SOLICITADO → PLANIFICADO,
/// asigna el operador de transferencia y los datos de transporte por línea,
/// y valida las restricciones DUM del punto de recogida.
/// </summary>
public sealed record PlanWasteMoveCommand(
    Guid                    WasteMoveId,
    DateTime                PlannedPickupStart,
    DateTime                PlannedPickupEnd,
    DateTime                PlannedDeliveryStart,
    DateTime                PlannedDeliveryEnd,
    Guid                    IdOperatorTransfer,
    PlanWasteMoveLineInput[] Lines
) : IRequest<PlanWasteMoveResult>;

/// <summary>Resultado del comando: si hay advertencias DUM se incluyen aquí.</summary>
public sealed record PlanWasteMoveResult(
    string   ActionType,
    string?  DumReason,
    string[] DumZoneCodes
);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class PlanWasteMoveCommandHandler
    : IRequestHandler<PlanWasteMoveCommand, PlanWasteMoveResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDumZoneService       _dumZoneService;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<PlanWasteMoveCommandHandler> _logger;

    public PlanWasteMoveCommandHandler(
        IApplicationDbContext context,
        IDumZoneService       dumZoneService,
        ICurrentUserService   currentUser,
        ILogger<PlanWasteMoveCommandHandler> logger)
    {
        _context        = context;
        _dumZoneService = dumZoneService;
        _currentUser    = currentUser;
        _logger         = logger;
    }

    public async Task<PlanWasteMoveResult> Handle(
        PlanWasteMoveCommand request, CancellationToken ct)
    {
        // ── Paso 1: cargar y validar estado ───────────────────────────────────
        var wm = await _context.WasteMoves
            .Include(w => w.WasteMoveResidues)
            .Include(w => w.ServiceOrder)
            .FirstOrDefaultAsync(w => w.Id == request.WasteMoveId, ct)
            ?? throw new KeyNotFoundException(
                $"Traslado {request.WasteMoveId} no encontrado.");

        if (wm.ServiceStatus != WasteMoveStatuses.Solicitado)
            throw new InvalidOperationException(
                $"Solo se puede planificar un traslado en estado '{WasteMoveStatuses.Solicitado}'. " +
                $"Estado actual: '{wm.ServiceStatus}'.");

        // ── Paso 2: validar operador/transportista ─────────────────────────────
        var operatorEntity = await _context.BusinessEntities
            .FirstOrDefaultAsync(e => e.Id == request.IdOperatorTransfer, ct)
            ?? throw new KeyNotFoundException(
                $"Entidad {request.IdOperatorTransfer} no encontrada.");

        if (operatorEntity.EntityRole is not (EntityRoles.Carrier or EntityRoles.OperatorTransfer))
            throw new InvalidOperationException(
                $"La entidad '{operatorEntity.Name}' no tiene rol Carrier ni OperatorTransfer " +
                $"(rol actual: '{operatorEntity.EntityRole}').");

        // Validar cada transportista por línea
        var carrierIds = request.Lines.Select(l => l.IdCarrier).Distinct().ToList();
        var carriers   = await _context.BusinessEntities
            .Where(e => carrierIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        foreach (var line in request.Lines)
        {
            if (!carriers.TryGetValue(line.IdCarrier, out var carrier))
                throw new KeyNotFoundException(
                    $"Transportista {line.IdCarrier} no encontrado.");

            if (carrier.EntityRole != EntityRoles.Carrier)
                throw new InvalidOperationException(
                    $"La entidad '{carrier.Name}' no tiene rol Carrier.");

            if (string.IsNullOrWhiteSpace(carrier.InscriptionNumber))
                throw new InvalidOperationException(
                    $"El transportista '{carrier.Name}' no tiene InscriptionNumber registrado.");
        }

        // ── Paso 3: validación DUM ─────────────────────────────────────────────
        var dumResult = new DumCheckResult("Allow", null, []);

        var pickupPointId = wm.ServiceOrder?.IdPickupPoint;
        if (pickupPointId.HasValue)
        {
            var firstLine = request.Lines.FirstOrDefault();
            dumResult = await _dumZoneService.CheckPickupPointAsync(
                pickupPointId.Value,
                request.PlannedPickupStart,
                firstLine?.VehicleType,
                firstLine?.EuroClass,
                ct);

            _logger.LogDebug(
                "DUM check para punto {PickupPointId} en fecha {Date}: ActionType={ActionType}, Zonas={Zones}",
                pickupPointId, request.PlannedPickupStart, dumResult.ActionType,
                string.Join(",", dumResult.ZoneCodes));

            if (dumResult.ActionType == "Block")
                throw new InvalidOperationException(
                    $"La planificación está bloqueada por restricción DUM. " +
                    $"Zonas: [{string.Join(", ", dumResult.ZoneCodes)}]. " +
                    $"Motivo: {dumResult.Reason}");
        }

        // ── Paso 4: actualizar WasteMove y líneas ──────────────────────────────
        wm.IdOperatorTransfer    = request.IdOperatorTransfer;
        wm.PlannedPickupStart    = request.PlannedPickupStart;
        wm.PlannedPickupEnd      = request.PlannedPickupEnd;
        wm.PlannedDeliveryStart  = request.PlannedDeliveryStart;
        wm.PlannedDeliveryEnd    = request.PlannedDeliveryEnd;
        wm.ServiceStatus         = WasteMoveStatuses.Planificado;
        wm.DateModifiedSys       = DateTime.UtcNow;

        var residueDict = wm.WasteMoveResidues.ToDictionary(r => r.Id);

        foreach (var lineInput in request.Lines)
        {
            WasteMoveResidue residueLine;

            if (lineInput.WasteMoveResidueId == Guid.Empty)
            {
                // Traslado legacy sin líneas: crear la entidad ahora
                residueLine = new WasteMoveResidue
                {
                    Id          = Guid.NewGuid(),
                    IdWasteMove = wm.Id
                };
                wm.WasteMoveResidues.Add(residueLine);
            }
            else
            {
                if (!residueDict.TryGetValue(lineInput.WasteMoveResidueId, out residueLine!))
                    throw new KeyNotFoundException(
                        $"Línea de residuo {lineInput.WasteMoveResidueId} no pertenece al traslado.");
            }

            residueLine.IdCarrier                                = lineInput.IdCarrier;
            residueLine.TransportInfo_VehicleRegistration        = lineInput.VehicleRegistration;
            residueLine.TransportInfo_VehicleRegistrationTrailer = lineInput.VehicleRegistrationTrailer;
            residueLine.VehicleType                              = lineInput.VehicleType;
            residueLine.FuelType                                 = lineInput.FuelType;
            residueLine.EuroClass                                = lineInput.EuroClass;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Traslado {WasteMoveId} planificado. DUM={ActionType}",
            wm.Id, dumResult.ActionType);

        return new PlanWasteMoveResult(
            dumResult.ActionType,
            dumResult.Reason,
            dumResult.ZoneCodes);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PlanWasteMoveCommandValidator
    : AbstractValidator<PlanWasteMoveCommand>
{
    public PlanWasteMoveCommandValidator()
    {
        RuleFor(x => x.WasteMoveId)
            .NotEmpty();

        RuleFor(x => x.PlannedPickupStart)
            .NotEmpty()
            .LessThan(x => x.PlannedPickupEnd)
            .WithMessage("La fecha de inicio de recogida debe ser anterior a la de fin.");

        RuleFor(x => x.PlannedPickupEnd)
            .NotEmpty();

        RuleFor(x => x.PlannedDeliveryStart)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.PlannedPickupStart)
            .WithMessage("La fecha de inicio de entrega debe ser posterior o igual al inicio de recogida.");

        RuleFor(x => x.PlannedDeliveryEnd)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.PlannedDeliveryStart)
            .WithMessage("La fecha de fin de entrega debe ser posterior o igual al inicio de entrega.");

        RuleFor(x => x.IdOperatorTransfer)
            .NotEmpty();

        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("Debe especificar al menos una línea de residuo.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.WasteMoveResidueId).NotEmpty();
            line.RuleFor(l => l.IdCarrier).NotEmpty();
            line.RuleFor(l => l.VehicleRegistration).NotEmpty();
        });
    }
}
