namespace GreenTransit.Domain.Constants;

/// <summary>
/// Valores válidos del campo EntityRole (tabla Entities).
/// Valores: Producer | OperatorTransfer | SCRAP | PublicEntity |
///          Carrier | CAC | Plant | Coordinator | Other
/// </summary>
public static class EntityRoles
{
    public const string Producer         = "Producer";
    public const string OperatorTransfer = "OperatorTransfer";
    public const string SCRAP            = "SCRAP";
    public const string PublicEntity     = "PublicEntity";
    public const string Carrier          = "Carrier";
    public const string CAC              = "CAC";
    public const string Plant            = "Plant";
    public const string Coordinator      = "Coordinator";
    public const string DispatchOffice   = "DISPATCH_OFFICE";
    public const string Other            = "Other";

    public static readonly IReadOnlyList<string> All =
    [
        Producer, OperatorTransfer, SCRAP, PublicEntity,
        Carrier, CAC, Plant, Coordinator, DispatchOffice, Other
    ];

    /// <summary>Etiqueta en español para mostrar en UI.</summary>
    public static string Label(string role) => role switch
    {
        Producer         => "Productor",
        OperatorTransfer => "Operador de traslado",
        SCRAP            => "SCRAP",
        PublicEntity     => "Entidad pública",
        Carrier          => "Transportista",
        CAC              => "CAC",
        Plant            => "Planta de tratamiento",
        Coordinator      => "Coordinador",
        DispatchOffice   => "Oficina de Asignación",
        Other            => "Otro",
        _                => role
    };

    /// <summary>
    /// Devuelve el Reference del perfil de usuario a crear automáticamente,
    /// o null si el rol no genera usuario.
    /// </summary>
    public static string? GetAutoUserProfile(string role) => role switch
    {
        SCRAP            => "SCRAP",
        Producer         => "PRODUCER",
        Carrier          => "CARRIER",
        OperatorTransfer => "CARRIER",
        Plant            => "PLANT_OP",
        CAC              => "CAC_OP",
        PublicEntity     => "PUBLIC_ENT",
        Coordinator      => "COORDINATOR",
        DispatchOffice   => "DISPATCH_OFFICE",
        _                => null
    };

    /// <summary>Roles que requieren Latitude y Longitude obligatorios.</summary>
    public static bool RequiresCoordinates(string role) =>
        role is Plant or CAC;

    /// <summary>Roles que requieren InscriptionNumber obligatorio.</summary>
    public static bool RequiresInscriptionNumber(string role) =>
        role is Carrier;
}
