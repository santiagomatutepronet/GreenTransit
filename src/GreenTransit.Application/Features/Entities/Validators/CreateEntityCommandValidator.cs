using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Entities.Commands;
using GreenTransit.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Entities.Validators;

public sealed class CreateEntityCommandValidator
    : AbstractValidator<CreateEntityCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateEntityCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(256);

        RuleFor(x => x.EntityRole)
            .NotEmpty().WithMessage("El rol de la entidad es obligatorio.")
            .Must(r => EntityRoles.All.Contains(r))
            .WithMessage($"Rol inválido. Valores permitidos: {string.Join(", ", EntityRoles.All)}.");

        // NationalId único por EntityRole
        RuleFor(x => x)
            .MustAsync(NationalIdUniqueForRole)
            .When(x => !string.IsNullOrWhiteSpace(x.NationalId))
            .WithName("NationalId")
            .WithMessage("Ya existe una entidad con ese NIF/NationalId y el mismo rol.");

        // Coordenadas obligatorias para Plant y CAC
        RuleFor(x => x.Latitude)
            .NotEmpty().WithMessage("La latitud es obligatoria para Plant y CAC.")
            .When(x => EntityRoles.RequiresCoordinates(x.EntityRole));

        RuleFor(x => x.Longitude)
            .NotEmpty().WithMessage("La longitud es obligatoria para Plant y CAC.")
            .When(x => EntityRoles.RequiresCoordinates(x.EntityRole));

        // InscriptionNumber obligatorio para Carrier
        RuleFor(x => x.InscriptionNumber)
            .NotEmpty().WithMessage("El número de inscripción (RAT) es obligatorio para Carrier.")
            .When(x => EntityRoles.RequiresInscriptionNumber(x.EntityRole));

        // Email obligatorio cuando el rol genera usuario automático
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El Email es obligatorio para este rol (genera usuario automáticamente).")
            .EmailAddress()
            .When(x => EntityRoles.GetAutoUserProfile(x.EntityRole) is not null);
    }

    private async Task<bool> NationalIdUniqueForRole(
        CreateEntityCommand cmd, CancellationToken ct)
        => !await _context.BusinessEntities
            .AnyAsync(e =>
                e.NationalId  == cmd.NationalId &&
                e.EntityRole  == cmd.EntityRole, ct);
}
