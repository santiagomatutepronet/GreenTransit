using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.ProductDeclarations.Commands;

// ── Crear declaración ─────────────────────────────────────────────────────────

public sealed record CreateProductDeclarationCommand(
    Guid    IdProducer,
    int     Year,
    int     Period,
    int?    Month,
    string  Type,
    string? Currency,
    string? Reference
) : IRequest<Guid>;

public sealed class CreateProductDeclarationCommandHandler
    : IRequestHandler<CreateProductDeclarationCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateProductDeclarationCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateProductDeclarationCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.Producer))
            throw new UnauthorizedAccessException("No tienes permiso para crear declaraciones.");

        // PRODUCER: forzar su propia entidad
        var idProducer = _currentUser.IsInProfile(ProfileConstants.Producer)
            ? _currentUser.LinkedEntityId ?? request.IdProducer
            : request.IdProducer;

        // Verificar unicidad: no existe declaración activa con mismo productor+año+periodo+tipo
        var duplicate = await _context.ProductDeclarations
            .AnyAsync(pd => pd.IdProducer == idProducer
                         && pd.Year       == request.Year
                         && pd.Period     == request.Period
                         && pd.Type       == request.Type
                         && pd.State      != ProductDeclaration.States.Rejected, ct);

        if (duplicate)
            throw new InvalidOperationException(
                "Ya existe una declaración activa con el mismo productor, año, periodo y tipo.");

        var now = DateTime.UtcNow;
        var declaration = new ProductDeclaration
        {
            Id              = Guid.NewGuid(),
            OwnerId         = _currentUser.OwnerId,
            IdProducer      = idProducer,
            Year            = request.Year,
            Period          = request.Period,
            Month           = request.Month,
            Type            = request.Type,
            Currency        = request.Currency ?? "EUR",
            Reference       = request.Reference,
            State           = ProductDeclaration.States.Draft,
            DateCreate      = now,
            DateCreateSys   = now,
            DateModifiedSys = now,
            IdUser          = _currentUser.IdUser
        };

        _context.Add(declaration);
        await _context.SaveChangesAsync(ct);

        return declaration.Id;
    }
}

public sealed class CreateProductDeclarationCommandValidator
    : AbstractValidator<CreateProductDeclarationCommand>
{
    public CreateProductDeclarationCommandValidator()
    {
        RuleFor(x => x.IdProducer).NotEmpty().WithMessage("El productor es obligatorio.");
        RuleFor(x => x.Year)
            .InclusiveBetween(2020, 2100)
            .WithMessage("El año debe estar entre 2020 y 2100.");
        RuleFor(x => x.Period).NotEmpty().WithMessage("El periodo es obligatorio.");
        RuleFor(x => x.Type).NotEmpty().WithMessage("El tipo de declaración es obligatorio.");
    }
}
