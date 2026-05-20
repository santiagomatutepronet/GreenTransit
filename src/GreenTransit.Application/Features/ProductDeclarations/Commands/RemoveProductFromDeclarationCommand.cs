using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.ProductDeclarations.Commands;

// ── Eliminar línea de producto ────────────────────────────────────────────────

public sealed record RemoveProductFromDeclarationCommand(Guid Id) : IRequest;

public sealed class RemoveProductFromDeclarationCommandHandler
    : IRequestHandler<RemoveProductFromDeclarationCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public RemoveProductFromDeclarationCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(RemoveProductFromDeclarationCommand request, CancellationToken ct)
    {
        var product = await _context.Products
            .Include(p => p.ProductDeclaration)
                .ThenInclude(pd => pd.Products)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Línea de producto {request.Id} no encontrada.");

        var declaration = product.ProductDeclaration;

        if (!ProductDeclaration.States.Editable.Contains(declaration.State ?? string.Empty))
            throw new InvalidOperationException(
                $"No se pueden eliminar líneas de una declaración en estado '{declaration.State}'.");

        if (_currentUser.IsInProfile(ProfileConstants.Producer)
            && declaration.IdProducer != _currentUser.LinkedEntityId)
            throw new UnauthorizedAccessException(
                "No tienes permiso para modificar esta declaración.");

        _context.Remove(product);

        // Recalcular Amount de la cabecera
        var remaining = declaration.Products
            .Where(p => p.Id != request.Id)
            .ToList();
        declaration.Amount          = remaining.Sum(p => (p.Quantity ?? 0) * (p.Price ?? 0));
        declaration.DateModifiedSys = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}
