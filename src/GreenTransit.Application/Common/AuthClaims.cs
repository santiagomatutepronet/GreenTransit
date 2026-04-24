namespace GreenTransit.Application.Common;

/// <summary>Nombres de claims internos usados en el sistema GreenTransit.</summary>
public static class AuthClaims
{
    /// <summary>Identificador interno del usuario (int, mapeado desde la tabla Users).</summary>
    public const string IdUser = "greentransit:iduser";

    /// <summary>OwnerId del tenant (Guid, extraído del claim organizativo del token OIDC).</summary>
    public const string OwnerId = "greentransit:ownerid";

    /// <summary>Nombre para mostrar del usuario (CompleteName de la tabla Users).</summary>
    public const string UserName = "greentransit:username";

    /// <summary>Reference del perfil del usuario (p. ej. ADMIN, SCRAP, PRODUCER…).</summary>
    public const string Profile = "greentransit:profile";

    /// <summary>Nombre del claim estándar 'sub' del proveedor OIDC.</summary>
    public const string Sub = "sub";

    /// <summary>Claim organizativo del proveedor OIDC que contiene el OwnerId del tenant.</summary>
    public const string OrganizationOwnerId = "owner_id";
}
