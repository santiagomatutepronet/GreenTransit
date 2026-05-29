namespace GreenTransit.Domain.Authorization;

/// <summary>
/// Constantes que representan los valores del campo Reference en la tabla Profiles.
/// Deben coincidir exactamente con los registros seed insertados en DbInitializer.
/// </summary>
public static class ProfileConstants
{
    public const string Admin           = "ADMIN";
    public const string Scrap           = "SCRAP";
    public const string Producer        = "PRODUCER";
    public const string Carrier         = "CARRIER";
    public const string PlantOp         = "PLANT_OP";
    public const string CacOp           = "CAC_OP";
    public const string PublicEnt       = "PUBLIC_ENT";
    public const string Coordinator     = "COORDINATOR";
    public const string DispatchOffice  = "DISPATCH_OFFICE";
    public const string Regulator       = "REGULATOR";
    public const string Certifier       = "CERTIFIER";

    /// <summary>Todos los perfiles del sistema.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Admin, Scrap, Producer, Carrier,
        PlantOp, CacOp, PublicEnt, Coordinator, DispatchOffice,
        Regulator, Certifier
    ];
}
