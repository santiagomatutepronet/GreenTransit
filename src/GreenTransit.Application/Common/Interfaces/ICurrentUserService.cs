namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Provee el contexto del usuario autenticado para multi-tenant y auditoría.
/// Implementado en GreenTransit.Web con IHttpContextAccessor.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Indica si el usuario está autenticado.</summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Identificador interno del usuario (mapeo del claim 'sub' → tabla Users).
    /// Devuelve 0 si no está autenticado.
    /// </summary>
    int IdUser { get; }

    /// <summary>
    /// OwnerId del tenant activo (claim organizativo del token OIDC).
    /// Devuelve Guid.Empty si no está autenticado.
    /// </summary>
    Guid OwnerId { get; }

    /// <summary>Email del usuario autenticado (claim 'email' o 'preferred_username').</summary>
    string Email { get; }
}
