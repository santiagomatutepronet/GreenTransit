using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Repositorio para acceder a los usuarios del sistema.
/// Implementado en GreenTransit.Infrastructure.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Busca un usuario por su Login (claim 'sub' del proveedor OIDC).
    /// Devuelve null si el usuario no existe en la base de datos.
    /// </summary>
    Task<AppUser?> FindByLoginAsync(string login, CancellationToken cancellationToken = default);
}
