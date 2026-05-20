using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using MediatR;

namespace GreenTransit.Application.Features.WasteMoves.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

public sealed record UpdateWasteMoveCommand(
    Guid      Id,
    Guid      IdSource,
    Guid      IdDestination,
    Guid      IdScrap,
    Guid?     IdScrap2,
    DateTime? PlannedPickupStart,
    DateTime? PlannedPickupEnd,
    DateTime? PlannedDeliveryStart,
    DateTime? PlannedDeliveryEnd
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateWasteMoveCommandHandler
    : IRequestHandler<UpdateWasteMoveCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateWasteMoveCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task Handle(UpdateWasteMoveCommand request, CancellationToken ct)
    {
        var wm = await _context.WasteMoves
            .FirstOrDefaultAsync(w => w.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Traslado {request.Id} no encontrado.");

        if (!WasteMoveStatuses.Editable.Contains(wm.ServiceStatus ?? ""))
            throw new InvalidOperationException(
                $"No se puede editar un traslado en estado '{wm.ServiceStatus}'.");

        wm.IdSource             = request.IdSource;
        wm.IdDestination        = request.IdDestination;
        wm.IdScrap              = request.IdScrap;
        wm.IdScrap2             = request.IdScrap2;
        wm.PlannedPickupStart   = request.PlannedPickupStart;
        wm.PlannedPickupEnd     = request.PlannedPickupEnd;
        wm.PlannedDeliveryStart = request.PlannedDeliveryStart;
        wm.PlannedDeliveryEnd   = request.PlannedDeliveryEnd;
        wm.DateModifiedSys      = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}

// ── Validador ─────────────────────────────────────────────────────────────────

public sealed class UpdateWasteMoveCommandValidator
    : AbstractValidator<UpdateWasteMoveCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateWasteMoveCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.IdSource)
            .MustAsync((id, ct) => EntityHasRoleAsync(id, WasteMoveSourceRoles.Valid, ct))
            .WithMessage($"El origen debe tener uno de los roles: {string.Join(", ", WasteMoveSourceRoles.Valid)}.");

        RuleFor(x => x.IdDestination)
            .MustAsync((id, ct) => EntityHasRoleAsync(id, WasteMoveDestinationRoles.Valid, ct))
            .WithMessage($"El destino debe tener uno de los roles: {string.Join(", ", WasteMoveDestinationRoles.Valid)}.");

        RuleFor(x => x.IdScrap)
            .MustAsync((id, ct) => EntityHasRoleAsync(id, [EntityRoles.SCRAP], ct))
            .WithMessage("El campo SCRAP debe corresponder a una entidad con rol SCRAP.");

        RuleFor(x => x)
            .Must(x => x.PlannedPickupEnd is null || x.PlannedPickupStart is null
                    || x.PlannedPickupEnd > x.PlannedPickupStart)
            .WithMessage("La fecha fin de recogida debe ser posterior a la fecha inicio.")
            .OverridePropertyName(nameof(UpdateWasteMoveCommand.PlannedPickupEnd));

        RuleFor(x => x)
            .Must(x => x.PlannedDeliveryStart is null || x.PlannedPickupEnd is null
                    || x.PlannedDeliveryStart >= x.PlannedPickupEnd)
            .WithMessage("La fecha inicio de entrega debe ser igual o posterior al fin de recogida.")
            .OverridePropertyName(nameof(UpdateWasteMoveCommand.PlannedDeliveryStart));
    }

    private async Task<bool> EntityHasRoleAsync(
        Guid id, IReadOnlyList<string> roles, CancellationToken ct)
    {
        return await _context.BusinessEntities
            .AnyAsync(e => e.Id == id && roles.Contains(e.EntityRole) && e.IsActive, ct);
    }
}
