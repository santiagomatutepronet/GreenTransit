using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;

namespace GreenTransit.Domain.Services;

/// <summary>
/// Gestiona las transiciones de estado de <see cref="ProductDeclaration"/>.
/// Transiciones permitidas:
///   Borrador   → Emitido   (requiere ≥1 Product, IdProducer, Year y Period informados)
///   Emitido    → Validado  (sin precondiciones adicionales)
///   Emitido    → Rechazado (requiere motivo no vacío)
///   Rechazado  → Borrador  (siempre permitido)
/// </summary>
public sealed class ProductDeclarationStateService
{
    /// <summary>Transiciona la declaración a <c>Emitido</c>.</summary>
    public ProductDeclaration Emit(ProductDeclaration declaration)
    {
        if (declaration.State != ProductDeclaration.States.Draft)
            throw new DomainException(
                $"Solo se puede emitir una declaración en estado '{ProductDeclaration.States.Draft}'. " +
                $"Estado actual: '{declaration.State}'.");

        if (!declaration.IdProducer.HasValue)
            throw new DomainException("La declaración debe tener un productor asignado antes de emitirse.");

        if (!declaration.Year.HasValue)
            throw new DomainException("La declaración debe tener el año informado antes de emitirse.");

        if (!declaration.Period.HasValue)
            throw new DomainException("La declaración debe tener el periodo informado antes de emitirse.");

        if (!declaration.Products.Any())
            throw new DomainException("La declaración debe tener al menos una línea de producto antes de emitirse.");

        declaration.State           = ProductDeclaration.States.Issued;
        declaration.DateEmit        = DateTime.UtcNow;
        declaration.DateModifiedSys = DateTime.UtcNow;

        return declaration;
    }

    /// <summary>Transiciona la declaración a <c>Validado</c>.</summary>
    public ProductDeclaration Validate(ProductDeclaration declaration)
    {
        if (declaration.State != ProductDeclaration.States.Issued)
            throw new DomainException(
                $"Solo se puede validar una declaración en estado '{ProductDeclaration.States.Issued}'. " +
                $"Estado actual: '{declaration.State}'.");

        declaration.State           = ProductDeclaration.States.Validated;
        declaration.DateModifiedSys = DateTime.UtcNow;

        return declaration;
    }

    /// <summary>Transiciona la declaración a <c>Rechazado</c>.</summary>
    public ProductDeclaration Reject(ProductDeclaration declaration, string reason)
    {
        if (declaration.State != ProductDeclaration.States.Issued)
            throw new DomainException(
                $"Solo se puede rechazar una declaración en estado '{ProductDeclaration.States.Issued}'. " +
                $"Estado actual: '{declaration.State}'.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("El motivo de rechazo es obligatorio.");

        declaration.State           = ProductDeclaration.States.Rejected;
        declaration.DateModifiedSys = DateTime.UtcNow;

        return declaration;
    }

    /// <summary>Transiciona la declaración de <c>Rechazado</c> a <c>Borrador</c>.</summary>
    public ProductDeclaration ReturnToDraft(ProductDeclaration declaration)
    {
        if (declaration.State != ProductDeclaration.States.Rejected)
            throw new DomainException(
                $"Solo se puede devolver a borrador una declaración en estado '{ProductDeclaration.States.Rejected}'. " +
                $"Estado actual: '{declaration.State}'.");

        declaration.State           = ProductDeclaration.States.Draft;
        declaration.DateModifiedSys = DateTime.UtcNow;

        return declaration;
    }
}
