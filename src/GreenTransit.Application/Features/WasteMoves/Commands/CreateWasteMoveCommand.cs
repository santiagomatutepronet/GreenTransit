using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.WasteMoves.Commands;

// ── Input de línea de residuo ─────────────────────────────────────────────────

/// <summary>Datos de una línea de residuo proporcionados por el usuario al crear el traslado.</summary>
public sealed record WasteMoveLineInput(
    Guid?    IdResidue,
    decimal? Weight,
    string?  MeasureUnit,
    int?     Units
);

// ── Comando ───────────────────────────────────────────────────────────────────

public sealed record CreateWasteMoveCommand(
    Guid[]               ServiceOrderIds,
    Guid                 IdSource,
    Guid                 IdDestination,
    Guid                 IdScrap,
    Guid?                IdScrap2,
    DateTime?            PlannedPickupStart,
    DateTime?            PlannedPickupEnd,
    DateTime?            PlannedDeliveryStart,
    DateTime?            PlannedDeliveryEnd,
    WasteMoveLineInput[] Lines
) : IRequest<Guid>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreateWasteMoveCommandHandler
    : IRequestHandler<CreateWasteMoveCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateWasteMoveCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        CreateWasteMoveCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // ── Cargar las SOs del tenant ────────────────────────────────────────
        var serviceOrders = await _context.ServiceOrders
            .Where(s => request.ServiceOrderIds.Contains(s.Id)
                     && s.OwnerId == ownerId)
            .ToListAsync(ct);

        if (serviceOrders.Count != request.ServiceOrderIds.Length)
            throw new InvalidOperationException(
                "Una o varias Órdenes de Servicio no existen o no pertenecen al tenant activo.");

        // ── Referencia única ─────────────────────────────────────────────────
        var reference = await GenerateReferenceAsync(ownerId, ct);

        var now = DateTime.UtcNow;

        // ── Entidad WasteMove ────────────────────────────────────────────────
        var wasteMove = new WasteMove
        {
            Id                   = Guid.NewGuid(),
            OwnerId              = ownerId,
            WasteMoveReference   = reference,
            ServiceStatus        = WasteMoveStatuses.Solicitado,
            IdSource             = request.IdSource,
            IdDestination        = request.IdDestination,
            IdScrap              = request.IdScrap,
            IdScrap2             = request.IdScrap2,
            ServiceOrderId       = serviceOrders[0].Id,
            RequestDate          = now,
            PlannedPickupStart   = request.PlannedPickupStart,
            PlannedPickupEnd     = request.PlannedPickupEnd,
            PlannedDeliveryStart = request.PlannedDeliveryStart,
            PlannedDeliveryEnd   = request.PlannedDeliveryEnd,
            DateCreateSys        = now,
            DateModifiedSys      = now,
            IdUser               = _currentUser.IdUser,
            Version              = 1
        };

        // ── Líneas de residuo — proporcionadas explícitamente por el usuario ────
        foreach (var line in request.Lines)
        {
            wasteMove.WasteMoveResidues.Add(new WasteMoveResidue
            {
                Id          = Guid.NewGuid(),
                IdWasteMove = wasteMove.Id,
                IdResidue   = line.IdResidue,
                Weight      = line.Weight,
                MeasureUnit = line.MeasureUnit,
                Units       = line.Units,
                IdTreatmentOperationDestiny = null
            });
        }

        // ── Vincular referencia en las SOs ───────────────────────────────────
        foreach (var so in serviceOrders)
            so.WasteMoveReference = reference;

        _context.WasteMoves.Add(wasteMove);
        await _context.SaveChangesAsync(ct);

        return wasteMove.Id;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GenerateReferenceAsync(Guid ownerId, CancellationToken ct)
    {
        var count = await _context.WasteMoves
            .CountAsync(w => w.OwnerId == ownerId, ct);

        return $"WM-{DateTime.UtcNow.Year}-{(count + 1):00000}";
    }
}

// ── Validador ─────────────────────────────────────────────────────────────────

public sealed class CreateWasteMoveCommandValidator
    : AbstractValidator<CreateWasteMoveCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateWasteMoveCommandValidator(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;

        RuleFor(x => x.ServiceOrderIds)
            .NotEmpty()
            .WithMessage("Debe seleccionar al menos una Orden de Servicio.");

        RuleFor(x => x.ServiceOrderIds)
            .MustAsync(ServiceOrdersAreEligibleAsync)
            .WithMessage("Todas las SOs deben estar en estado Pendiente o Planificada, pertenecer al tenant activo y no estar ya incluidas en otro traslado.");

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
            .OverridePropertyName(nameof(CreateWasteMoveCommand.PlannedPickupEnd));

        RuleFor(x => x)
            .Must(x => x.PlannedDeliveryStart is null || x.PlannedPickupEnd is null
                    || x.PlannedDeliveryStart >= x.PlannedPickupEnd)
            .WithMessage("La fecha inicio de entrega debe ser igual o posterior al fin de recogida.")
            .OverridePropertyName(nameof(CreateWasteMoveCommand.PlannedDeliveryStart));
    }

    private async Task<bool> ServiceOrdersAreEligibleAsync(
        Guid[] ids, CancellationToken ct)
    {
        var ownerId    = _currentUser.OwnerId;
        var validCount = await _context.ServiceOrders
            .CountAsync(s => ids.Contains(s.Id)
                          && s.OwnerId == ownerId
                          && (s.Status == ServiceOrderStatuses.Pending
                           || s.Status == ServiceOrderStatuses.Scheduled)
                          && s.WasteMoveReference == null, ct);

        return validCount == ids.Length;
    }

    private async Task<bool> EntityHasRoleAsync(
        Guid id, IReadOnlyList<string> roles, CancellationToken ct)
    {
        return await _context.BusinessEntities
            .AnyAsync(e => e.Id == id && roles.Contains(e.EntityRole) && e.IsActive, ct);
    }
}
