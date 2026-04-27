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
    /// Autenticación temporalmente deshabilitada en la app web: tratamos al usuario como anónimo.
    public bool IsAuthenticated => false;

    /// <inheritdoc/>
    public int IdUser => 0;

    /// <inheritdoc/>
    public Guid OwnerId => Guid.Empty;

    /// <inheritdoc/>
    public string Email => string.Empty;

    /// <inheritdoc/>
    public string UserName => "Anónimo";

    /// <inheritdoc/>
    /// Mientras la autenticación esté deshabilitada, se asume perfil ADMIN para desarrollo.
    public string UserProfile => "ADMIN";
}
