using System.Security.Claims;
using GreenTransit.Application.Common;
using GreenTransit.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace GreenTransit.Web.Auth;

/// <summary>
/// Transforma los claims del token OIDC en claims internos de GreenTransit
/// añadiendo IdUser y OwnerId desde la base de datos.
/// Se ejecuta automáticamente en cada request autenticado.
/// </summary>
public sealed class ClaimsTransformation : IClaimsTransformation
{
    private readonly IUserRepository _userRepository;

    public ClaimsTransformation(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Solo transforma identidades autenticadas
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        // Evita re-transformar si los claims internos ya están presentes
        if (principal.HasClaim(c => c.Type == AuthClaims.IdUser))
            return principal;

        // Obtiene el identificador externo del usuario (claim 'sub')
        var sub = principal.FindFirstValue(AuthClaims.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(sub))
            return principal;

        // Busca el usuario interno en la base de datos
        var user = await _userRepository.FindByLoginAsync(sub);
        if (user is null)
            return principal;

        // Añade claims internos en una nueva identidad
        var identity = new ClaimsIdentity();

        identity.AddClaim(new Claim(
            AuthClaims.IdUser,
            user.Id.ToString(),
            ClaimValueTypes.Integer32));

        if (user.OwnerId.HasValue)
        {
            identity.AddClaim(new Claim(
                AuthClaims.OwnerId,
                user.OwnerId.Value.ToString()));
        }

        principal.AddIdentity(identity);
        return principal;
    }
}
