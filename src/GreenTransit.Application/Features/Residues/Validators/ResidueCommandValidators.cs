using FluentValidation;
using GreenTransit.Application.Features.Residues.Commands;

namespace GreenTransit.Application.Features.Residues.Validators;

public sealed class CreateResidueCommandValidator
    : AbstractValidator<CreateResidueCommand>
{
    private static readonly string[] ValidTypes = ["Waste", "Product", "ProductSpec"];

    public CreateResidueCommandValidator()
    {
        RuleFor(x => x.ResidueType)
            .NotEmpty().WithMessage("El tipo de residuo es obligatorio.")
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("Tipo inválido. Valores permitidos: Waste, Product, ProductSpec.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(256);

        RuleFor(x => x.DangerousCode)
            .NotEmpty().WithMessage("El código de peligrosidad es obligatorio cuando IsDangerous = true.")
            .When(x => x.IsDangerous);

        RuleFor(x => x.IdProducer)
            .NotNull().WithMessage("El productor es obligatorio para fichas técnicas (ProductSpec).")
            .When(x => x.ResidueType == "ProductSpec");

        RuleFor(x => x.WeightPerUnitKg)
            .GreaterThan(0).When(x => x.WeightPerUnitKg.HasValue)
            .WithMessage("El peso por unidad debe ser mayor que 0.");

        RuleFor(x => x.RecycledContentPercent)
            .InclusiveBetween(0, 100)
            .When(x => x.RecycledContentPercent.HasValue)
            .WithMessage("El contenido reciclado debe estar entre 0 y 100.");
    }
}

public sealed class UpdateResidueCommandValidator
    : AbstractValidator<UpdateResidueCommand>
{
    private static readonly string[] ValidTypes = ["Waste", "Product", "ProductSpec"];

    public UpdateResidueCommandValidator()
    {
        RuleFor(x => x.ResidueType)
            .NotEmpty()
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("Tipo inválido. Valores permitidos: Waste, Product, ProductSpec.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(256);

        RuleFor(x => x.DangerousCode)
            .NotEmpty().WithMessage("El código de peligrosidad es obligatorio cuando IsDangerous = true.")
            .When(x => x.IsDangerous);

        RuleFor(x => x.IdProducer)
            .NotNull().WithMessage("El productor es obligatorio para fichas técnicas (ProductSpec).")
            .When(x => x.ResidueType == "ProductSpec");

        RuleFor(x => x.WeightPerUnitKg)
            .GreaterThan(0).When(x => x.WeightPerUnitKg.HasValue)
            .WithMessage("El peso por unidad debe ser mayor que 0.");

        RuleFor(x => x.RecycledContentPercent)
            .InclusiveBetween(0, 100)
            .When(x => x.RecycledContentPercent.HasValue)
            .WithMessage("El contenido reciclado debe estar entre 0 y 100.");
    }
}
