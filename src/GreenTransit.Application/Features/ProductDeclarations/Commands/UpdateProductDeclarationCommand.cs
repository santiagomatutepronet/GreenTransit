using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.ProductDeclarations.Commands;

// ── Actualizar declaración ────────────────────────────────────────────────────

public sealed record UpdateProductDeclarationCommand(
    Guid    Id,
    int?    Year,
    int?    Period,
    int?    Month,
    string? Type,
    string? Currency,
    string? Reference
) : IRequest;

public sealed class UpdateProductDeclarationCommandHandler
    : IRequestHandler<UpdateProductDeclarationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateProductDeclarationCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateProductDeclarationCommand request, CancellationToken ct)
    {
        var declaration = await _context.ProductDeclarations
            .FirstOrDefaultAsync(pd => pd.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Declaración {request.Id} no encontrada.");

        if (!ProductDeclaration.States.Editable.Contains(declaration.State ?? string.Empty))
            throw new InvalidOperationException(
                $"No se puede editar una declaración en estado '{declaration.State}'.");

        // PRODUCER: solo puede editar su propia declaración
        if (_currentUser.IsInProfile(ProfileConstants.Producer)
            && declaration.IdProducer != _currentUser.LinkedEntityId)
            throw new UnauthorizedAccessException(
                "No tienes permiso para editar esta declaración.");

        if (request.Year.HasValue)    declaration.Year      = request.Year;
        if (request.Period.HasValue)  declaration.Period    = request.Period;
        if (request.Month.HasValue)   declaration.Month     = request.Month;
        if (request.Type is not null) declaration.Type      = request.Type;
        if (request.Currency is not null) declaration.Currency  = request.Currency;
        if (request.Reference is not null) declaration.Reference = request.Reference;

        declaration.DateModifiedSys = DateTime.UtcNow;
        declaration.IdUser          = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);
    }
}

public sealed class UpdateProductDeclarationCommandValidator
    : AbstractValidator<UpdateProductDeclarationCommand>
{
    public UpdateProductDeclarationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("El Id de la declaración es obligatorio.");
        RuleFor(x => x.Year)
            .InclusiveBetween(2020, 2100)
            .When(x => x.Year.HasValue)
            .WithMessage("El año debe estar entre 2020 y 2100.");
    }
}
