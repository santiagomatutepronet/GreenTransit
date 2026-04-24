namespace GreenTransit.Domain.Constants;

/// <summary>
/// Valores válidos del campo EntityRole (tabla Entities).
/// Valores: Source | Destination | Carrier | OperatorTransfer | SCRAP |
///          Producer | Plant | CAC | PublicEntity | Coordinator | Other
/// </summary>
public static class EntityRoles
{
    public const string Source           = "Source";
    public const string Destination      = "Destination";
    public const string Carrier          = "Carrier";
    public const string OperatorTransfer = "OperatorTransfer";
    public const string SCRAP            = "SCRAP";
    public const string Producer         = "Producer";
    public const string Plant            = "Plant";
    public const string CAC              = "CAC";
    public const string PublicEntity     = "PublicEntity";
    public const string Coordinator      = "Coordinator";
    public const string Other            = "Other";

    public static readonly IReadOnlyList<string> All =
    [
        Source, Destination, Carrier, OperatorTransfer, SCRAP,
        Producer, Plant, CAC, PublicEntity, Coordinator, Other
    ];

    /// <summary>
    /// Devuelve el Reference del perfil de usuario a crear automáticamente,
    /// o null si el rol no genera usuario.
    /// Mapeo: SCRAP→SCRAP, Producer→PRODUCER, Carrier→CARRIER, Plant→PLANT_OP,
    ///        CAC→CAC_OP, PublicEntity→PUBLIC_ENT, Coordinator→COORDINATOR,
    ///        OperatorTransfer→CARRIER. Source/Destination/Other→null.
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
        _                => null
    };

    /// <summary>Roles que requieren Latitude y Longitude obligatorios.</summary>
    public static bool RequiresCoordinates(string role) =>
        role is Plant or CAC;

    /// <summary>Roles que requieren InscriptionNumber obligatorio.</summary>
    public static bool RequiresInscriptionNumber(string role) =>
        role is Carrier;
}
