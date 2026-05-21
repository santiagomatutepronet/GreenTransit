# Autoridad de Publicación por Entidad — BD Intermedia EcoDataNet

> Cada tabla de la BD intermedia de EcoDataNet tiene **un único publicador autoritativo**: el participante que es dueño natural del dato y lo publica como copia directa desde GreenTransit. Este principio garantiza cero duplicación y un punto de verdad único por entidad.
>
> Los demás participantes que necesiten estos datos los **consumen** a través de los endpoints GET del conector EDC, aplicando los filtros de scoping correspondientes a su perfil.

---

## Mapa de autoridad

| Entidad (tabla GreenTransit) | Publicador autoritativo | Justificación |
|---|---|---|
| `Agreements` | **DISPATCH_OFFICE** | La Oficina de Asignación es la gestora central de los convenios marco entre SCRAPs, entidades públicas y coordinadores. Tiene visibilidad completa del tenant. |
| `ServiceOrders` | **DISPATCH_OFFICE** | Las órdenes de servicio se planifican y gestionan centralmente. Aunque las emiten distintos perfiles (PRODUCER, PUBLIC_ENT), la Oficina consolida la vista completa. |
| `WasteMoves` | **DISPATCH_OFFICE** | El traslado es la entidad núcleo del sistema. La Oficina tiene la visión integral de todos los traslados del tenant, incluyendo los actores implicados (origen, destino, SCRAP, transportista). |
| `WasteMoveResidues` | **DISPATCH_OFFICE** | Detalle por residuo de cada traslado: pesos, documentación normativa (NT/DI), datos de transporte y emisiones. Inseparable de `WasteMoves`. |
| `Settlements` | **DISPATCH_OFFICE** | Las liquidaciones económicas por periodo se gestionan centralmente. Vinculadas a convenios y validadas por la Oficina. |
| `SettlementLines` | **DISPATCH_OFFICE** | Desglose por línea de cada liquidación (peso, LER, precio). Inseparable de `Settlements`. |
| `MarketShares` | **SCRAP** | Las cuotas de mercado son objetivos propios de cada SCRAP por categoría, territorio y periodo. Cada SCRAP es el dueño de sus cuotas. |
| `EntryPlants` | **PLANT_OP** | El registro de entrada y pesaje en planta lo realiza el operador de la planta. Es el dato oficial de recepción. |
| `EntryPlantResidues` | **PLANT_OP** | Detalle por residuo de cada entrada en planta. Inseparable de `EntryPlants`. |
| `TreatmentPlants` | **PLANT_OP** | El tratamiento ejecutado (operación, impropios, incidencias) es responsabilidad del operador de la planta. |
| `TreatmentPlantResidues` | **PLANT_OP** | Fracciones resultantes del tratamiento (reutilización, reciclaje, valorización, rechazo). Inseparable de `TreatmentPlants`. |
| `EntryCACs` | **CAC_OP** | Las entradas en centro de acopio las registra el operador del CAC. |
| `EntryCACResidues` | **CAC_OP** | Detalle por residuo de cada entrada en CAC. Inseparable de `EntryCACs`. |
| `DUMZones` | **PUBLIC_ENT** | Las zonas de Distribución Urbana de Mercancías las define y gestiona el ayuntamiento en su ámbito municipal. |
| `DUMRestrictionRules` | **PUBLIC_ENT** | Las reglas de restricción de cada zona DUM son competencia municipal. Inseparables de `DUMZones`. |
| `EmissionFactorSets` | **DISPATCH_OFFICE** | Los conjuntos de factores de emisión (metodología, versionado, vigencia) se gestionan centralmente como catálogo normativo. |
| `EmissionFactors` | **DISPATCH_OFFICE** | Factores individuales por tipo de vehículo, combustible y clase Euro. Inseparables de `EmissionFactorSets`. |
| `PlantEnergies` | **PLANT_OP** | El consumo energético declarado por planta y periodo es responsabilidad del operador de la planta (Scope 2). |
| `EcoModulationRuleSets` | **SCRAP** | Los conjuntos de reglas de ecomodulación (incentivos/penalizaciones) los define cada SCRAP según sus criterios de ecodiseño. |
| `EcoModulationRules` | **SCRAP** | Reglas individuales de cada conjunto. Inseparables de `EcoModulationRuleSets`. |
| `ProductSpecs` | **PRODUCER** | La ficha técnica de producto (composición, reparabilidad, contenido reciclado) la declara el productor como responsable RAP. |

---

## Resumen por publicador

| Publicador | Entidades que publica | Total |
|---|---|---|
| **DISPATCH_OFFICE** | `Agreements`, `ServiceOrders`, `WasteMoves`, `WasteMoveResidues`, `Settlements`, `SettlementLines`, `EmissionFactorSets`, `EmissionFactors` | 8 |
| **PLANT_OP** | `EntryPlants`, `EntryPlantResidues`, `TreatmentPlants`, `TreatmentPlantResidues`, `PlantEnergies` | 5 |
| **SCRAP** | `MarketShares`, `EcoModulationRuleSets`, `EcoModulationRules` | 3 |
| **PUBLIC_ENT** | `DUMZones`, `DUMRestrictionRules` | 2 |
| **CAC_OP** | `EntryCACs`, `EntryCACResidues` | 2 |
| **PRODUCER** | `ProductSpecs` | 1 |
| **CARRIER** | — | 0 |
| **COORDINATOR** | — | 0 |

> **CARRIER** y **COORDINATOR** no publican entidades base. CARRIER aporta datos de ejecución que ya quedan registrados dentro de `WasteMoves` / `WasteMoveResidues` (publicados por DISPATCH_OFFICE). COORDINATOR es consumidor puro.

---

## Principio de diseño

```
Regla:  1 entidad = 1 publicador autoritativo = 0 duplicación
```

Los KPIs, agregaciones y vistas cruzadas se construyen como **vistas SQL** sobre las tablas base de la BD intermedia, nunca como datasets publicados independientes. De esta forma, cualquier consumidor que acceda a un endpoint GET obtiene datos calculados en tiempo real a partir de la fuente de verdad única.
