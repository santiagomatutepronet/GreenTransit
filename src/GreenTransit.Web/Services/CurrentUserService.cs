using System.Security.Claims;
using GreenTransit.Application.Common;
using GreenTransit.Application.Common.Interfaces;

namespace GreenTransit.Web.Services;

/// <summary>
/// Implementación de ICurrentUserService que lee los claims gt_* del HttpContext.
/// Los claims son emitidos por ClaimsTransformation tras validar el usuario en la BD.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc/>
    /// True solo si está autenticado OIDC Y el claim gt_user_found = "true"
    /// (es decir, el usuario existe en la tabla Users).
    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true &&
        User.FindFirstValue(AuthClaims.UserFound) == "true";

    /// <inheritdoc/>
    public int IdUser =>
        int.TryParse(User?.FindFirstValue(AuthClaims.IdUser), out var id) ? id : 0;

    /// <inheritdoc/>
    public string Login =>
        User?.FindFirstValue(AuthClaims.Login) ?? string.Empty;

    /// <inheritdoc/>
    public string Email =>
        User?.FindFirstValue(AuthClaims.Email)
        ?? User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email")
        ?? string.Empty;

    /// <inheritdoc/>
    public string UserName =>
        User?.FindFirstValue(AuthClaims.UserName)
        ?? User?.FindFirstValue(ClaimTypes.Name)
        ?? "Anónimo";

    /// <inheritdoc/>
    public Guid OwnerId =>
        Guid.TryParse(User?.FindFirstValue(AuthClaims.OwnerId), out var g) ? g : Guid.Empty;

    /// <inheritdoc/>
    public int ProfileId =>
        int.TryParse(User?.FindFirstValue(AuthClaims.ProfileId), out var pid) ? pid : 0;

    /// <inheritdoc/>
    public string UserProfile =>
        User?.FindFirstValue(AuthClaims.Profile)
        ?? User?.FindFirstValue(ClaimTypes.Role)
        ?? string.Empty;

    /// <inheritdoc/>
    public Guid? LinkedEntityId =>
        Guid.TryParse(User?.FindFirstValue(AuthClaims.EntityId), out var eid) ? eid : null;

    /// <inheritdoc/>
    public bool IsInProfile(string profileRef) =>
        string.Equals(UserProfile, profileRef, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool IsInAnyProfile(params string[] profileRefs) =>
        profileRefs.Any(p => string.Equals(UserProfile, p, StringComparison.OrdinalIgnoreCase));
}
