using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ServiceOrders.Commands;

public sealed class CreateServiceOrderCommandValidator
    : AbstractValidator<CreateServiceOrderCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateServiceOrderCommandValidator(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => ServiceOrderStatuses.All.Contains(s))
            .WithMessage($"Estado inválido. Permitidos: {string.Join(", ", ServiceOrderStatuses.All)}.");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(p => ServiceOrderPriorities.All.Contains(p))
            .WithMessage($"Prioridad inválida. Permitidas: {string.Join(", ", ServiceOrderPriorities.All)}.");

        // Número único por tenant (solo si se proporciona manualmente)
        RuleFor(x => x.ServiceOrderNumber)
            .MaximumLength(64)
            .MustAsync(NumberUniqueAsync)
            .When(x => !string.IsNullOrWhiteSpace(x.ServiceOrderNumber))
            .WithMessage("Ya existe una Orden de Servicio con ese número en este tenant.");

        // IdPickupPoint debe existir y pertenecer al mismo OwnerId (via BusinessEntity)
        RuleFor(x => x.IdPickupPoint)
            .MustAsync(PickupPointExistsAsync)
            .When(x => x.IdPickupPoint.HasValue)
            .WithMessage("El punto de recogida no existe o no pertenece a este ecosistema.");

        // Consistencia de fechas
        RuleFor(x => x)
            .Must(x => x.PlannedPickupEnd is null || x.PlannedPickupStart is null
                    || x.PlannedPickupEnd > x.PlannedPickupStart)
            .WithMessage("La fecha fin de recogida debe ser posterior a la fecha inicio.")
            .OverridePropertyName(nameof(CreateServiceOrderCommand.PlannedPickupEnd));

        RuleFor(x => x)
            .Must(x => x.PlannedDeliveryStart is null || x.PlannedPickupEnd is null
                    || x.PlannedDeliveryStart >= x.PlannedPickupEnd)
            .WithMessage("La fecha inicio de entrega debe ser igual o posterior al fin de recogida.")
            .OverridePropertyName(nameof(CreateServiceOrderCommand.PlannedDeliveryStart));
    }

    private async Task<bool> NumberUniqueAsync(
        string number, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        return !await _context.ServiceOrders
            .AnyAsync(s => s.OwnerId == ownerId && s.ServiceOrderNumber == number, ct);
    }

    private async Task<bool> PickupPointExistsAsync(
        Guid? id, CancellationToken ct)
    {
        if (!id.HasValue) return true;
        return await _context.BusinessEntities
            .AnyAsync(e => e.Id == id.Value && e.IsActive, ct);
    }
}

