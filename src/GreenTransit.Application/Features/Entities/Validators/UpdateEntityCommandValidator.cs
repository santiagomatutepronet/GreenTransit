using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Entities.Commands;
using GreenTransit.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Entities.Validators;

public sealed class UpdateEntityCommandValidator
    : AbstractValidator<UpdateEntityCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateEntityCommandValidator(IApplicationDbContext context)
    {
        _context = context;

        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El Id de la entidad es obligatorio.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(256);

        RuleFor(x => x.EntityRole)
            .NotEmpty().WithMessage("El rol de la entidad es obligatorio.")
            .Must(r => EntityRoles.All.Contains(r))
            .WithMessage($"Rol inválido. Valores permitidos: {string.Join(", ", EntityRoles.All)}.");

        // NationalId único por EntityRole (excluye la propia entidad)
        RuleFor(x => x)
            .MustAsync(NationalIdUniqueForRole)
            .When(x => !string.IsNullOrWhiteSpace(x.NationalId))
            .WithName("NationalId")
            .WithMessage("Ya existe otra entidad con ese NIF/NationalId y el mismo rol.");

        RuleFor(x => x.Latitude)
            .NotEmpty().WithMessage("La latitud es obligatoria para Plant y CAC.")
            .When(x => EntityRoles.RequiresCoordinates(x.EntityRole));

        RuleFor(x => x.Longitude)
            .NotEmpty().WithMessage("La longitud es obligatoria para Plant y CAC.")
            .When(x => EntityRoles.RequiresCoordinates(x.EntityRole));

        RuleFor(x => x.InscriptionNumber)
            .NotEmpty().WithMessage("El número de inscripción (RAT) es obligatorio para Carrier.")
            .When(x => EntityRoles.RequiresInscriptionNumber(x.EntityRole));
    }

    private async Task<bool> NationalIdUniqueForRole(
        UpdateEntityCommand cmd, CancellationToken ct)
        => !await _context.BusinessEntities
            .AnyAsync(e =>
                e.Id         != cmd.Id &&
                e.NationalId == cmd.NationalId &&
                e.EntityRole == cmd.EntityRole, ct);
}
