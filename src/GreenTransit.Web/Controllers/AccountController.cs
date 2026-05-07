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
        var redirectUrl = Url.Content(
            string.IsNullOrEmpty(returnUrl) ? "~/dashboard" : returnUrl);
        return Challenge(
            new AuthenticationProperties { RedirectUri = redirectUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Cierra la sesión local (elimina la cookie de la app) y redirige
    /// a la página de confirmación. No redirige al IdP para evitar errores
    /// del endpoint de logout del servidor de identidad externo.
    /// </summary>
    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/signed-out");
    }
}
