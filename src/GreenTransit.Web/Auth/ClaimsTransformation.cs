using System.Security.Claims;
using GreenTransit.Application.Common;
using GreenTransit.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;

namespace GreenTransit.Web.Auth;

/// <summary>
/// Enriquece el ClaimsPrincipal OIDC con los datos del usuario de la tabla Users.
/// Se ejecuta automáticamente en cada request autenticado (una vez por sesión gracias
/// al guard de re-transformación).
///
/// Claims emitidos:
///   gt_user_found  → "true" si el usuario existe en la BD, "false" si no
///   gt_user_id     → Users.ID (int)
///   gt_login       → Users.Login
///   gt_email       → Users.Email
///   gt_user_name   → Users.CompleteName o Email
///   gt_profile_id  → Users.IdProfile (int)
///   gt_profile_ref → Profiles.Reference (ADMIN, SCRAP, PRODUCER…)
///   gt_owner_id    → Users.OwnerId (Guid, si existe)
///   gt_entity_id   → Entities.Id de la entidad activa con Email = Users.Email (si existe)
/// </summary>
public sealed class ClaimsTransformation : IClaimsTransformation
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ClaimsTransformation> _logger;

    public ClaimsTransformation(
        IUserRepository userRepository,
        ILogger<ClaimsTransformation> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        // Guard: no re-transformar si ya se procesó en este request
        if (principal.HasClaim(c => c.Type == AuthClaims.UserFound))
            return principal;

        // ═══ DIAGNÓSTICO: volcar TODOS los claims del token OIDC ═══
        _logger.LogInformation("═══ ClaimsTransformation: Inicio ═══");
        _logger.LogInformation("¿Autenticado? {IsAuth} | AuthenticationType: {Type}",
            principal.Identity?.IsAuthenticated, principal.Identity?.AuthenticationType);

        foreach (var c in principal.Claims)
            _logger.LogInformation("  Claim: {Type} = {Value}", c.Type, c.Value);

        // Intentar extraer el identificador del usuario probando todos los claims posibles
        var sub               = principal.FindFirstValue(AuthClaims.Sub);
        var email             = principal.FindFirstValue("email")
                                ?? principal.FindFirstValue(ClaimTypes.Email);
        var preferredUsername = principal.FindFirstValue("preferred_username");
        var login = sub
                    ?? email
                    ?? preferredUsername
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue(ClaimTypes.Name)
                    ?? principal.FindFirstValue("name")
                    ?? principal.FindFirstValue("unique_name");

        _logger.LogInformation(
            "Identificadores extraídos → sub='{Sub}' email='{Email}' preferred_username='{PU}' → login a usar='{Login}'",
            sub ?? "∅", email ?? "∅", preferredUsername ?? "∅", login ?? "*** NINGUNO ***");

        if (string.IsNullOrEmpty(login))
        {
            _logger.LogWarning("❌ No se encontró ningún claim con email/identificador. Claims disponibles: {Claims}",
                string.Join(", ", principal.Claims.Select(c => c.Type)));
            return AddUserNotFoundClaim(principal);
        }

        // 1er intento: buscar por Login = sub (GUID del IdP)
        _logger.LogInformation("Buscando en Users WHERE Login = '{Login}'...", login);
        var user = await _userRepository.FindByLoginAsync(login);

        // 2º intento: buscar por Email = email del token
        if (user is null && !string.IsNullOrEmpty(email) && email != login)
        {
            _logger.LogInformation("No encontrado por Login='{Login}'. Reintentando por Email='{Email}'...", login, email);
            user = await _userRepository.FindByEmailAsync(email);
        }

        // 3er intento: buscar por Login = email del token
        // (caso habitual: el Login en BD contiene el email, no el sub GUID del IdP)
        if (user is null && !string.IsNullOrEmpty(email))
        {
            _logger.LogInformation("No encontrado por Email='{Email}'. Reintentando por Login='{Email}'...", email);
            user = await _userRepository.FindByLoginAsync(email);
            if (user is not null)
                _logger.LogInformation("✅ Encontrado por Login=email. Considera actualizar Users.Login al sub del IdP: '{Sub}'", sub);
        }

        if (user is null)
        {
            _logger.LogWarning(
                "❌ Usuario NO encontrado en BD. sub='{Sub}' email='{Email}'. " +
                "Comprueba que Users.Login coincide con el claim 'sub' del IdP, " +
                "o que Users.Email coincide con el email del token.",
                sub, email);

            // Listar usuarios existentes para comparar (solo diagnóstico)
            var existingLogins = await _userRepository.GetAllLoginsAsync();
            _logger.LogInformation("Usuarios existentes en BD (Login | Email): {Users}",
                string.Join(" | ", existingLogins));

            return AddUserNotFoundClaim(principal);
        }

        _logger.LogInformation("✅ Usuario encontrado: ID={Id} Login={Login} Perfil={Profile} OwnerId={OwnerId}",
            user.Id, user.Login, user.Profile?.Reference ?? "SIN PERFIL", user.OwnerId);

        var identity = principal.Identities.FirstOrDefault(i => i.IsAuthenticated)
                       ?? principal.Identity as ClaimsIdentity;

        if (identity is null)
            return AddUserNotFoundClaim(principal);

        // ── Claims de usuario ─────────────────────────────────────────────────
        identity.AddClaim(new Claim(AuthClaims.UserFound,  "true"));
        identity.AddClaim(new Claim(AuthClaims.IdUser,     user.Id.ToString(), ClaimValueTypes.Integer));
        identity.AddClaim(new Claim(AuthClaims.Login,      user.Login));
        identity.AddClaim(new Claim(ClaimTypes.Name,       user.CompleteName ?? user.Email ?? user.Login));
        identity.AddClaim(new Claim(AuthClaims.UserName,   user.CompleteName ?? user.Email ?? user.Login));

        if (!string.IsNullOrEmpty(user.Email))
            identity.AddClaim(new Claim(AuthClaims.Email, user.Email));

        // ── Claims de perfil ──────────────────────────────────────────────────
        identity.AddClaim(new Claim(AuthClaims.ProfileId, user.IdProfile.ToString(), ClaimValueTypes.Integer));

        if (!string.IsNullOrEmpty(user.Profile?.Reference))
        {
            identity.AddClaim(new Claim(AuthClaims.Profile,  user.Profile.Reference));
            identity.AddClaim(new Claim(ClaimTypes.Role,     user.Profile.Reference));
        }

        // ── Claim de tenant (OwnerId) ─────────────────────────────────────────
        if (user.OwnerId.HasValue)
            identity.AddClaim(new Claim(AuthClaims.OwnerId, user.OwnerId.Value.ToString()));

        // ── Claim de entidad vinculada (segunda consulta, sólo si hay email) ──
        if (!string.IsNullOrEmpty(user.Email))
        {
            var entityId = await _userRepository.FindEntityIdByEmailAsync(user.Email);
            if (entityId.HasValue)
                identity.AddClaim(new Claim(AuthClaims.EntityId, entityId.Value.ToString()));
        }

        _logger.LogDebug(
            "ClaimsTransformation: usuario={Login} perfil={Profile} tenant={OwnerId}",
            user.Login, user.Profile?.Reference, user.OwnerId);

        return principal;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static ClaimsPrincipal AddUserNotFoundClaim(ClaimsPrincipal principal)
    {
        var identity = principal.Identities.FirstOrDefault(i => i.IsAuthenticated)
                       ?? principal.Identity as ClaimsIdentity;

        identity?.AddClaim(new Claim(AuthClaims.UserFound, "false"));
        return principal;
    }
}
