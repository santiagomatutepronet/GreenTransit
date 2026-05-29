using FluentValidation;
using GreenTransit.Application.Features.EcoDataNet.Commands;

namespace GreenTransit.Application.Features.EcoDataNet.Validators;

public class SaveExplorerLayoutConfigCommandValidator
    : AbstractValidator<SaveExplorerLayoutConfigCommand>
{
    public SaveExplorerLayoutConfigCommandValidator()
    {
        RuleFor(x => x.AssetId)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.ProviderParticipantId)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.DatasetName)
            .MaximumLength(256)
            .When(x => x.DatasetName is not null);

        RuleFor(x => x.Overrides)
            .NotNull();

        RuleForEach(x => x.Overrides).ChildRules(ov =>
        {
            ov.RuleFor(o => o.WidgetId)
              .NotEmpty();

            ov.RuleFor(o => o.CustomColumnSpan)
              .InclusiveBetween(1, 12)
              .When(o => o.CustomColumnSpan.HasValue);

            ov.RuleFor(o => o.CustomTitle)
              .MaximumLength(256)
              .When(o => o.CustomTitle is not null);

            ov.When(o => o.CustomChartBinding != null, () =>
            {
                ov.RuleFor(o => o.CustomChartBinding!.CustomCategoryField)
                  .MaximumLength(256)
                  .When(o => o.CustomChartBinding!.CustomCategoryField != null);

                ov.RuleFor(o => o.CustomChartBinding!.CustomValueFields)
                  .Must(fields => fields == null || fields.Count <= 10)
                  .WithMessage("Un gráfico no puede tener más de 10 series de valores.")
                  .When(o => o.CustomChartBinding!.CustomValueFields != null);

                ov.RuleForEach(o => o.CustomChartBinding!.CustomValueFields)
                  .MaximumLength(256)
                  .When(o => o.CustomChartBinding!.CustomValueFields != null);
            });

            ov.When(o => o.CustomTableColumns != null, () =>
            {
                ov.RuleFor(o => o.CustomTableColumns)
                  .Must(cols => cols == null || cols.Count <= 100)
                  .WithMessage("Una tabla no puede tener más de 100 overrides de columna.");

                ov.RuleForEach(o => o.CustomTableColumns!)
                  .ChildRules(col =>
                  {
                      col.RuleFor(c => c.PropertyName)
                         .NotEmpty()
                         .MaximumLength(256);

                      col.RuleFor(c => c.CustomWidth)
                         .InclusiveBetween(30, 2000)
                         .When(c => c.CustomWidth.HasValue)
                         .WithMessage("El ancho de columna debe estar entre 30 y 2000 píxeles.");
                  });
            });
        });
    }
}
