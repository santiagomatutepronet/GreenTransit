using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.TreatmentPlants.Commands;

// ── Input de línea ────────────────────────────────────────────────────────────

/// <summary>Datos de una línea de residuo para el registro de tratamiento.</summary>
public sealed record CreateTreatmentPlantLineInput(
    Guid     IdResidue,
    string?  Category,
    decimal  WeightTotal,
    string   MeasureUnit,
    int?     Units,
    decimal? PriceWeight,
    decimal? PriceUnit,
    // Fracción reutilizada
    Guid?    IdResidueReused,
    decimal? WeightReused,
    string?  MeasureUnitReused,
    int?     UnitsReused,
    // Fracción valorizada
    Guid?    IdResidueValued,
    decimal? WeightValued,
    string?  MeasureUnitValued,
    int?     UnitsValued,
    // Fracción rechazo
    Guid?    IdResidueRemove,
    decimal? WeightRemove,
    string?  MeasureUnitRemove,
    int?     UnitsRemove
);

// ── Resultado ─────────────────────────────────────────────────────────────────

/// <summary>Resultado del comando con el Id creado y lista de líneas con descuadre (si las hay).</summary>
public sealed record CreateTreatmentPlantResult(
    Guid          TreatmentPlantId,
    bool          HasBalanceErrors,
    IReadOnlyList<string> BalanceErrorMessages
);

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Registra el tratamiento final de un traslado.
/// Valida el balance de masas por línea. Si todas las líneas cuadran,
/// transiciona WasteMove a CLASIFICADO. Si alguna falla, crea un Incident
/// con Severity=High y devuelve error sin cambiar el estado.
/// </summary>
public sealed record CreateTreatmentPlantCommand(
    Guid                              WasteMoveId,
    DateTime                          PlantTreatmentDate,
    Guid                              IdTreatmentOperation,
    string?                           TicketScale,
    Guid?                             ServiceOrderId,
    decimal?                          ImproperWeight,
    string?                           QualityMetricsJson,
    string?                           TypeContainer,
    decimal?                          PriceContainer,
    CreateTreatmentPlantLineInput[]   Lines
) : IRequest<CreateTreatmentPlantResult>;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateTreatmentPlantCommandValidator : AbstractValidator<CreateTreatmentPlantCommand>
{
    public CreateTreatmentPlantCommandValidator()
    {
        RuleFor(x => x.WasteMoveId).NotEmpty();
        RuleFor(x => x.IdTreatmentOperation).NotEmpty();
        RuleFor(x => x.PlantTreatmentDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.Now.AddMinutes(5))
            .WithMessage("La fecha de tratamiento no puede ser futura.");
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("Debe incluir al menos una línea de residuo.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.IdResidue).NotEmpty();
            line.RuleFor(l => l.WeightTotal)
                .GreaterThan(0)
                .WithMessage("El peso total de la línea debe ser mayor que 0.");
            line.RuleFor(l => l.MeasureUnit).NotEmpty().MaximumLength(20);
        });
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateTreatmentPlantCommandHandler
    : IRequestHandler<CreateTreatmentPlantCommand, CreateTreatmentPlantResult>
{
    private const decimal BalanceTolerance = 0.01m; // 1%

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<CreateTreatmentPlantCommandHandler> _logger;

    public CreateTreatmentPlantCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        ILogger<CreateTreatmentPlantCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<CreateTreatmentPlantResult> Handle(
        CreateTreatmentPlantCommand request, CancellationToken ct)
    {
        // ── 1. Cargar el traslado con su entrada en planta ────────────────────
        var wm = await _context.WasteMoves
            .FirstOrDefaultAsync(w => w.Id == request.WasteMoveId, ct)
            ?? throw new KeyNotFoundException(
                $"Traslado {request.WasteMoveId} no encontrado.");

        // ── 2. Validar estado: debe ser EN_PLANTA ─────────────────────────────
        if (wm.ServiceStatus != WasteMoveStatuses.EnPlanta)
            throw new InvalidOperationException(
                $"El traslado está en estado '{wm.ServiceStatus}'. " +
                $"Se requiere '{WasteMoveStatuses.EnPlanta}' para registrar el tratamiento.");

        // ── 3. Verificar que existe al menos una EntryPlant previa ────────────
        var hasEntryPlant = await _context.EntryPlants
            .AnyAsync(e => e.IdWasteMove == wm.Id, ct);
        if (!hasEntryPlant)
            throw new InvalidOperationException(
                $"El traslado {wm.WasteMoveReference} no tiene ninguna entrada en planta registrada. " +
                "Registre el pesaje antes de aplicar el tratamiento.");

        // ── 4. Validar balance de masas por línea ─────────────────────────────
        var balanceErrors = new List<string>();

        foreach (var line in request.Lines)
        {
            var sumFractions = (line.WeightReused ?? 0m)
                             + (line.WeightValued ?? 0m)
                             + (line.WeightRemove ?? 0m)
                             + (request.ImproperWeight ?? 0m);

            var diff = Math.Abs(line.WeightTotal - sumFractions);
            var threshold = line.WeightTotal * BalanceTolerance;

            if (diff > threshold)
            {
                balanceErrors.Add(
                    $"Línea residuo {line.IdResidue}: descuadre de {diff:F2} kg " +
                    $"(total={line.WeightTotal:F2}, suma fracciones={sumFractions:F2}, " +
                    $"tolerancia={threshold:F2}).");
            }
        }

        if (balanceErrors.Count > 0)
        {
            // Crear Incident automático con Severity=High
            var incident = new Incident
            {
                Id                 = Guid.NewGuid(),
                OwnerId            = wm.OwnerId,
                Type               = "MassBalanceError",
                Severity           = "High",
                OpenedAt           = DateTime.UtcNow,
                WasteMoveReference = wm.WasteMoveReference,
                TicketScale        = request.TicketScale,
                Description        = string.Join(" | ", balanceErrors),
                CreatedAt          = DateTime.UtcNow,
                UpdatedAt          = DateTime.UtcNow,
                IdUser             = _currentUser.IdUser,
                Version            = 1,
            };
            _context.Add(incident);
            await _context.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Balance de masas incorrecto en traslado {WasteMoveRef}: {Errors}",
                wm.WasteMoveReference, string.Join("; ", balanceErrors));

            // Devuelve error de dominio con lista de líneas con descuadre
            throw new InvalidOperationException(
                $"Se detectaron {balanceErrors.Count} línea(s) con descuadre de masa. " +
                $"Se ha creado una incidencia automática. Corrija los pesos antes de confirmar. " +
                $"Detalle: {string.Join(" | ", balanceErrors)}");
        }

        // ── 5. Construir cabecera del tratamiento ─────────────────────────────
        var treatment = new TreatmentPlant
        {
            Id                   = Guid.NewGuid(),
            IdWasteMove          = wm.Id,
            WasteMoveReference   = wm.WasteMoveReference,
            OwnerId              = wm.OwnerId,
            TicketScale          = request.TicketScale,
            PlantTreatmentDate   = request.PlantTreatmentDate,
            IdTreatmentOperation = request.IdTreatmentOperation,
            ServiceOrderId       = request.ServiceOrderId,
            ImproperWeight       = request.ImproperWeight,
            QualityMetricsJson   = request.QualityMetricsJson,
            TypeContainer        = request.TypeContainer,
            PriceContainer       = request.PriceContainer,
            IncidentId           = null,
            IdUser               = _currentUser.IdUser,
            DateCreateSys        = DateTime.UtcNow,
            DateModifiedSys      = DateTime.UtcNow,
        };

        // ── 6. Líneas de tratamiento ──────────────────────────────────────────
        foreach (var line in request.Lines)
        {
            treatment.TreatmentPlantResidues.Add(new TreatmentPlantResidue
            {
                Id               = Guid.NewGuid(),
                IdTreatmentPlant = treatment.Id,
                IdResidue        = line.IdResidue,
                Category         = line.Category,
                WeightTotal      = line.WeightTotal,
                MeasureUnit      = line.MeasureUnit,
                Units            = line.Units,
                PriceWeight      = line.PriceWeight,
                PriceUnit        = line.PriceUnit,
                IdResidueReused  = line.IdResidueReused,
                WeightReused     = line.WeightReused,
                MeasureUnitReused = line.MeasureUnitReused,
                UnitsReused      = line.UnitsReused,
                IdResidueValued  = line.IdResidueValued,
                WeightValued     = line.WeightValued,
                MeasureUnitValued = line.MeasureUnitValued,
                UnitsValued      = line.UnitsValued,
                IdResidueRemove  = line.IdResidueRemove,
                WeightRemove     = line.WeightRemove,
                MeasureUnitRemove = line.MeasureUnitRemove,
                UnitsRemove      = line.UnitsRemove,
            });
        }

        // ── 7. Transición de estado → CLASIFICADO ─────────────────────────────
        wm.ServiceStatus   = WasteMoveStatuses.Clasificado;
        wm.DateModifiedSys = DateTime.UtcNow;

        // ── 8. Completar las SOs vinculadas al traslado ───────────────────────
        var linkedSOs = await _context.ServiceOrders
            .Where(s => s.WasteMoveReference == wm.WasteMoveReference)
            .ToListAsync(ct);

        var completedAt = DateTime.UtcNow;
        foreach (var so in linkedSOs)
        {
            so.Status    = ServiceOrderStatuses.Completed;
            so.UpdatedAt = completedAt;
            so.IdUser    = _currentUser.IdUser;
        }

        _context.Add(treatment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "TreatmentPlant {TreatmentPlantId} creado para traslado {WasteMoveRef}. Estado → CLASIFICADO. {Count} SO(s) → Completed.",
            treatment.Id, wm.WasteMoveReference, linkedSOs.Count);

        return new CreateTreatmentPlantResult(treatment.Id, false, []);
    }
}
