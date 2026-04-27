using System.Security.Claims;
using GreenTransit.Application.Common;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
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
    private readonly ILogger<ClaimsTransformation> _logger;

    public ClaimsTransformation(IUserRepository userRepository, ILogger<ClaimsTransformation> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        _logger.LogDebug("TransformAsync iniciado. IsAuthenticated={IsAuthenticated}",
            principal.Identity?.IsAuthenticated);

        // Solo transforma identidades autenticadas
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        // Evita re-transformar si los claims internos ya están presentes
        if (principal.HasClaim(c => c.Type == AuthClaims.IdUser))
            return principal;

        // Obtiene el identificador externo del usuario (claim 'sub')
        var sub = principal.FindFirstValue(AuthClaims.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        _logger.LogDebug("TransformAsync: sub={Sub}", sub);

        if (string.IsNullOrEmpty(sub))
        {
            _logger.LogWarning("TransformAsync: sub claim no encontrado en el principal");
            return principal;
        }

        // Busca el usuario interno en la base de datos
        AppUser? user;
        try
        {
            user = await _userRepository.FindByLoginAsync(sub);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TransformAsync: error al buscar usuario con login={Sub}", sub);
            return principal;
        }

        if (user is null)
        {
            _logger.LogWarning("TransformAsync: usuario no encontrado para login={Sub}", sub);
            return principal;
        }

        // Añade claims internos a la identidad autenticada para evitar crear
        // identidades "anónimas" (sin AuthenticationType) que pueden interferir
        // con el esquema de cookies y provocar bucles de redirección.
        var identity = principal.Identities.FirstOrDefault(i => i.IsAuthenticated)
                       ?? principal.Identity as ClaimsIdentity;

        if (identity is null)
            return principal;

        identity.AddClaim(new Claim(
            AuthClaims.IdUser,
            user.Id.ToString(),
            ClaimValueTypes.String));

        if (user.OwnerId.HasValue)
        {
            identity.AddClaim(new Claim(
                AuthClaims.OwnerId,
                user.OwnerId.Value.ToString()));
        }

        // Nombre para mostrar (CompleteName → Email → Login como fallback)
        var displayName = user.CompleteName
                          ?? user.Email
                          ?? sub;
        identity.AddClaim(new Claim(AuthClaims.UserName, displayName));
        identity.AddClaim(new Claim(ClaimTypes.Name, displayName));

        // Perfil / rol interno → habilita AuthorizeView Roles="ADMIN" etc.
        if (!string.IsNullOrEmpty(user.Profile?.Reference))
        {
            identity.AddClaim(new Claim(AuthClaims.Profile,   user.Profile.Reference));
            identity.AddClaim(new Claim(ClaimTypes.Role,      user.Profile.Reference));
        }
        return principal;
    }
}
