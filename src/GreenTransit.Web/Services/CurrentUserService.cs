using System.Security.Claims;
using GreenTransit.Application.Common;
using GreenTransit.Application.Common.Interfaces;

namespace GreenTransit.Web.Services;

/// <summary>
/// Implementacion de ICurrentUserService que lee los claims gt_* del HttpContext.
/// Los claims son emitidos por ClaimsTransformation tras validar el usuario en la BD.
///
/// IMPORTANTE - Blazor Server tiene dos fases:
///   1. SSR pre-rendering: IHttpContextAccessor.HttpContext esta disponible -> lectura directa.
///   2. Modo interactivo (circuito WebSocket): HttpContext es NULL -> se usa _interactiveUser,
///      poblado por MainLayout.OnInitializedAsync via SetInteractiveUser(ClaimsPrincipal).
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private ClaimsPrincipal? _interactiveUser;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User ?? _interactiveUser;

    public void SetInteractiveUser(ClaimsPrincipal user) => _interactiveUser = user;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true &&
        User.FindFirstValue(AuthClaims.UserFound) == "true";

    public int IdUser =>
        int.TryParse(User?.FindFirstValue(AuthClaims.IdUser), out var id) ? id : 0;

    public string Login =>
        User?.FindFirstValue(AuthClaims.Login) ?? string.Empty;

    public string Email =>
        User?.FindFirstValue(AuthClaims.Email)
        ?? User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email")
        ?? string.Empty;

    public string UserName =>
        User?.FindFirstValue(AuthClaims.UserName)
        ?? User?.FindFirstValue(ClaimTypes.Name)
        ?? "Anonimo";

    public Guid OwnerId =>
        Guid.TryParse(User?.FindFirstValue(AuthClaims.OwnerId), out var g) ? g : Guid.Empty;

    public int ProfileId =>
        int.TryParse(User?.FindFirstValue(AuthClaims.ProfileId), out var pid) ? pid : 0;

    public string UserProfile =>
        User?.FindFirstValue(AuthClaims.Profile)
        ?? User?.FindFirstValue(ClaimTypes.Role)
        ?? string.Empty;

    public Guid? LinkedEntityId =>
        Guid.TryParse(User?.FindFirstValue(AuthClaims.EntityId), out var eid) ? eid : null;

    public bool IsInProfile(string profileRef) =>
        string.Equals(UserProfile, profileRef, StringComparison.OrdinalIgnoreCase);

    public bool IsInAnyProfile(params string[] profileRefs) =>
        profileRefs.Any(p => string.Equals(UserProfile, p, StringComparison.OrdinalIgnoreCase));
}
