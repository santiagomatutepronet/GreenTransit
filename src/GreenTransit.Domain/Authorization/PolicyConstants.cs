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

    // ─── Huella de Carbono (HC) ───────────────────────────────────────────────

    /// <summary>Dashboard HC-A — Visión Consolidada de Huella de Carbono: SCRAP, COORDINATOR, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewCarbonFootprintOverview = "CanViewCarbonFootprintOverview";

    /// <summary>Dashboard HC-B — Análisis de Emisiones del Transporte: SCRAP, COORDINATOR, DISPATCH_OFFICE, CARRIER, ADMIN.</summary>
    public const string CanViewCarbonTransportAnalysis = "CanViewCarbonTransportAnalysis";

    /// <summary>Dashboard HC-C — Huella Energética de Plantas: PLANT_OP, SCRAP, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewCarbonPlantEnergy = "CanViewCarbonPlantEnergy";

    /// <summary>Dashboard HC-D — Reporte de Huella para Productores: PRODUCER, ADMIN.</summary>
    public const string CanViewCarbonProducerReport = "CanViewCarbonProducerReport";

    /// <summary>Dashboard HC-E — Vista de Emisiones para Entidades Públicas: PUBLIC_ENT, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewCarbonPublicView = "CanViewCarbonPublicView";

    // ─── Mapas de Calor ───────────────────────────────────────────────────────

    /// <summary>Dashboard HM-A — Mapa de Calor de Densidad de Residuos: SCRAP, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewHeatMapWasteDensity = "CanViewHeatMapWasteDensity";

    /// <summary>Dashboard HM-B — Análisis de Patrones y Estacionalidad: SCRAP, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewHeatMapPatternAnalysis = "CanViewHeatMapPatternAnalysis";

    /// <summary>Dashboard HM-C — Vista de Mapas de Calor para Entidades Públicas: PUBLIC_ENT, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewHeatMapPublicView = "CanViewHeatMapPublicView";

    // ─── Cumplimiento Normativo (CN) ──────────────────────────────────────────

    /// <summary>Dashboard CN-A — Panel de Cumplimiento Normativo — Visión SCRAP: SCRAP, ADMIN.</summary>
    public const string CanViewScrapComplianceOverview = "CanViewScrapComplianceOverview";

    /// <summary>Dashboard CN-B — Auditoría de Cuotas de Mercado — Reparto entre SCRAPs: COORDINATOR, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewMarketShareAudit = "CanViewMarketShareAudit";

    /// <summary>Dashboard CN-C — Monitorización de Convenios — Coordinador: COORDINATOR, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewAgreementComplianceMonitoring = "CanViewAgreementComplianceMonitoring";

    /// <summary>Dashboard CN-D — Cumplimiento Normativo — Entidad Pública: PUBLIC_ENT, ADMIN.</summary>
    public const string CanViewPublicEntityComplianceView = "CanViewPublicEntityComplianceView";

    /// <summary>Dashboard CN-E — Datos de Cumplimiento — Oficina de Asignación: DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewDispatchOfficeComplianceData = "CanViewDispatchOfficeComplianceData";

    /// <summary>Acceso a módulo de reporting y trazabilidad: todos los autenticados (con filtrado por datos propios).</summary>
    public const string CanViewReporting = "CanViewReporting";

    // ─── Seguridad ────────────────────────────────────────────────────────────

    /// <summary>CRUD de Usuarios del tenant: ADMIN.</summary>
    public const string CanManageUsers = "CanManageUsers";

    /// <summary>CRUD de Perfiles: ADMIN.</summary>
    public const string CanManageProfiles = "CanManageProfiles";

    /// <summary>Gestión de permisos por pantalla: solo ADMIN.</summary>
    public const string CanManagePagePermissions = "CanManagePagePermissions";

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

    // ─── Cuotas de mercado ────────────────────────────────────────────────────

    /// <summary>Ver cuotas de mercado y cumplimiento: SCRAP, ADMIN.</summary>
    public const string CanViewMarketShares = "CanViewMarketShares";

    /// <summary>CRUD de cuotas de mercado: solo ADMIN.</summary>
    public const string CanManageMarketShares = "CanManageMarketShares";

    // ─── Movilidad Urbana (UC3) ───────────────────────────────────────────────

    /// <summary>Dashboard UC3-A — Análisis de Impacto en Movilidad — Coordinador: COORDINATOR, ADMIN.</summary>
    public const string CanViewMobilityCoordinatorAnalysis = "CanViewMobilityCoordinatorAnalysis";

    /// <summary>Dashboard UC3-B — Monitorización de Movilidad — Ayuntamiento: PUBLIC_ENT, ADMIN.</summary>
    public const string CanViewMobilityMunicipalMonitoring = "CanViewMobilityMunicipalMonitoring";

    /// <summary>Vista UC3-C — Datos de Impacto RAEE en Movilidad — Oficina de Asignación: DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewMobilityDispatchData = "CanViewMobilityDispatchData";

    // ─── Logística y optimización ────────────────────────────────────────────

    /// <summary>Dashboard 1 — Panel de Optimización Logística RAEE: SCRAP, COORDINATOR, ADMIN.</summary>
    public const string CanViewLogisticsOptimization = "CanViewLogisticsOptimization";

    /// <summary>Dashboard 2 — Panel de Monitorización para Entidades Públicas: PUBLIC_ENT, ADMIN.</summary>
    public const string CanViewPublicMonitoring = "CanViewPublicMonitoring";

    /// <summary>Dashboard 3 — Panel Operativo Gestores/CACs/Plantas: DISPATCH_OFFICE, CAC_OP, PLANT_OP, ADMIN.</summary>
    public const string CanViewOperationalDashboard = "CanViewOperationalDashboard";

    // ─── Tratamiento y Reciclaje (TR) ────────────────────────────────────────

    /// <summary>Dashboard TR-A — Análisis de Calidad y Revalorización — SCRAP: SCRAP, ADMIN.</summary>
    public const string CanViewTRScrapAnalysis = "CanViewTRScrapAnalysis";

    /// <summary>Dashboard TR-B — Monitorización de Reciclaje — Ayuntamiento: PUBLIC_ENT, ADMIN.</summary>
    public const string CanViewTRMunicipalMonitoring = "CanViewTRMunicipalMonitoring";

    /// <summary>Dashboard TR-C — Validación y Datos Multi-SCRAP — Coordinador: COORDINATOR, ADMIN.</summary>
    public const string CanViewTRCoordinatorValidation = "CanViewTRCoordinatorValidation";

    /// <summary>Vista TR-D — Datos Operativos de Tratamiento — Oficina de Asignación: DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewTRDispatchData = "CanViewTRDispatchData";

    // ─── Ecomodulación (UC5) ─────────────────────────────────────────────────

    /// <summary>Dashboard UC5-A — Panel de Datos de Ecomodulación — SCRAP: SCRAP, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewEcomodulationScrapOverview = "CanViewEcomodulationScrapOverview";

    /// <summary>Dashboard UC5-B — Panel de Monitorización Regulatoria: PUBLIC_ENT, COORDINATOR, ADMIN.</summary>
    public const string CanViewEcomodulationRegulatoryView = "CanViewEcomodulationRegulatoryView";

    /// <summary>Dashboard UC5-C — Preparación DPP: SCRAP, COORDINATOR, PUBLIC_ENT, DISPATCH_OFFICE, ADMIN.</summary>
    public const string CanViewEcomodulationDppReadiness = "CanViewEcomodulationDppReadiness";

    // ─── Administración del sistema ──────────────────────────────────

    /// <summary>Operaciones exclusivas del administrador del sistema (ej. seed de datos).</summary>
    public const string AdminOnly = "AdminOnly";
}
