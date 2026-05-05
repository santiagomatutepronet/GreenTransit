using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Security.Commands;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Security.Validators;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateUserCommandValidator(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;

        RuleFor(x => x.Login)
            .NotEmpty().WithMessage("El login es obligatorio.")
            .MaximumLength(256)
            .MustAsync(LoginUniqueForOwner)
            .WithMessage("Ya existe un usuario con ese login en este tenant.");

        RuleFor(x => x.Email)
            .MaximumLength(256)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("El email no tiene un formato válido.");

        RuleFor(x => x.IdProfile)
            .GreaterThan(0).WithMessage("Debe seleccionar un perfil.");
    }

    private async Task<bool> LoginUniqueForOwner(
        CreateUserCommand command, string login, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        return !await _context.AppUsers
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Login == login &&
                           (ownerId == Guid.Empty ? u.OwnerId == null : u.OwnerId == ownerId), ct);
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateUserCommandValidator(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;

        RuleFor(x => x.Login)
            .NotEmpty().WithMessage("El login es obligatorio.")
            .MaximumLength(256)
            .MustAsync(LoginUniqueForOwnerExcludingSelf)
            .WithMessage("Ya existe un usuario con ese login en este tenant.");

        RuleFor(x => x.Email)
            .MaximumLength(256)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("El email no tiene un formato válido.");

        RuleFor(x => x.IdProfile)
            .GreaterThan(0).WithMessage("Debe seleccionar un perfil.");
    }

    private async Task<bool> LoginUniqueForOwnerExcludingSelf(
        UpdateUserCommand command, string login, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        return !await _context.AppUsers
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Login == login &&
                           (ownerId == Guid.Empty ? u.OwnerId == null : u.OwnerId == ownerId) &&
                           u.Id != command.Id, ct);
    }
}
