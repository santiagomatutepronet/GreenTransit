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
    /// Si se recibe prompt=login (p.ej. tras logout) se fuerza al IdP
    /// a pedir credenciales aunque tenga sesión activa.
    /// </summary>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl, [FromQuery] string? prompt)
    {
        var redirectUrl = Url.Content(
            string.IsNullOrEmpty(returnUrl) ? "~/dashboard" : returnUrl);

        var props = new AuthenticationProperties { RedirectUri = redirectUrl };

        if (!string.IsNullOrEmpty(prompt))
            props.Items["prompt"] = prompt;

        return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Elimina la sesión local (cookie) y redirige a /account/login,
    /// que emite el Challenge OIDC completo con client_id=GreenTransit.
    /// No se llama al end_session_endpoint del IdP porque el
    /// post_logout_redirect_uri no está registrado en el cliente externo
    /// y provocaría que el IdP mostrase su pantalla de login genérica.
    /// </summary>
    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // Redirigir al login con prompt=login para que el IdP pida
        // credenciales aunque tenga sesión SSO activa.
        return LocalRedirect("/account/login?prompt=login");
    }
}
