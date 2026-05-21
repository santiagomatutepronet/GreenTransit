namespace GreenTransit.Web.Components.Pages.EcoDataNet.Dataspace.Shared;

public record DatasetInfo(string Ref, string Desc, string Uc);
public record ConsumeGroup(string FromProfile, string FromProfileLabel, List<DatasetInfo> Items);

public record ProfileDatasets
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public List<DatasetInfo> Publish { get; init; } = new();
    public List<ConsumeGroup> ConsumeGrouped { get; init; } = new();
}

public static class EcoDataNetDatasetStore
{
    public const string BaseDatasetEndpointHint = "/api/usDataSet/{DatasetName}";

    public static readonly IReadOnlyList<ProfileDatasets> Profiles = new List<ProfileDatasets>
    {
        // === OFICINA DE ASIGNACIÓN (DISPATCH_OFFICE) ===
        new ProfileDatasets
        {
            Id    = "dispatch-office",
            Label = "Oficina de Asignación",
            Slug  = "dispatch-office",
            Publish = new()
            {
                new("UC1_OFIRAEE_Publish_Agreements",               "Convenios y acuerdos marco con condiciones, vigencia y obligaciones.", "UC1"),
                new("UC1_OFIRAEE_Publish_ServiceOrders",            "Órdenes de servicio para recogida/entrega con planificación y ubicación.", "UC1"),
                new("UC1_OFIRAEE_Publish_ComplianceKPIs",           "KPIs de cumplimiento por SCRAP/periodo/categoría.", "UC1"),
                new("UC1_OFIRAEE_Publish_Settlements",              "Liquidaciones/compensaciones económicas por convenio/periodo.", "UC1"),
                new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics",  "Órdenes planificadas con detalle logístico (ventanas, destino, etc.).", "UC2"),
                new("UC2_OFIRAEE_Publish_LogisticsKPIs",            "KPIs logísticos (distancia, duración, puntualidad, volumen, etc.).", "UC2"),
                new("UC3_OFIRAEE_Publish_MobilityKPIs_Basic",       "KPIs de movilidad por franjas/uso urbano para análisis DUM.", "UC3"),
                new("UC4_OFIRAEE_Publish_RecyclingKPIs",            "KPIs de reciclaje/valorización agregados por periodo/tipología.", "UC4"),
                new("UC5_OFIRAEE_Publish_TraceabilityKPIs",         "KPIs de trazabilidad agregados (CAC → traslado → tratamiento).", "UC5"),
                new("UC6_Publish_HeatmapEvents_Aggregated",         "Eventos georreferenciados agregados para mapas de calor.", "UC6"),
                new("UC7_Publish_EmissionFactors_Active",           "Factores de emisión activos (fuente/metodología/versionado).", "UC7"),
                new("UC7_Publish_Emissions_Calculated",             "Emisiones calculadas por servicio/traslado en base a distancia y factores.", "UC7"),
                new("UC7_Publish_CarbonFootprintKPIs",              "KPIs consolidados de huella de carbono por zona/periodo/tipología.", "UC7"),
            },
            ConsumeGrouped = new()
            {
                new("scrap", "SCRAP", new()
                {
                    new("UC1_SCRAP_Publish_MarketShares_Objectives", "Objetivos y cuotas SCRAP por periodo/categoría/territorio.", "UC1"),
                }),
                new("carrier", "Transportista", new()
                {
                    new("UC2_Gestor_Publish_WasteMoves_Execution",   "Ejecución real de traslados (fechas, ruta, residuos, vehículo).", "UC1/UC2/UC3/UC7"),
                    new("UC3_Gestor_Publish_Execution_ForAudit",     "Ejecución real orientada a auditoría movilidad (plan vs real).", "UC3"),
                }),
                new("plant", "Planta de tratamiento", new()
                {
                    new("UC2_Planta_Publish_Entries",                "Entradas en planta con pesaje/fecha/referencia.", "UC1/UC2/UC4"),
                    new("UC2_Planta_Publish_Treatments",             "Tratamiento ejecutado con impropios y resultado por fracciones.", "UC1/UC2/UC4"),
                    new("UC4_Planta_Publish_TreatmentQuality",       "Calidad/rendimiento del tratamiento final (ratios, rechazos).", "UC4"),
                }),
                new("cac", "Operador de Centro de Acopio", new()
                {
                    new("UC2_CAC_Publish_AvailableVolumes",          "Volúmenes disponibles por punto/método/tipología.", "UC1/UC2/UC6"),
                }),
                new("public-entity", "Entidad Pública / Ayuntamiento", new()
                {
                    new("UC1_Ayto_Publish_TonnageByMethod_Point_Period", "Toneladas municipales por punto/método/periodo.", "UC1/UC6"),
                    new("UC3_Ayto_Publish_DUMZones",                 "Zonificación DUM/áreas urbanas reguladas.", "UC3"),
                    new("UC3_Ayto_Publish_DUMRestrictionRules",      "Reglas y restricciones DUM (horarios, condiciones, acciones).", "UC3"),
                }),
                new("producer", "Productor", new()
                {
                    new("UC5_Productor_Publish_ProductSpecs",        "Ficha de producto/categoría (composición, reparabilidad, etc.).", "UC5"),
                }),
            }
        },

        // === SCRAP ===
        new ProfileDatasets
        {
            Id    = "scrap",
            Label = "SCRAP",
            Slug  = "scrap",
            Publish = new()
            {
                new("UC1_SCRAP_Publish_MarketShares_Objectives",        "Objetivos/cuotas oficiales por categoría, territorio y periodo.", "UC1"),
                new("UC1_SCRAP_Publish_OperationalEvidence_Aggregated", "Evidencia operativa agregada (movimientos por periodo/tipología).", "UC1"),
                new("UC5_SCRAP_Publish_EcoModulationRules",             "Reglas de ecomodulación (incentivos/penalizaciones por criterios).", "UC5"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC1_OFIRAEE_Publish_Agreements",              "Convenios aplicables al SCRAP.", "UC1"),
                    new("UC1_OFIRAEE_Publish_Settlements",             "Liquidaciones/compensaciones asociadas.", "UC1"),
                    new("UC1_OFIRAEE_Publish_ComplianceKPIs",          "KPIs de cumplimiento (logro objetivo vs evidencia).", "UC1"),
                    new("UC1_OFIRAEE_Publish_ServiceOrders",           "Órdenes/servicios emitidos.", "UC1"),
                    new("UC4_OFIRAEE_Publish_RecyclingKPIs",           "KPIs globales de reciclaje para seguimiento.", "UC4"),
                    new("UC5_OFIRAEE_Publish_TraceabilityKPIs",        "KPIs trazabilidad para rendimiento por tipología.", "UC5"),
                }),
                new("public-entity", "Entidad Pública / Ayuntamiento", new()
                {
                    new("UC1_Ayto_Publish_TonnageByMethod_Point_Period", "Datos municipales de recogida por método/punto/periodo.", "UC1"),
                }),
                new("plant", "Planta de tratamiento", new()
                {
                    new("UC4_Planta_Publish_TreatmentQuality",         "Resultado final/certificado de tratamiento y ratios de recuperación.", "UC4"),
                }),
            }
        },

