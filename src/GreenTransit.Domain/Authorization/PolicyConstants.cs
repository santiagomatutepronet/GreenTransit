namespace GreenTransit.Domain.Authorization;

/// <summary>
/// Constantes con los nombres de las policies de autorización de ASP.NET Core.
/// Cada policy se registra en Program.cs y se referencia en [Authorize(Policy = ...)].
/// </summary>
public static class PolicyConstants
{
    // ─── Maestros ────────────────────────────────────────────────────────────

    /// <summary>CRUD de Entidades: DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanManageEntities = "CanManageEntities";

    /// <summary>C+R de Entidades restringido a su ámbito: SCRAP.</summary>
    public const string CanCreateEntitiesRestricted = "CanCreateEntitiesRestricted";

    /// <summary>CRUD del catálogo LER: ADMIN.</summary>
    public const string CanManageLER = "CanManageLER";

    /// <summary>CRUD de Residuos (tipo Waste y operativo): DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanManageResidues = "CanManageResidues";

    /// <summary>CRUD-P de Residuos propios (Product/ProductSpec): PRODUCER.</summary>
    public const string CanManageOwnResidues = "CanManageOwnResidues";

    /// <summary>CRUD del catálogo de Operaciones de Tratamiento R/D: ADMIN.</summary>
    public const string CanManageTreatmentOps = "CanManageTreatmentOps";

    // ─── Operaciones ─────────────────────────────────────────────────────────

    /// <summary>CRUD de Órdenes de Servicio: DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanManageServiceOrders = "CanManageServiceOrders";

    /// <summary>CRUD-P de Órdenes de Servicio propias: PRODUCER, PUBLIC_ENT.</summary>
    public const string CanCreateOwnServiceOrders = "CanCreateOwnServiceOrders";

    /// <summary>CRUD de Traslados: DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanManageWasteMoves = "CanManageWasteMoves";

    /// <summary>Actualización de traslados asignados: CARRIER (solo sus propios).</summary>
    public const string CanUpdateAssignedMoves = "CanUpdateAssignedMoves";

    /// <summary>CRUD de Entradas en Planta: PLANT_OP (propias), ADMIN (todas).</summary>
    public const string CanManageEntryPlants = "CanManageEntryPlants";

    /// <summary>CRUD de Entradas en CAC: CAC_OP (propias), ADMIN (todas).</summary>
    public const string CanManageEntryCACs = "CanManageEntryCACs";

    /// <summary>CRUD de Tratamientos en Planta: PLANT_OP (propios), ADMIN (todos).</summary>
    public const string CanManageTreatments = "CanManageTreatments";

    // ─── Sostenibilidad ───────────────────────────────────────────────────────

    /// <summary>Apertura de incidencias: todos los perfiles autenticados.</summary>
    public const string CanCreateIncidents = "CanCreateIncidents";

    /// <summary>Resolución/cierre/eliminación de incidencias: DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanResolveIncidents = "CanResolveIncidents";

    /// <summary>CRUD de Zonas DUM y reglas de restricción: ADMIN.</summary>
    public const string CanManageDUMZones = "CanManageDUMZones";

    /// <summary>CRUD de consumo energético de planta: PLANT_OP (propia), ADMIN.</summary>
    public const string CanManagePlantEnergy = "CanManagePlantEnergy";

    /// <summary>CRUD de conjuntos de factores de emisión: ADMIN.</summary>
    public const string CanManageEmissionFactors = "CanManageEmissionFactors";

    // ─── Contratación y economía ─────────────────────────────────────────────

    /// <summary>CRUD de Acuerdos: SCRAP (propios), ADMIN.</summary>
    public const string CanManageAgreements = "CanManageAgreements";

    /// <summary>CRUD de Liquidaciones: SCRAP (validador), ADMIN.</summary>
    public const string CanManageSettlements = "CanManageSettlements";

    // ─── Reporting ────────────────────────────────────────────────────────────

    /// <summary>Lectura de KPIs regulatorios: SCRAP, PUBLIC_ENT, PLANT_OP, COORDINATOR, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewKPIs = "CanViewKPIs";

    /// <summary>Acceso a módulo de reporting y trazabilidad: todos los autenticados (con filtrado por datos propios).</summary>
    public const string CanViewReporting = "CanViewReporting";

    // ─── Seguridad ────────────────────────────────────────────────────────────

    /// <summary>CRUD de Usuarios del tenant: ADMIN.</summary>
    public const string CanManageUsers = "CanManageUsers";

    /// <summary>CRUD de Perfiles: ADMIN.</summary>
    public const string CanManageProfiles = "CanManageProfiles";

    /// <summary>Lectura restringida de usuarios del propio ámbito: SCRAP.</summary>
    public const string CanViewOwnUsers = "CanViewOwnUsers";

    // ─── Declaraciones de Producción ─────────────────────────────────────────

    /// <summary>Ver declaraciones: ADMIN, PRODUCER, SCRAP, COORDINATOR.</summary>
    public const string CanViewProductDeclarations = "CanViewProductDeclarations";

    /// <summary>Crear/editar propias declaraciones: PRODUCER, ADMIN.</summary>
    public const string CanManageProductDeclarations = "CanManageProductDeclarations";

    /// <summary>Validar o rechazar declaraciones: solo ADMIN.</summary>
    public const string CanValidateProductDeclarations = "CanValidateProductDeclarations";

    /// <summary>Gestión de diccionarios de declaraciones: solo ADMIN.</summary>
    public const string CanManageDeclarationDicts = "CanManageDeclarationDicts";
}
