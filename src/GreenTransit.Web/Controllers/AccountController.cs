using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace GreenTransit.Web.Controllers;

/// <summary>
/// Endpoints de autenticación que el middleware OIDC no puede manejar
/// directamente desde componentes Blazor (requieren redirecciones HTTP reales).
/// </summary>
[Route("account")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AccountController : Controller
{
    /// <summary>
    /// Inicia el flujo de login desafiando al proveedor OIDC.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        var redirectUrl = Url.Content(string.IsNullOrEmpty(returnUrl) ? "~/" : returnUrl);
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Cierra la sesión de cookies y del proveedor OIDC (Single Sign-Out).
    /// </summary>
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