        // === ENTIDAD PÚBLICA / AYUNTAMIENTO ===
        new ProfileDatasets
        {
            Id    = "public-entity",
            Label = "Entidad Pública / Ayuntamiento",
            Slug  = "public-entity",
            Publish = new()
            {
                new("UC1_Ayto_Publish_TonnageByMethod_Point_Period", "Toneladas recogidas por punto/método/periodo y tipología.", "UC1"),
                new("UC1_Ayto_Publish_PointInventory_Embedded",      "Inventario de puntos de recogida (ubicación/datos básicos).", "UC1"),
                new("UC3_Ayto_Publish_DUMZones",                     "Zonificación DUM (geometría/ámbitos).", "UC3"),
                new("UC3_Ayto_Publish_DUMRestrictionRules",          "Restricciones DUM (condiciones, vigencias, acción).", "UC3"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC1_OFIRAEE_Publish_Settlements",              "Liquidaciones/compensaciones.", "UC1"),
                    new("UC1_OFIRAEE_Publish_ComplianceKPIs",           "KPIs cumplimiento del sistema.", "UC1"),
                    new("UC1_OFIRAEE_Publish_ServiceOrders",            "Planificación/servicios.", "UC1"),
                    new("UC3_OFIRAEE_Publish_MobilityKPIs_Basic",       "KPIs de movilidad urbana asociados a recogidas.", "UC3"),
                }),
                new("carrier", "Transportista", new()
                {
                    new("UC3_Gestor_Publish_Execution_ForAudit",        "Ejecución real para auditoría plan vs real.", "UC3"),
                }),
                new("scrap", "SCRAP", new()
                {
                    new("UC1_SCRAP_Publish_MarketShares_Objectives",    "Objetivos/cuotas para contraste (si aplica).", "UC1"),
                }),
            }
        },

        // === TRANSPORTISTA ===
        new ProfileDatasets
        {
            Id    = "carrier",
            Label = "Transportista",
            Slug  = "carrier",
            Publish = new()
            {
                new("UC2_Gestor_Publish_WasteMoves_Execution",       "Traslados ejecutados (residuos, fechas, distancias, vehículo).", "UC2"),
                new("UC3_Gestor_Publish_Execution_ForAudit",         "Ejecución real orientada a auditoría movilidad (servicio y tiempos).", "UC3"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics", "Órdenes planificadas logística.", "UC2"),
                    new("UC1_OFIRAEE_Publish_ServiceOrders",           "Órdenes (si se usa en UC1).", "UC1"),
                }),
                new("scrap", "SCRAP", new()
                {
                    new("UC7_Publish_EmissionFactors_Active",           "Factores de emisión vigentes (si se requiere).", "UC7"),
                }),
            }
        },

        // === OPERADOR DE CENTRO DE ACOPIO (CAC) ===
        new ProfileDatasets
        {
            Id    = "cac",
            Label = "Operador de Centro de Acopio",
            Slug  = "cac",
            Publish = new()
            {
                new("UC2_CAC_Publish_AvailableVolumes", "Volumen disponible/eventos por punto, método y tipología.", "UC2"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics", "Servicios planificados (para preparación).", "UC2"),
                }),
            }
        },

        // === PLANTA DE TRATAMIENTO ===
        new ProfileDatasets
        {
            Id    = "plant",
            Label = "Planta de tratamiento",
            Slug  = "plant",
            Publish = new()
            {
                new("UC2_Planta_Publish_Entries",            "Entradas y pesajes en planta (ticket, neto/bruto, fecha).", "UC2"),
                new("UC2_Planta_Publish_Treatments",         "Tratamiento ejecutado (operación, impropios, fracciones, incidencias).", "UC2"),
                new("UC4_Planta_Publish_TreatmentQuality",   "Calidad/rendimiento final (ratios recuperación/rechazo por tipología).", "UC4"),
            },
            ConsumeGrouped = new()
            {
                new("carrier", "Transportista", new()
                {
                    new("UC2_Gestor_Publish_WasteMoves_Execution",      "Preaviso/relación de traslados para contrastar entrada y tratamiento.", "UC2"),
                }),
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics",  "Planificación prevista (opcional).", "UC2"),
                }),
            }
        },

        // === PRODUCTOR ===
        new ProfileDatasets
        {
            Id    = "producer",
            Label = "Productor",
            Slug  = "producer",
            Publish = new()
            {
                new("UC5_Productor_Publish_ProductSpecs", "Ficha de producto/categoría (composición, reparabilidad, etc.).", "UC5"),
            },
            ConsumeGrouped = new()
            {
                new("scrap", "SCRAP", new()
                {
                    new("UC5_SCRAP_Publish_EcoModulationRules",         "Reglas de ecomodulación.", "UC5"),
                }),
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC5_OFIRAEE_Publish_TraceabilityKPIs",         "KPIs de rendimiento por tipología.", "UC5"),
                }),
                new("plant", "Planta de tratamiento", new()
                {
                    new("UC4_Planta_Publish_TreatmentQuality",          "Resultados reales de tratamiento vinculables a tipologías.", "UC4/UC5"),
                }),
            }
        },

        // === COORDINADOR ===
        new ProfileDatasets
        {
            Id    = "coordinator",
            Label = "Coordinador del acuerdo",
            Slug  = "coordinator",
            Publish = new(),   // No publica nada
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC3_OFIRAEE_Publish_MobilityKPIs_Basic",       "KPIs movilidad para análisis logístico.", "UC3"),
                }),
            }
        },
    };

    /// <summary>
    /// Obtiene los datasets de un perfil por su slug de ruta.
    /// </summary>
    public static ProfileDatasets? GetBySlug(string slug)
        => Profiles.FirstOrDefault(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}
