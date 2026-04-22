using System.Security.Claims;
using GreenTransit.Application.Common;
using GreenTransit.Application.Common.Interfaces;

namespace GreenTransit.Web.Services;

/// <summary>
/// Implementación de ICurrentUserService que resuelve el contexto del usuario
/// desde los claims del HttpContext (IHttpContextAccessor).
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
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc/>
    public int IdUser =>
        int.TryParse(User?.FindFirstValue(AuthClaims.IdUser), out var id) ? id : 0;

    /// <inheritdoc/>
    public Guid OwnerId =>
        Guid.TryParse(User?.FindFirstValue(AuthClaims.OwnerId), out var guid) ? guid : Guid.Empty;

    /// <inheritdoc/>
    public string Email =>
        User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email")
        ?? User?.FindFirstValue("preferred_username")
        ?? string.Empty;
}
