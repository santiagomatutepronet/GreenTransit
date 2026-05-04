using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Provisiona automáticamente un registro en la tabla Users cuando se crea
/// una BusinessEntity con un EntityRole que tiene perfil mapeado.
/// </summary>
public interface IEntityUserProvisioningService
{
    /// <summary>
    /// Crea el registro Users para la entidad dada si su EntityRole tiene perfil asociado.
    /// Debe llamarse dentro de la misma transacción que crea la entidad, antes del SaveChanges.
    /// </summary>
    /// <param name="entity">La entidad recién creada (todavía no guardada).</param>
    /// <param name="suggestedLogin">Login alternativo al email (opcional).</param>
    /// <param name="ct">CancellationToken.</param>
    /// <returns>
    /// El Users.ID del registro creado o ya existente, o null si el EntityRole no genera usuario.
    /// </returns>
    Task<int?> ProvisionUserForEntityAsync(
        BusinessEntity entity,
        string? suggestedLogin = null,
        CancellationToken ct = default);
}
