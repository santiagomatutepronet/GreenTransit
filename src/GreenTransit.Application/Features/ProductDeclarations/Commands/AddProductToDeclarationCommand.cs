using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ProductDeclarations.Commands;

// ── Añadir línea de producto ──────────────────────────────────────────────────

public sealed record AddProductToDeclarationCommand(
    Guid     IdProductDeclaration,
    Guid?    IdResidue,
    string?  Reference,
    string?  Source,
    string?  ProductUse,
    string?  ProductCategory,
    decimal  Quantity,
    string?  MeasureUnit,
    int?     Units,
    decimal? Price
) : IRequest<Guid>;

public sealed class AddProductToDeclarationCommandHandler
    : IRequestHandler<AddProductToDeclarationCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public AddProductToDeclarationCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(AddProductToDeclarationCommand request, CancellationToken ct)
    {
        var declaration = await _context.ProductDeclarations
            .Include(pd => pd.Products)
            .FirstOrDefaultAsync(pd => pd.Id == request.IdProductDeclaration, ct)
            ?? throw new KeyNotFoundException(
                $"Declaración {request.IdProductDeclaration} no encontrada.");

        if (!ProductDeclaration.States.Editable.Contains(declaration.State ?? string.Empty))
            throw new InvalidOperationException(
                $"No se pueden añadir líneas a una declaración en estado '{declaration.State}'.");

        if (_currentUser.IsInProfile(ProfileConstants.Producer)
            && declaration.IdProducer != _currentUser.LinkedEntityId)
            throw new UnauthorizedAccessException(
                "No tienes permiso para modificar esta declaración.");

        // Validar residuo solo si se proporciona
        if (request.IdResidue.HasValue && request.IdResidue.Value != Guid.Empty)
        {
            var residueExists = await _context.Residues
                .AnyAsync(r => r.Id == request.IdResidue.Value && r.ResidueType == "Product", ct);
            if (!residueExists)
                throw new KeyNotFoundException(
                    $"Residuo {request.IdResidue} no encontrado o no es de tipo 'Product'.");
        }

        var product = new Product
        {
            Id                    = Guid.NewGuid(),
            IdProductDeclaration  = request.IdProductDeclaration,
            IdResidue             = request.IdResidue,
            Reference             = request.Reference,
            Source                = request.Source,
            ProductUse            = request.ProductUse,
            ProductCategory       = request.ProductCategory,
            Quantity              = request.Quantity,
            MeasureUnit           = request.MeasureUnit,
            Units                 = request.Units,
            Price                 = request.Price
        };

        _context.Products.Add(product);

        // EF fix-up añade product a declaration.Products automáticamente.
        // Se recalcula el total sobre la colección completa (incluyendo la nueva línea).
        declaration.Amount          = declaration.Products.Sum(p => (p.Quantity ?? 0) * (p.Price ?? 0));
        declaration.DateModifiedSys = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return product.Id;
    }
}

public sealed class AddProductToDeclarationCommandValidator
    : AbstractValidator<AddProductToDeclarationCommand>
{
    public AddProductToDeclarationCommandValidator()
    {
        RuleFor(x => x.IdProductDeclaration).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor que 0.");
    }
}
