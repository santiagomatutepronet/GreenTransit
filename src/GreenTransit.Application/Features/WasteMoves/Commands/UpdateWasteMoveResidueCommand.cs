using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.WasteMoves.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

public sealed record UpdateWasteMoveResidueCommand(
    Guid      Id,                        // PK de WasteMoveResidue
    decimal?  Weight,
    string?   MeasureUnit,
    int?      Units,
    decimal?  UnitPriceKg,
    Guid?     IdTreatmentOperationDestiny,
    DateTime? DateDelivery
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateWasteMoveResidueCommandHandler
    : IRequestHandler<UpdateWasteMoveResidueCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateWasteMoveResidueCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateWasteMoveResidueCommand request, CancellationToken ct)
    {
        var line = await _context.WasteMoveResidues
            .Include(r => r.WasteMove)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Línea de residuo {request.Id} no encontrada.");

        if (!WasteMoveStatuses.Editable.Contains(line.WasteMove.ServiceStatus ?? ""))
            throw new InvalidOperationException(
                $"No se puede editar una línea de un traslado en estado '{line.WasteMove.ServiceStatus}'.");

        line.Weight                     = request.Weight;
        line.MeasureUnit                = request.MeasureUnit;
        line.Units                      = request.Units;
        line.UnitPriceKg                = request.UnitPriceKg;
        line.IdTreatmentOperationDestiny = request.IdTreatmentOperationDestiny;
        line.DateDelivery               = request.DateDelivery;

        line.WasteMove.DateModifiedSys = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}

// ── Validador ─────────────────────────────────────────────────────────────────

public sealed class UpdateWasteMoveResidueCommandValidator
    : AbstractValidator<UpdateWasteMoveResidueCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateWasteMoveResidueCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.Weight)
            .GreaterThan(0)
            .When(x => x.Weight.HasValue)
            .WithMessage("El peso debe ser mayor que cero.");

        RuleFor(x => x.Units)
            .GreaterThan(0)
            .When(x => x.Units.HasValue)
            .WithMessage("Las unidades deben ser mayor que cero.");

        // Si el residuo es peligroso o RAEE, IdTreatmentOperationDestiny es obligatorio
        RuleFor(x => x.IdTreatmentOperationDestiny)
            .MustAsync(TreatmentOperationRequiredForDangerousAsync)
            .WithMessage("La operación de tratamiento destino es obligatoria para residuos peligrosos o RAEE.");
    }

    private async Task<bool> TreatmentOperationRequiredForDangerousAsync(
        UpdateWasteMoveResidueCommand cmd,
        Guid? idTreatmentOperation,
        CancellationToken ct)
    {
        var line = await _context.WasteMoveResidues
            .AsNoTracking()
            .Include(r => r.Residue)
            .FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);

        if (line?.Residue is null) return true;

        var requiresOperation = line.Residue.IsDangerous || line.Residue.IsRAEE;

        return !requiresOperation || idTreatmentOperation.HasValue;
    }
}
