using Microsoft.AspNetCore.Authorization;

namespace GreenTransit.Infrastructure.Authorization;

/// <summary>
/// Requisito de autorización basado en perfil de usuario.
/// Una policy cumple este requisito si el perfil del usuario autenticado
/// está incluido en AllowedProfiles.
/// Un único ProfileAuthorizationHandler evalúa todas las policies que usen este requisito.
/// </summary>
public sealed class ProfileRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Lista de Profiles.Reference que tienen permitido el acceso.
    /// Los valores deben coincidir con ProfileConstants (ej: "ADMIN", "SCRAP"…).
    /// </summary>
    public IReadOnlyList<string> AllowedProfiles { get; }

    /// <param name="allowedProfiles">Uno o más valores de Profiles.Reference permitidos.</param>
    public ProfileRequirement(params string[] allowedProfiles)
    {
        AllowedProfiles = allowedProfiles ?? [];
    }
}
