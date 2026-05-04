using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Repositorio para acceder a los usuarios del sistema.
/// Implementado en GreenTransit.Infrastructure.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Busca un usuario (incluyendo su perfil) por Login.
    /// Devuelve null si el usuario no existe.
    /// </summary>
    Task<AppUser?> FindByLoginAsync(string login, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un usuario (incluyendo su perfil) por Email.
    /// Devuelve null si el usuario no existe.
    /// </summary>
    Task<AppUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca el Id de la entidad activa (Entities) cuyo Email coincide con el email del usuario.
    /// Devuelve null si no existe entidad vinculada.
    /// </summary>
    Task<Guid?> FindEntityIdByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve una lista de cadenas "Login|Email" de los primeros 10 usuarios para diagnóstico.
    /// </summary>
    Task<IEnumerable<string>> GetAllLoginsAsync(CancellationToken cancellationToken = default);
}
