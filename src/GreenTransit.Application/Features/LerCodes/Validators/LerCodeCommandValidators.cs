using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.LerCodes.Commands;

namespace GreenTransit.Application.Features.LerCodes.Validators;

public sealed class CreateLerCodeCommandValidator
    : AbstractValidator<CreateLerCodeCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateLerCodeCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código LER es obligatorio.")
            .Length(6).WithMessage("El código LER debe tener exactamente 6 dígitos.")
            .Matches(@"^\d{6}$").WithMessage("El código LER debe contener solo dígitos.")
            .MustAsync(BeUniqueCode).WithMessage("Ya existe un código LER con ese valor.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(512);

        RuleFor(x => x)
            .Must(ChapterCoherent)
            .When(x => !string.IsNullOrWhiteSpace(x.Chapter) && x.Code.Length == 6)
            .WithName("Chapter")
            .WithMessage("El capítulo debe coincidir con los dos primeros dígitos del código LER.");
    }

    private async Task<bool> BeUniqueCode(
        string code, CancellationToken ct)
        => !await _context.LerCodes.AnyAsync(l => l.Code == code, ct);

    private static bool ChapterCoherent(CreateLerCodeCommand cmd)
        => cmd.Chapter == cmd.Code[..2];
}

public sealed class UpdateLerCodeCommandValidator
    : AbstractValidator<UpdateLerCodeCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateLerCodeCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El código LER es obligatorio.")
            .Length(6).WithMessage("El código LER debe tener exactamente 6 dígitos.")
            .Matches(@"^\d{6}$").WithMessage("El código LER debe contener solo dígitos.")
            .MustAsync(BeUniqueCodeExcludingSelf)
            .WithMessage("Ya existe otro código LER con ese valor.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(512);

        RuleFor(x => x)
            .Must(ChapterCoherent)
            .When(x => !string.IsNullOrWhiteSpace(x.Chapter) && x.Code.Length == 6)
            .WithName("Chapter")
            .WithMessage("El capítulo debe coincidir con los dos primeros dígitos del código LER.");
    }

    private async Task<bool> BeUniqueCodeExcludingSelf(
        UpdateLerCodeCommand cmd, string code, CancellationToken ct)
        => !await _context.LerCodes.AnyAsync(l => l.Code == code && l.Id != cmd.Id, ct);

    private static bool ChapterCoherent(UpdateLerCodeCommand cmd)
        => cmd.Chapter == cmd.Code[..2];
}
