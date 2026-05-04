using Microsoft.AspNetCore.Authorization;

namespace GreenTransit.Infrastructure.Authorization;

/// <summary>
/// Requisito de autorización para operaciones sobre datos propios (CRUD-P, R-P, U-P).
/// Permite a un handler verificar que el usuario:
///   1. Tiene un perfil incluido en AllowedProfiles.
///   2. Tiene una entidad vinculada (LinkedEntityId != null) cuando RequiresEntityLink = true.
///
/// NOTA: Este requisito SOLO valida el derecho a intentar la operación.
/// El filtrado efectivo de datos (WHERE IdCarrier = @entityId) se aplica en los
/// query/command handlers de MediatR usando ICurrentUserService.LinkedEntityId.
/// </summary>
public sealed class OwnDataRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Lista de Profiles.Reference que pueden operar sobre datos propios.
    /// </summary>
    public IReadOnlyList<string> AllowedProfiles { get; }

    /// <summary>
    /// Si true, el usuario debe tener una entidad vinculada (LinkedEntityId != null)
    /// para que el requisito se cumpla. Casos donde no se requiere: perfiles funcionales
    /// como DISPATCH_OFFICE o ADMIN que no tienen entidad asociada.
    /// </summary>
    public bool RequiresEntityLink { get; }

    /// <param name="requiresEntityLink">
    /// True para perfiles operativos (CARRIER, PLANT_OP, CAC_OP, PRODUCER, PUBLIC_ENT).
    /// False para perfiles funcionales sin entidad (DISPATCH_OFFICE, ADMIN).
    /// </param>
    /// <param name="allowedProfiles">Perfiles con acceso a datos propios.</param>
    public OwnDataRequirement(bool requiresEntityLink, params string[] allowedProfiles)
    {
        RequiresEntityLink = requiresEntityLink;
        AllowedProfiles    = allowedProfiles ?? [];
    }
}
