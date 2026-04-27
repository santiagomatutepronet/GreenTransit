using FluentValidation;
using GreenTransit.Application.Features.TreatmentOperations.Commands;

namespace GreenTransit.Application.Features.TreatmentOperations.Validators;

public sealed class CreateTreatmentOperationCommandValidator
    : AbstractValidator<CreateTreatmentOperationCommand>
{
    private static readonly string[] ValidTypes = ["Recovery", "Disposal"];

    public CreateTreatmentOperationCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(8).WithMessage("El código no puede superar 8 caracteres.");

        RuleFor(x => x.OperationType)
            .NotEmpty()
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("Tipo inválido. Valores permitidos: Recovery, Disposal.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(512);

        RuleFor(x => x.ShortDescription)
            .MaximumLength(128).When(x => x.ShortDescription is not null);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).When(x => x.SortOrder.HasValue);
    }
}

public sealed class UpdateTreatmentOperationCommandValidator
    : AbstractValidator<UpdateTreatmentOperationCommand>
{
    private static readonly string[] ValidTypes = ["Recovery", "Disposal"];

    public UpdateTreatmentOperationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(8).WithMessage("El código no puede superar 8 caracteres.");

        RuleFor(x => x.OperationType)
            .NotEmpty()
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("Tipo inválido. Valores permitidos: Recovery, Disposal.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(512);

        RuleFor(x => x.ShortDescription)
            .MaximumLength(128).When(x => x.ShortDescription is not null);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).When(x => x.SortOrder.HasValue);
    }
}
