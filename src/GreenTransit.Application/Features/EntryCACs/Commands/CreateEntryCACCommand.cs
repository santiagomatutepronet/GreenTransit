using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EntryCACs.Commands;

// ── Input de línea ────────────────────────────────────────────────────────────

/// <summary>Datos de una línea de residuo para la entrada en CAC.</summary>
public sealed record CreateEntryCACLineInput(
    Guid     IdResidue,
    decimal  Weight,
    string   MeasureUnit,
    int?     Units,
    decimal? PriceWeight,
    decimal? PriceUnit
);

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Registra una Entrada en Centro de Acopio Ciudadano.
/// Transiciona el WasteMove de RECOGIDO → EN CAC.
/// </summary>
public sealed record CreateEntryCACCommand(
    Guid                      WasteMoveId,
    DateTime                  CACEntryDate,
    string?                   TypeContainer,
    decimal?                  PriceContainer,
    string?                   CollectionMethod,
    CreateEntryCACLineInput[] Lines
) : IRequest<Guid>;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreateEntryCACCommandValidator : AbstractValidator<CreateEntryCACCommand>
{
    public CreateEntryCACCommandValidator()
    {
        RuleFor(x => x.WasteMoveId).NotEmpty();
        RuleFor(x => x.CACEntryDate).NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
            .WithMessage("La fecha de entrada no puede ser futura.");
        RuleFor(x => x.Lines).NotEmpty()
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

public sealed class CreateEntryCACCommandHandler
    : IRequestHandler<CreateEntryCACCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<CreateEntryCACCommandHandler> _logger;

    public CreateEntryCACCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        ILogger<CreateEntryCACCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<Guid> Handle(CreateEntryCACCommand request, CancellationToken ct)
    {
        // ── 1. Cargar el traslado ──────────────────────────────────────────────
        var wm = await _context.WasteMoves
            .FirstOrDefaultAsync(w => w.Id == request.WasteMoveId, ct)
            ?? throw new KeyNotFoundException(
                $"Traslado {request.WasteMoveId} no encontrado.");

        // ── 2. Validar estado: debe estar RECOGIDO ────────────────────────────
        if (wm.ServiceStatus != WasteMoveStatuses.Recogido)
            throw new InvalidOperationException(
                $"El traslado está en estado '{wm.ServiceStatus}' y no puede " +
                $"registrarse una entrada en CAC. Se requiere '{WasteMoveStatuses.Recogido}'.");

        // ── 3. Construir cabecera heredando del WasteMove ─────────────────────
        var entry = new EntryCAC
        {
            Id                 = Guid.NewGuid(),
            IdWasteMove        = wm.Id,
            WasteMoveReference = wm.WasteMoveReference,
            OwnerId            = wm.OwnerId,
            CACEntryDate       = request.CACEntryDate,
            TypeContainer      = request.TypeContainer,
            PriceContainer     = request.PriceContainer,
            CollectionMethod   = request.CollectionMethod,
            IdUser             = _currentUser.IdUser,
            DateCreateSys      = DateTime.UtcNow,
            DateModifiedSys    = DateTime.UtcNow,
        };

        // ── 4. Líneas de residuo ──────────────────────────────────────────────
        foreach (var line in request.Lines)
        {
            entry.EntryCACResidues.Add(new EntryCACResidue
            {
                Id          = Guid.NewGuid(),
                IdEntryCAC  = entry.Id,
                IdResidue   = line.IdResidue,
                Weight      = line.Weight,
                MeasureUnit = line.MeasureUnit,
                Units       = line.Units,
                PriceWeight = line.PriceWeight,
                PriceUnit   = line.PriceUnit,
            });
        }

        // ── 5. Transición de estado ───────────────────────────────────────────
        wm.ServiceStatus = WasteMoveStatuses.EnCAC;

        _context.EntryCACs.Add(entry);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "EntryCAC {EntryCACId} creada para WasteMove {WasteMoveId} → estado EN_CAC.",
            entry.Id, wm.Id);

        return entry.Id;
    }
}
