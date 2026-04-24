using FluentValidation;

namespace GreenTransit.Application.Features.ServiceOrders.Commands;

public sealed class CreateServiceOrderCommandValidator
    : AbstractValidator<CreateServiceOrderCommand>
{
    private static readonly string[] ValidStatuses   = ["Pending", "InProgress", "Completed", "Cancelled"];
    private static readonly string[] ValidPriorities = ["Low", "Normal", "High", "Critical"];

    public CreateServiceOrderCommandValidator()
    {
        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("OwnerId es obligatorio.");

        RuleFor(x => x.ServiceOrderNumber)
            .NotEmpty().WithMessage("El número de orden de servicio es obligatorio.")
            .MaximumLength(64).WithMessage("El número de orden no puede superar 64 caracteres.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("El estado es obligatorio.")
            .Must(s => ValidStatuses.Contains(s))
            .WithMessage($"Estado inválido. Valores permitidos: {string.Join(", ", ValidStatuses)}.");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("La prioridad es obligatoria.")
            .Must(p => ValidPriorities.Contains(p))
            .WithMessage($"Prioridad inválida. Valores permitidos: {string.Join(", ", ValidPriorities)}.");
    }
}
