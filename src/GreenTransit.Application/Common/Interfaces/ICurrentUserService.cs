using System.Security.Claims;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Provee el contexto del usuario autenticado para multi-tenant, autorización y auditoría.
/// Implementado en GreenTransit.Web con IHttpContextAccessor.
/// Los valores se leen de los claims gt_* emitidos por ClaimsTransformation.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>True si el usuario está autenticado Y existe en la tabla Users (gt_user_found=true).</summary>
    bool IsAuthenticated { get; }

    /// <summary>Users.ID (int). Devuelve 0 si no autenticado.</summary>
    int IdUser { get; }

    /// <summary>Users.Login.</summary>
    string Login { get; }

    /// <summary>Users.Email.</summary>
    string Email { get; }

    /// <summary>Nombre para mostrar — Users.CompleteName o Email.</summary>
    string UserName { get; }

    /// <summary>Users.OwnerId (multi-tenant). Devuelve Guid.Empty si no hay tenant.</summary>
    Guid OwnerId { get; }

    /// <summary>Users.IdProfile (int).</summary>
    int ProfileId { get; }

    /// <summary>Profiles.Reference (ej: "ADMIN", "SCRAP", "PRODUCER"…).</summary>
    string UserProfile { get; }

    /// <summary>
    /// Alias de UserProfile. Devuelve Profiles.Reference para alinearse con ProfileConstants.
    /// Usar este nombre en el nuevo código de autorización basado en policies.
    /// </summary>
    string ProfileReference => UserProfile;

    /// <summary>Id de la Entidad activa vinculada al usuario por Email. Null si no existe.</summary>
    Guid? LinkedEntityId { get; }

    /// <summary>True si el usuario tiene exactamente el perfil indicado (case-insensitive).</summary>
    bool IsInProfile(string profileRef);

    /// <summary>True si el usuario tiene alguno de los perfiles indicados (case-insensitive).</summary>
    bool IsInAnyProfile(params string[] profileRefs);

    /// <summary>
    /// Inicializa el contexto de usuario en modo interactivo Blazor Server, donde
    /// IHttpContextAccessor.HttpContext es null (conexión WebSocket, no HTTP).
    /// Debe llamarse desde MainLayout.OnInitializedAsync con el ClaimsPrincipal
    /// obtenido del CascadingParameter Task&lt;AuthenticationState&gt;.
    /// </summary>
    void SetInteractiveUser(ClaimsPrincipal user);
}

