using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EntryPlants.Commands;

// ── Input de línea ────────────────────────────────────────────────────────────

/// <summary>Datos de una línea de residuo para la entrada en planta.</summary>
public sealed record CreateEntryPlantLineInput(
    Guid     IdResidue,
    decimal  Weight,
    string   MeasureUnit,
    int?     Units,
    decimal? PriceWeight,
    decimal? PriceUnit
);

// ── Resultado con información de incidencia ───────────────────────────────────

/// <summary>Resultado del comando con el Id creado y aviso de descuadre si procede.</summary>
public sealed record CreateEntryPlantResult(
    Guid    EntryPlantId,
    bool    WeightDiscrepancyDetected,
    decimal NetWeight,
    string? DiscrepancyMessage
);

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Registra una Entrada en Planta con pesaje.
/// Transiciona el WasteMove de RECOGIDO|EN_CAC → EN_PLANTA.
/// NetWeight se calcula siempre en backend (GrossWeight - TareWeight).
/// </summary>
public sealed record CreateEntryPlantCommand(
    Guid                         WasteMoveId,
    string                       TicketScale,
    string?                      WeighbridgeId,
    DateTime                     PlantEntryDate,
    decimal                      GrossWeight,
    decimal                      TareWeight,
    decimal?                     DirectNetWeight,
    string?                      TypeContainer,
    decimal?                     PriceContainer,
    Guid?                        ServiceOrderId,
    CreateEntryPlantLineInput[]  Lines
) : IRequest<CreateEntryPlantResult>;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateEntryPlantCommandValidator : AbstractValidator<CreateEntryPlantCommand>
{
    public CreateEntryPlantCommandValidator()
    {
        RuleFor(x => x.WasteMoveId).NotEmpty();
        RuleFor(x => x.TicketScale).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PlantEntryDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.Now.AddMinutes(5))
            .WithMessage("La fecha de entrada no puede ser futura.");
        RuleFor(x => x.GrossWeight)
            .GreaterThan(0)
            .WithMessage("El peso bruto debe ser mayor que 0.")
            .When(x => x.DirectNetWeight is null);
        RuleFor(x => x.TareWeight)
            .GreaterThan(0)
            .WithMessage("El peso tara debe ser mayor que 0.")
            .When(x => x.DirectNetWeight is null);
        RuleFor(x => x)
            .Must(x => x.GrossWeight > x.TareWeight)
            .WithName("GrossWeight")
            .WithMessage("El peso bruto debe ser mayor que el peso tara.")
            .When(x => x.DirectNetWeight is null);
        RuleFor(x => x.DirectNetWeight)
            .GreaterThan(0)
            .WithMessage("El peso neto directo debe ser mayor que 0.")
            .When(x => x.DirectNetWeight.HasValue);
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("Debe incluir al menos una línea de residuo.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.IdResidue).NotEmpty();
            line.RuleFor(l => l.Weight).GreaterThan(0);
            line.RuleFor(l => l.MeasureUnit).NotEmpty().MaximumLength(20);
        });
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateEntryPlantCommandHandler
    : IRequestHandler<CreateEntryPlantCommand, CreateEntryPlantResult>
{
    private const double DiscrepancyThreshold = 0.05; // 5 %

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<CreateEntryPlantCommandHandler> _logger;

    public CreateEntryPlantCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        ILogger<CreateEntryPlantCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<CreateEntryPlantResult> Handle(
        CreateEntryPlantCommand request, CancellationToken ct)
    {
        // ── 1. Cargar el traslado ──────────────────────────────────────────────
        var wm = await _context.WasteMoves
            .Include(w => w.WasteMoveResidues)
            .FirstOrDefaultAsync(w => w.Id == request.WasteMoveId, ct)
            ?? throw new KeyNotFoundException(
                $"Traslado {request.WasteMoveId} no encontrado.");

        // ── 2. Validar estado: debe ser RECOGIDO o EN_CAC ─────────────────────
        var validStatuses = new[] { WasteMoveStatuses.Recogido, WasteMoveStatuses.EnCAC };
        if (!validStatuses.Contains(wm.ServiceStatus))
            throw new InvalidOperationException(
                $"El traslado está en estado '{wm.ServiceStatus}'. " +
                $"Se requiere '{WasteMoveStatuses.Recogido}' o '{WasteMoveStatuses.EnCAC}'.");

        // ── 3. Calcular NetWeight en backend ──────────────────────────────────
        var netWeight = request.DirectNetWeight
            ?? (request.GrossWeight - request.TareWeight);

        // ── 4. Construir cabecera ─────────────────────────────────────────────
        var entry = new EntryPlant
        {
            Id                 = Guid.NewGuid(),
            IdWasteMove        = wm.Id,
            WasteMoveReference = wm.WasteMoveReference,
            OwnerId            = wm.OwnerId,
            TicketScale        = request.TicketScale,
            WeighbridgeId      = request.WeighbridgeId,
            PlantEntryDate     = request.PlantEntryDate,
            GrossWeight        = request.GrossWeight,
            TareWeight         = request.TareWeight,
            NetWeight          = netWeight,
            TypeContainer      = request.TypeContainer,
            PriceContainer     = request.PriceContainer,
            ServiceOrderId     = request.ServiceOrderId,
            IdUser             = _currentUser.IdUser,
            DateCreateSys      = DateTime.UtcNow,
            DateModifiedSys    = DateTime.UtcNow,
        };

        // ── 5. Líneas de residuo ──────────────────────────────────────────────
        foreach (var line in request.Lines)
        {
            entry.EntryPlantResidues.Add(new EntryPlantResidue
            {
                Id           = Guid.NewGuid(),
                IdEntryPlant = entry.Id,
                IdResidue    = line.IdResidue,
                Weight       = line.Weight,
                MeasureUnit  = line.MeasureUnit,
                Units        = line.Units,
                PriceWeight  = line.PriceWeight,
                PriceUnit    = line.PriceUnit,
            });
        }

        // ── 6. Detectar descuadre de peso ─────────────────────────────────────
        var estimatedWeight = wm.WasteMoveResidues.Sum(r => r.Weight ?? 0m);
        bool discrepancyDetected = false;
        string? discrepancyMessage = null;

        if (estimatedWeight > 0)
        {
            var diff = Math.Abs((double)(netWeight - estimatedWeight)) / (double)estimatedWeight;
            if (diff > DiscrepancyThreshold)
            {
                discrepancyDetected = true;
                discrepancyMessage =
                    $"NetWeight={netWeight:F2} kg difiere un {diff:P1} " +
                    $"respecto al peso estimado del traslado ({estimatedWeight:F2} kg). " +
                    $"Umbral: {DiscrepancyThreshold:P0}.";

                var incident = new Incident
                {
                    Id                 = Guid.NewGuid(),
                    OwnerId            = wm.OwnerId,
                    Type               = "WeightDiscrepancy",
                    Severity           = "Medium",
                    OpenedAt           = DateTime.UtcNow,
                    WasteMoveReference = wm.WasteMoveReference,
                    TicketScale        = request.TicketScale,
                    Description        = discrepancyMessage,
                    CreatedAt          = DateTime.UtcNow,
                    UpdatedAt          = DateTime.UtcNow,
                    IdUser             = _currentUser.IdUser,
                    Version            = 1,
                };
                _context.Incidents.Add(incident);

                _logger.LogWarning(
                    "Descuadre de peso detectado en traslado {WasteMoveRef}: {Message}",
                    wm.WasteMoveReference, discrepancyMessage);
            }
        }

        // ── 7. Transición de estado ───────────────────────────────────────────
        wm.ServiceStatus    = WasteMoveStatuses.EnPlanta;
        wm.DateModifiedSys  = DateTime.UtcNow;

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

        _context.EntryPlants.Add(entry);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "EntryPlant {Id} creada para traslado {Ref}. NetWeight={Net} kg. Estado→EN_PLANTA. {Count} SO(s) → Completed.",
            entry.Id, wm.WasteMoveReference, netWeight, linkedSOs.Count);

        return new CreateEntryPlantResult(
            entry.Id,
            discrepancyDetected,
            netWeight,
            discrepancyMessage);
    }
}
