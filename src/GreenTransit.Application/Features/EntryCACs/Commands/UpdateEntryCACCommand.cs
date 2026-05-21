using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EntryCACs.Commands;

// ── Input de línea ────────────────────────────────────────────────────────────

/// <summary>Línea de residuo para actualización. Id nulo = nueva línea.</summary>
public sealed record UpdateEntryCACLineInput(
    Guid?    Id,
    Guid     IdResidue,
    decimal  Weight,
    string   MeasureUnit,
    int?     Units,
    decimal? PriceWeight,
    decimal? PriceUnit
);

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Actualiza cabecera y líneas de una Entrada en CAC.
/// Solo permitido mientras el WasteMove vinculado esté en estado EN CAC.
/// </summary>
public sealed record UpdateEntryCACCommand(
    Guid                      Id,
    DateTime                  CACEntryDate,
    string?                   TypeContainer,
    decimal?                  PriceContainer,
    string?                   CollectionMethod,
    UpdateEntryCACLineInput[] Lines
) : IRequest;

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateEntryCACCommandValidator : AbstractValidator<UpdateEntryCACCommand>
{
    public UpdateEntryCACCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CACEntryDate).NotEmpty()
            .LessThanOrEqualTo(_ => DateTime.Now.AddMinutes(5))
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

public sealed class UpdateEntryCACCommandHandler : IRequestHandler<UpdateEntryCACCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<UpdateEntryCACCommandHandler> _logger;

    public UpdateEntryCACCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        ILogger<UpdateEntryCACCommandHandler> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task Handle(UpdateEntryCACCommand request, CancellationToken ct)
    {
        // ── 1. Cargar entrada con líneas y el WasteMove vinculado ─────────────
        var entry = await _context.EntryCACs
            .Include(e => e.EntryCACResidues)
            .Include(e => e.WasteMove)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException(
                $"EntryCAC {request.Id} no encontrada.");

        // ── 2. Validar que el traslado aún está EN CAC ────────────────────────
        if (entry.WasteMove.ServiceStatus != WasteMoveStatuses.EnCAC)
            throw new InvalidOperationException(
                $"No se puede modificar la entrada porque el traslado está en " +
                $"estado '{entry.WasteMove.ServiceStatus}'. Se requiere '{WasteMoveStatuses.EnCAC}'.");

        // ── 3. Actualizar cabecera ────────────────────────────────────────────
        entry.CACEntryDate    = request.CACEntryDate;
        entry.TypeContainer   = request.TypeContainer;
        entry.PriceContainer  = request.PriceContainer;
        entry.CollectionMethod = request.CollectionMethod;
        entry.DateModifiedSys  = DateTime.UtcNow;

        // ── 4. Reconciliar líneas (upsert + eliminar huérfanas) ───────────────
        var incomingIds = request.Lines
            .Where(l => l.Id.HasValue)
            .Select(l => l.Id!.Value)
            .ToHashSet();

        // Eliminar líneas que ya no están en la petición
        var toRemove = entry.EntryCACResidues
            .Where(r => !incomingIds.Contains(r.Id))
            .ToList();
        foreach (var r in toRemove)
            entry.EntryCACResidues.Remove(r);

        foreach (var line in request.Lines)
        {
            if (line.Id.HasValue)
            {
                // Actualizar línea existente
                var existing = entry.EntryCACResidues
                    .First(r => r.Id == line.Id.Value);
                existing.IdResidue   = line.IdResidue;
                existing.Weight      = line.Weight;
                existing.MeasureUnit = line.MeasureUnit;
                existing.Units       = line.Units;
                existing.PriceWeight = line.PriceWeight;
                existing.PriceUnit   = line.PriceUnit;
            }
            else
            {
                // Nueva línea
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
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "EntryCAC {EntryCACId} actualizada por usuario {IdUser}.",
            entry.Id, _currentUser.IdUser);
    }
}
