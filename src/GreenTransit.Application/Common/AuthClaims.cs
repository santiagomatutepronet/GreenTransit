namespace GreenTransit.Application.Common;

/// <summary>Nombres de claims internos usados en el sistema GreenTransit.</summary>
public static class AuthClaims
{
    // ── Claims OIDC estándar (entrada) ────────────────────────────────────────
    /// <summary>Identificador único externo del usuario en el proveedor OIDC.</summary>
    public const string Sub = "sub";

    /// <summary>Claim organizativo del proveedor OIDC que contiene el OwnerId del tenant.</summary>
    public const string OrganizationOwnerId = "owner_id";

    // ── Claims internos GreenTransit (gt_*) ───────────────────────────────────
    /// <summary>Indica si el usuario autenticado existe en la tabla Users. "true" | "false".</summary>
    public const string UserFound = "gt_user_found";

    /// <summary>Identificador interno del usuario (int) — tabla Users.ID.</summary>
    public const string IdUser = "gt_user_id";

    /// <summary>OwnerId del tenant (Guid) — tabla Users.OwnerId.</summary>
    public const string OwnerId = "gt_owner_id";

    /// <summary>Nombre para mostrar del usuario — Users.CompleteName o Email.</summary>
    public const string UserName = "gt_user_name";

    /// <summary>Login del usuario — Users.Login.</summary>
    public const string Login = "gt_login";

    /// <summary>Email del usuario — Users.Email.</summary>
    public const string Email = "gt_email";

    /// <summary>IdProfile del usuario (int) — Users.IdProfile.</summary>
    public const string ProfileId = "gt_profile_id";

    /// <summary>Reference del perfil (p. ej. ADMIN, SCRAP, PRODUCER…) — Profiles.Reference.</summary>
    public const string Profile = "gt_profile_ref";

    /// <summary>Id de la Entidad vinculada al usuario (Guid) — Entities.Id.</summary>
    public const string EntityId = "gt_entity_id";
}
