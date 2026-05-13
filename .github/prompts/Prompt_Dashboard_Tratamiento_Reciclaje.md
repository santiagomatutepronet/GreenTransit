# Prompt para GitHub Copilot — Módulo de Dashboards "Tratamiento y Reciclaje"

> **Instrucción**: Adjunta este prompt junto con `README.md`, `Crear_BD_v4_1.sql`, `COPILOT_CONTEXT.md` y `Mapa_Funcionalidades.md` en cada sesión de Copilot Chat.

---

## 🎯 Objetivo — Caso de Uso: Tratamiento y Reciclaje

Crear un **nuevo módulo de dashboards "Tratamiento y Reciclaje"** dentro de la carpeta `Reporting` de la aplicación GreenTransit. El módulo se centra en el **ámbito de embalajes industriales y comerciales**, con potencial extensión a RAEE y Residuos de Envases Domésticos. El objetivo es evaluar la capacidad de integración Multi-SCRAP en los procesos de tratamiento y reciclaje, alineado con los tratados europeos de economía circular y la reincorporación de residuos en la cadena de valor.

Los dashboards deben:

1. **Los SCRAPs** analicen la calidad de los materiales recuperados tras el tratamiento en planta, determinen la aptitud para revalorización y obtengan insights para optimizar cada etapa del reciclaje.
2. **Las Entidades Públicas (Ayuntamientos)** monitoricen los resultados de tratamiento y las tasas de reciclaje de los residuos de embalajes industriales y comerciales generados en su municipio.
3. **Los Coordinadores** validen los datos compartidos entre actores y supervisen transversalmente el modelo de recogidas Multi-SCRAP.
4. **La Oficina de Asignación** provea los datos operativos necesarios para evaluar los procesos de tratamiento y reciclaje, y alimente un prototipo de modelo de recogidas Multi-SCRAP validable.

Este caso de uso converge con los objetivos de economía circular europeos: incorporar residuos en modelos productivos, optimizar la revalorización y mejorar la trazabilidad del ciclo completo del residuo.

---

## 📊 Dashboards a crear (son TRES vistas diferenciadas + una vista compartida)

> **IMPORTANTE**: Todos los archivos de este módulo deben estar dentro de la carpeta `Reporting/TratamientoReciclaje/` tanto en la capa Web como en la capa Application.

### Dashboard TR-A — **Panel de Análisis de Calidad y Revalorización — SCRAP** (`/reporting/tratamiento-reciclaje/scrap-analysis`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Policy de autorización**: `CanViewTRScrapAnalysis` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: vista estratégica para que los SCRAPs analicen la calidad de los materiales recuperados, evalúen la aptitud para revalorización y optimicen las etapas del reciclaje dentro del ámbito de embalajes industriales y comerciales.

#### Widgets / KPIs requeridos:

1. **Balance de tratamiento por planta** (bar chart apilado + tabla)
   - Fuente: `TreatmentPlants` JOIN `TreatmentPlantResidues` JOIN `Entities` (planta) JOIN `WasteMoves` (para vincular con el SCRAP).
   - Métricas por planta:
     - **Kg reutilizados**: `SUM(TreatmentPlantResidues.WeightReused)`.
     - **Kg valorizados**: `SUM(TreatmentPlantResidues.WeightValued)`.
     - **Kg rechazados/eliminados**: `SUM(TreatmentPlantResidues.WeightRemove)`.
     - **Tasa de reciclaje**: `(WeightReused + WeightValued) / WeightTotal * 100`.
   - Bar chart apilado mostrando la proporción reutilizado/valorizado/rechazado por planta.
   - Drill-down por planta al detalle de residuos tratados.

2. **Tasa de revalorización por tipo de residuo** (donut chart + tabla ranking)
   - Fuente: `TreatmentPlantResidues` JOIN `Residues` JOIN `LERCodes`.
   - Agrupado por `LERCodes.Code` (código LER) y `Residues.Name`.
   - Métricas: tasa de revalorización = `(WeightReused + WeightValued) / WeightTotal * 100`.
   - Semáforo: verde > 70%, naranja 40–70%, rojo < 40%.
   - Objetivo: identificar qué tipos de residuo de embalaje tienen mayor/menor aptitud para reincorporarse en la cadena de valor.

3. **Evolución mensual de calidad de materiales recuperados** (line chart multi-serie)
   - Fuente: `TreatmentPlantResidues` agrupado por mes (vía `TreatmentPlants.CreatedAt` o `EntryPlants.PlantEntryDate`).
   - Series: tasa de reciclaje, % impropios (`TreatmentPlants.ImproperWeight / SUM(WeightTotal) * 100`), kg valorizados.
   - Tendencia de los últimos 12 meses.
   - Objetivo: medir mejora/deterioro en la calidad del material recuperado a lo largo del tiempo.

4. **Comparativa Multi-SCRAP** (tabla ranking + sparklines)
   - Fuente: `TreatmentPlants` JOIN `WasteMoves` JOIN `WasteMoveResidues`, agrupado por `WasteMoves.IdScrap` / `IdScrap2`.
   - Métricas por SCRAP:
     - Nº total de traslados tratados en el periodo.
     - Kg totales tratados.
     - Tasa de reciclaje.
     - % de impropios.
     - Nº de incidencias de tratamiento (`Incidents` WHERE `Type` relacionado con tratamiento).
   - Ordenado por tasa de reciclaje descendente.
   - Objetivo: alimentar el prototipo de modelo de recogidas Multi-SCRAP con datos comparativos.

5. **Mapa de destino de materiales recuperados** (diagrama Sankey o treemap)
   - Fuente: `TreatmentPlantResidues` JOIN `TreatmentOperations` (operación R/D aplicada).
   - Visualización del flujo: material de entrada → operación de tratamiento (R1–R13 / D1–D15) → destino (reutilización, valorización energética, eliminación).
   - Agrupado por `TreatmentOperations.Code` y `TreatmentOperations.Description`.
   - Objetivo: trazar el destino final de los materiales y verificar alineación con objetivos de economía circular.

6. **Incidencias de tratamiento** (tabla con semáforo)
   - Fuente: `Incidents` WHERE `ClosedAt IS NULL` AND vinculadas a `TreatmentPlants` o `WasteMoves` del SCRAP.
   - Columnas: referencia del traslado, tipo de incidencia, severidad, fecha apertura, días abierta, planta afectada.

#### Filtros globales:
- `Year`, `Month`/`Quarter`.
- `IdScrap` (solo ADMIN puede ver todos; SCRAP ve solo los suyos, filtrado por `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`).
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `IdLERCode` (para aislar tipos de embalaje específicos).
- `TreatmentOperationCode` (filtro por operación R/D aplicada).

---

### Dashboard TR-B — **Panel de Monitorización de Reciclaje — Ayuntamiento** (`/reporting/tratamiento-reciclaje/municipal-monitoring`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Policy de autorización**: `CanViewTRMunicipalMonitoring` (nueva).

**Propósito**: los ayuntamientos monitorizan las tasas de reciclaje y tratamiento de los residuos de embalajes industriales y comerciales generados en su municipio, verificando el cumplimiento de objetivos de economía circular.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo de tratamiento** (cards de KPI)
   - **Kg totales tratados del periodo** en el municipio: `SUM(TreatmentPlantResidues.WeightTotal)` filtrado por `ServiceOrders.IdPickupPoint` → `Entities.MunicipalityCode` = municipio del ayuntamiento, o `ServiceOrders.IdIssuedBy = LinkedEntityId`.
   - **Tasa de reciclaje municipal**: `(SUM(WeightReused) + SUM(WeightValued)) / SUM(WeightTotal) * 100`.
   - **% de impropios**: `SUM(ImproperWeight) / SUM(WeightTotal) * 100`.
   - **Kg revalorizados** (aptos para reincorporación en cadena de valor): `SUM(WeightReused) + SUM(WeightValued)`.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Histórico de tasas de reciclaje** (line chart dual axis)
   - Eje izquierdo: kg tratados por mes.
   - Eje derecho: tasa de reciclaje (%).
   - Líneas separadas por SCRAP (si hay más de uno operando en el municipio).
   - Tendencia de los últimos 12 meses.

3. **Detalle de cumplimiento por SCRAP** (tabla)
   - Fuente: `TreatmentPlants` JOIN `WasteMoves` JOIN `ServiceOrders` WHERE `ServiceOrders.IdIssuedBy = LinkedEntityId` o `IdPickupPoint` en su municipio.
   - Columnas por SCRAP: nº traslados tratados, kg totales, tasa de reciclaje, % impropios, nº incidencias, operaciones R/D predominantes.
   - Semáforo por fila según tasa de reciclaje.

4. **Distribución por operación de tratamiento** (donut chart)
   - Fuente: `TreatmentPlants` JOIN `TreatmentOperations`.
   - Categorías: operaciones R (valorización) vs operaciones D (eliminación).
   - Subcategorías: desglose por código R/D específico (`R1`, `R2`, ..., `D1`, `D2`, ...).
   - Objetivo: verificar que las operaciones de tratamiento se alinean con objetivos de economía circular (predominancia de operaciones R sobre D).

5. **Alertas de calidad** (lista tipo inbox)
   - Alertas generadas cuando:
     - La tasa de reciclaje del municipio cae por debajo de un umbral configurable (por defecto 50%).
     - El % de impropios supera un umbral configurable (por defecto 15%).
     - Hay incidencias de tratamiento abiertas en plantas que procesan residuos del municipio.
   - Fuente: generadas por el backend al calcular KPIs periódicos.

#### Filtros:
- `Year`, `Month`.
- `IdScrap` (los SCRAPs que operan en su municipio — derivado de `WasteMoves` históricas o `Agreements`).
- `IdLERCode` (para aislar tipos de embalaje).

---

### Dashboard TR-C — **Panel de Validación y Datos Multi-SCRAP — Coordinador** (`/reporting/tratamiento-reciclaje/coordinator-validation`)

**Destinado a**: perfiles `COORDINATOR` y `ADMIN`.

**Policy de autorización**: `CanViewTRCoordinatorValidation` (nueva).

**Propósito**: los coordinadores (entidades certificadoras) validan los datos compartidos entre SCRAPs, entidades públicas y plantas de tratamiento, supervisando transversalmente el modelo de recogidas Multi-SCRAP para embalajes industriales y comerciales.

#### Widgets / KPIs requeridos:

1. **Resumen transversal Multi-SCRAP** (cards agrupadas por SCRAP)
   - Fuente: `WasteMoves` JOIN `TreatmentPlants` JOIN `TreatmentPlantResidues`, filtrado por `Agreements.IdCoordinator = LinkedEntityId`.
   - Para cada SCRAP vinculado a los acuerdos del coordinador:
     - Nº de recogidas del periodo.
     - Kg totales tratados.
     - Tasa de reciclaje.
     - % impropios.
     - Nº incidencias abiertas.
   - Con drill-down al detalle por SCRAP.

2. **Comparativa de rendimiento entre SCRAPs** (radar chart / bar chart comparativo)
   - Fuente: misma que widget 1, agregada por SCRAP.
   - Ejes del radar: tasa de reciclaje, % impropios (invertido), volumen tratado, nº incidencias (invertido), % operaciones R vs D.
   - Objetivo: visualizar fortalezas/debilidades relativas de cada SCRAP participante.

3. **Mapa de flujos de residuos origen → planta → destino** (diagrama Sankey)
   - Fuente: `ServiceOrders` (origen/municipio) → `WasteMoves` → `EntryPlants` → `TreatmentPlants` → `TreatmentPlantResidues` (destino por operación R/D).
   - Visualización del ciclo completo: municipio de origen → planta de tratamiento → destino del material (reutilización / valorización / eliminación).
   - Filtrable por SCRAP y por código LER.

4. **Panel de datos exportables para validación externa** (tabla con exportación XLSX)
   - Fuente: `TreatmentPlants` + `TreatmentPlantResidues` + `WasteMoves` + `ServiceOrders` + `Entities` + `TreatmentOperations`.
   - Dataset plano con campos: fecha tratamiento, municipio origen, provincia, SCRAP, planta, código LER, descripción residuo, operación R/D aplicada, peso total, peso reutilizado, peso valorizado, peso rechazado, tasa reciclaje, impropios.
   - Exportable a XLSX (patrón ClosedXML existente).
   - Objetivo: proveer datos limpios para validación por parte de coordinadores y entidades certificadoras.

5. **Evolución mensual por SCRAP** (line chart multi-serie)
   - Series: tasa de reciclaje por SCRAP (una línea por cada SCRAP vinculado al coordinador).
   - Últimos 12 meses.
   - Incluir línea de referencia con el objetivo de reciclaje normativo (configurable en `appsettings.json`).

#### Filtros globales:
- `Year`, `Month`/`Quarter`.
- `IdScrap` (el coordinador ve transversalmente los SCRAPs vinculados a sus acuerdos — filtrar por `Agreements` donde participa como `IdCoordinator`).
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `IdLERCode`.
- `TreatmentOperationCode`.

---

### Vista compartida TR-D — **Datos Operativos de Tratamiento y Reciclaje — Oficina de Asignación** (`/reporting/tratamiento-reciclaje/dispatch-data`)

**Destinado a**: perfiles `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewTRDispatchData` (nueva).

**Propósito**: la Oficina de Asignación provee los datos operativos necesarios para evaluar los procesos de tratamiento y reciclaje Multi-SCRAP y alimentar el prototipo de modelo de recogidas validable.

#### Widgets / KPIs requeridos:

1. **Panel de datos exportables para análisis externo** (tabla con exportación XLSX)
   - Fuente: `TreatmentPlants` + `TreatmentPlantResidues` + `WasteMoves` + `WasteMoveResidues` + `ServiceOrders` + `Entities` + `TreatmentOperations` + `LERCodes`.
   - Dataset plano con campos: fecha tratamiento, fecha recogida, municipio origen, provincia, SCRAP, planta, transportista, código LER, descripción residuo, tipo vehículo, distancia, duración, emisiones CO₂, operación R/D, peso total, peso reutilizado, peso valorizado, peso rechazado, impropios, tasa reciclaje.
   - Exportable a XLSX (patrón ClosedXML existente).
   - Objetivo: proveer datos limpios para el prototipo de modelo de recogidas Multi-SCRAP.

2. **Resumen operativo por SCRAP** (cards agrupadas)
   - Para cada SCRAP bajo la coordinación de la oficina:
     - Nº de recogidas del periodo.
     - Kg totales tratados.
     - Tasa de reciclaje.
     - % impropios.
     - Nº incidencias abiertas.
   - Con drill-down al detalle.

3. **Balance de tratamiento agregado** (bar chart + tabla)
   - Fuente: `TreatmentPlantResidues` del periodo, todos los SCRAPs del tenant.
   - Métricas: kg reutilizados, kg valorizados, kg rechazados, tasa global de reciclaje.
   - Comparativa con periodo anterior.

4. **Evolución mensual de métricas de reciclaje** (line chart multi-serie)
   - Series: tasa de reciclaje global, % impropios, kg valorizados por periodo `YYYY-MM`.
   - Fuente: agregación mensual de las métricas ya calculadas.
   - Últimos 12 meses.
   - Objetivo: ver tendencia para informar el modelo de recogidas Multi-SCRAP.

5. **Distribución de operaciones de tratamiento** (treemap)
   - Fuente: `TreatmentPlants` JOIN `TreatmentOperations`.
   - Treemap por operación R/D: tamaño = kg tratados, color = tipo (R = verde, D = rojo).
   - Objetivo: visión rápida de qué operaciones de tratamiento predominan.

#### Filtros:
- `Year`, `Month`.
- `IdScrap`.
- `ProvinceCode` / `MunicipalityCode`.
- `IdLERCode`.

---

## 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el módulo de Tratamiento y Reciclaje se alimenta de las tablas existentes del modelo v4.1. Las nuevas métricas (tasa de reciclaje, % impropios, aptitud para revalorización) se calculan en las Queries CQRS.

| Tabla | Campos principales para este módulo |
|-------|-------------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `ProvinceCode`, `MunicipalityCode`, `IsActive` |
| `ServiceOrders` | `Id`, `Status`, `IdPickupPoint`, `IdIssuedBy`, `WasteStream`, `IdLERCode`, `OwnerId` |
| `WasteMoves` | `Id`, `IdSource`, `IdDestination`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `ServiceStatus`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `IdResidue`, `Weight`, `VehicleType`, `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions` |
| `EntryPlants` | `Id`, `IdWasteMove`, `PlantEntryDate`, `GrossWeight`, `TareWeight`, `NetWeight`, `ServiceOrderId`, `OwnerId` |
| `EntryPlantResidues` | `IdEntryPlant`, `IdResidue`, `Weight`, `MeasureUnit` |
| `TreatmentPlants` | `Id`, `IdTreatmentOperation`, `ImproperWeight`, `ServiceOrderId`, `IncidentId`, `OwnerId` |
| `TreatmentPlantResidues` | `IdTreatmentPlant`, `IdResidue`, `WeightTotal`, `WeightReused`, `WeightValued`, `WeightRemove` |
| `TreatmentOperations` | `Id`, `Code`, `Description`, `IsActive` (catálogo R1–R13 / D1–D15) |
| `Residues` | `Id`, `Name`, `Reference`, `ResidueType`, `IdLERCode`, `IdProducer` |
| `LERCodes` | `Id`, `Code`, `Description`, `IsDangerous` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `WasteMoveReference`, `ServiceOrderId` |
| `Agreements` | `Id`, `IdScrap`, `IdCoordinator`, `IdPublicEntity` |

---

## 🔒 Reglas de autorización y filtrado de datos

### Control de acceso a pantallas (NO hardcodeado)

> **CRÍTICO**: El acceso a cada dashboard se gestiona **dinámicamente desde la interfaz de administración** (`/security/page-permissions`) mediante las tablas `PageDefinitions` y `PagePermissions`. El administrador configura qué perfiles pueden acceder a cada pantalla (Lectura / Escritura / Ambos / Sin acceso) sin modificar código.

Las policies en código (`CanViewTRScrapAnalysis`, `CanViewTRMunicipalMonitoring`, `CanViewTRCoordinatorValidation`, `CanViewTRDispatchData`) actúan como **mínimo de seguridad estático** (suelo). Los permisos dinámicos de BD pueden restringir más, pero nunca ampliar el acceso más allá de lo que permite la policy de código.

El servicio `IPageDiscoveryService` detectará automáticamente las nuevas páginas `.razor` al arrancar la aplicación y las registrará en `PageDefinitions`. El administrador las verá destacadas en amarillo en `/security/page-permissions` y deberá asignar permisos.

### Filtrado de datos por perfil

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | TR-A | Solo ve datos de tratamiento donde `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. |
| `PUBLIC_ENT` | TR-B | Solo ve datos de tratamiento cuyo punto de recogida (`IdPickupPoint` → `Entities.MunicipalityCode`) pertenece a su municipio, o cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`). |
| `COORDINATOR` | TR-C | Ve transversalmente los SCRAPs vinculados a sus acuerdos (`Agreements.IdCoordinator = LinkedEntityId`). |
| `DISPATCH_OFFICE` | TR-D | Ve **todos** los datos de tratamiento del tenant (`OwnerId`). Sin restricción por entidad vinculada. |
| `ADMIN` | TR-A, TR-B, TR-C, TR-D | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, coordinador, ayuntamiento o planta. |

**Patrón de filtrado de datos**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` + `IDataScopeService.ApplyScope()` (ya implementados en la capa Application).

**Regla especial para DISPATCH_OFFICE y ADMIN**: estos perfiles ven **todos** los registros del tenant sin filtro por entidad vinculada. La lógica en los Query handlers debe comprobar:
```csharp
if (_currentUser.IsInAnyProfile("ADMIN", "DISPATCH_OFFICE"))
{
    // Sin filtro por LinkedEntityId — ve todo el tenant (ya filtrado por OwnerId)
}
```

---

## 🏗️ Arquitectura de implementación

### Capa Application (CQRS)

```
Application/Features/Reporting/TratamientoReciclaje/
├── Queries/
│   ├── GetTRScrapAnalysisQuery.cs              → TR-A (SCRAP)
│   ├── GetTRMunicipalMonitoringQuery.cs         → TR-B (PUBLIC_ENT)
│   ├── GetTRCoordinatorValidationQuery.cs       → TR-C (COORDINATOR)
│   ├── GetTRDispatchDataQuery.cs                → TR-D (DISPATCH_OFFICE)
│   ├── GetTreatmentBalanceQuery.cs              → Widget compartido: balance tratamiento
│   ├── GetRecyclingRateQuery.cs                 → Widget compartido: tasa de reciclaje
│   ├── GetTreatmentOperationsDistributionQuery.cs → Widget compartido: distribución R/D
│   └── ExportTRDataToExcelQuery.cs              → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── TRScrapAnalysisDto.cs
│   ├── TRMunicipalMonitoringDto.cs
│   ├── TRCoordinatorValidationDto.cs
│   ├── TRDispatchDataDto.cs
│   ├── TreatmentBalanceDto.cs
│   ├── RecyclingRateDto.cs
│   ├── TreatmentOperationsDistributionDto.cs
│   ├── TRExportRowDto.cs
│   └── TRScrapComparisonDto.cs
└── Services/
    └── RecyclingQualityEngine.cs                → Motor de alertas de calidad de reciclaje
```

### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/TratamientoReciclaje/
├── ScrapAnalysis.razor                → /reporting/tratamiento-reciclaje/scrap-analysis
├── MunicipalMonitoring.razor          → /reporting/tratamiento-reciclaje/municipal-monitoring
├── CoordinatorValidation.razor        → /reporting/tratamiento-reciclaje/coordinator-validation
└── DispatchData.razor                 → /reporting/tratamiento-reciclaje/dispatch-data
```

### Componentes reutilizables (complementan los ya existentes)

- `TreatmentBalanceChart.razor` — bar chart apilado (reutilizado/valorizado/rechazado).
- `RecyclingRateCard.razor` — card de tasa de reciclaje con comparativa temporal.
- `TreatmentSankeyDiagram.razor` — diagrama Sankey de flujos entrada → operación R/D → destino.
- `RecyclingQualityAlerts.razor` — panel de alertas de calidad.
- `ScrapComparisonTable.razor` — tabla ranking comparativa Multi-SCRAP con sparklines.

> **Reutilizar** de los dashboards existentes: `EmissionsCard.razor`, `IncidentsBadge.razor`, `DUMComplianceDonut.razor` (si aplica).

---

### Actualización de `PageDiscoveryService.InferModuleName()`

Las nuevas rutas `/reporting/tratamiento-reciclaje/` ya mapean al módulo **Reporting** gracias a la regla existente `Reporting` · `/traceability` · `/kpis` · `/documents`. Sin embargo, si se desea un submódulo diferenciado (ej: "Tratamiento y Reciclaje"), añadir en `InferModuleName()`:

```csharp
if (route.StartsWith("/reporting/tratamiento-reciclaje"))
    return "Tratamiento y Reciclaje";
```

Si no se añade, las pantallas aparecerán bajo el módulo genérico "Reporting" en `/security/page-permissions`, lo cual también es aceptable.

### Actualización de `PageDiscoveryService.HumanizeName()`

Añadir las traducciones al español para los nombres de componentes:

```csharp
{ "ScrapAnalysis", "Análisis de Calidad y Revalorización — SCRAP" },
{ "MunicipalMonitoring", "Monitorización de Reciclaje — Ayuntamiento" },  // Ya existe en Mobility, renombrar si conflicto
{ "CoordinatorValidation", "Validación y Datos Multi-SCRAP — Coordinador" },
{ "DispatchData", "Datos Operativos de Tratamiento — Oficina de Asignación" },  // Ya existe en Mobility, renombrar si conflicto
```

> **Nota sobre conflictos de nombre**: si ya existen componentes `MunicipalMonitoring.razor` y `DispatchData.razor` en el módulo Mobility, los nuevos componentes en la carpeta `Reporting/TratamientoReciclaje/` tendrán un namespace diferente pero el mismo nombre de clase. Para evitar conflictos en `HumanizeName()`, usar el **nombre completo cualificado** (ej: `Reporting.TratamientoReciclaje.MunicipalMonitoring`) o renombrar los componentes nuevos (ej: `TRMunicipalMonitoring.razor`, `TRDispatchData.razor`).

### Policies nuevas a registrar

En `Domain/Authorization/PolicyConstants.cs`:

```csharp
public const string CanViewTRScrapAnalysis = nameof(CanViewTRScrapAnalysis);
public const string CanViewTRMunicipalMonitoring = nameof(CanViewTRMunicipalMonitoring);
public const string CanViewTRCoordinatorValidation = nameof(CanViewTRCoordinatorValidation);
public const string CanViewTRDispatchData = nameof(CanViewTRDispatchData);
```

En `Program.cs`:

```csharp
options.AddPolicy(PolicyConstants.CanViewTRScrapAnalysis, p =>
    p.RequireRole("SCRAP", "ADMIN"));

options.AddPolicy(PolicyConstants.CanViewTRMunicipalMonitoring, p =>
    p.RequireRole("PUBLIC_ENT", "ADMIN"));

options.AddPolicy(PolicyConstants.CanViewTRCoordinatorValidation, p =>
    p.RequireRole("COORDINATOR", "ADMIN"));

options.AddPolicy(PolicyConstants.CanViewTRDispatchData, p =>
    p.RequireRole("DISPATCH_OFFICE", "ADMIN"));
```

### Entrada en `NavMenu.razor`

Añadir una sección **Tratamiento y Reciclaje** bajo **Reporting** en el menú de navegación, con consulta a `IPagePermissionService.CanAccessRouteAsync()` para cada enlace:

```razor
@* — Tratamiento y Reciclaje — *@
<AuthorizeView Policy="@PolicyConstants.CanViewTRScrapAnalysis">
    @if (await PagePermissionService.CanAccessRouteAsync("/reporting/tratamiento-reciclaje/scrap-analysis", currentProfile))
    {
        <NavLink href="/reporting/tratamiento-reciclaje/scrap-analysis">
            <span class="nav-icon">♻️</span> Análisis Revalorización SCRAP
        </NavLink>
    }
</AuthorizeView>
@* ... repetir para TR-B, TR-C, TR-D ... *@
```

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. **ADMIN** y **DISPATCH_OFFICE** ven todos los datos del tenant sin filtro por entidad vinculada.
3. El acceso a pantallas se gestiona **dinámicamente** desde `/security/page-permissions`, NO hardcodeado.
4. Las pantallas nuevas se auto-descubren al arrancar (`IPageDiscoveryService`) y aparecen en `/security/page-permissions` destacadas en amarillo hasta que el admin las configure.
5. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
6. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
7. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
8. Exportación a XLSX disponible en TR-C y TR-D (patrón ya implementado con ClosedXML).
9. Responsive mobile-first.
10. Modo oscuro/claro (consistente con `MainLayout.razor`).
11. La **tasa de reciclaje** se calcula como: `(SUM(WeightReused) + SUM(WeightValued)) / SUM(WeightTotal) * 100`.
12. Los umbrales de alertas de calidad son configurables (no hardcodeados): se almacenan en `appsettings.json` → sección `RecyclingSettings`:
    - `MinRecyclingRateThreshold`: 50 (%)
    - `MaxImproperRateThreshold`: 15 (%)
13. Las alertas de calidad se generan en el backend (`RecyclingQualityEngine.cs`), no en el cliente.
14. Los archivos Blazor, Queries y DTOs están dentro de la carpeta `Reporting/TratamientoReciclaje/`.

---

## 🔗 Integración con módulos existentes

- **Dashboard UC2 (Optimización Logística RAEE)**: los datos de transporte (`WasteMoveResidues.TransportInfo_*`) se comparten. Enlace cruzado desde TR-A al Dashboard 1 para ver eficiencia de rutas.
- **Dashboard UC3 (Movilidad Urbana)**: los indicadores de conflicto de movilidad pueden complementar el análisis de tratamiento. Enlace cruzado desde TR-D al UC3-C para datos de impacto en movilidad.
- **Trazabilidad (§5.1)**: desde cualquier traslado en los dashboards TR, enlace directo a `/traceability?term={WasteMoveReference}`.
- **KPIs regulatorios (§5.2)**: los KPIs de tasa de reciclaje ya existen parcialmente en `/kpis`; aquí se desglosan por planta/SCRAP/municipio.
- **Incidencias (§4.3)**: los widgets de incidencias enlazan a `/incidents/{id}`.
- **Operaciones de tratamiento (§2.4)**: reutilización del catálogo `TreatmentOperations` como referencia normativa.

---

## 📋 Matriz de permisos recomendada (para configuración inicial en `/security/page-permissions`)

| Pantalla | Entidad BD principal | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **TR-A Análisis Revalorización** | `TreatmentPlants/Residues` | — | — | **R-P** | — | — | — | — | — | **R** |
| **TR-B Monit. Reciclaje Municipal** | `TreatmentPlants/Residues` | — | — | — | **R-P** | — | — | — | — | **R** |
| **TR-C Validación Multi-SCRAP** | `TreatmentPlants/Residues` | — | — | — | — | — | — | **R-P** | — | **R** |
| **TR-D Datos Operativos** | `TreatmentPlants/Residues` | — | — | — | — | — | — | — | **R** | **R** |

> Esta matriz es la **configuración recomendada por defecto**. El administrador la aplica desde `/security/page-permissions` tras el despliegue. No está hardcodeada en código.

---

## ✅ Checklist al crear las páginas

- [ ] `@page "/reporting/tratamiento-reciclaje/..."` definida en cada `.razor`
- [ ] `@attribute [Authorize(Policy = PolicyConstants.CanViewTR...)]` con policy adecuada
- [ ] Policies nuevas añadidas en `PolicyConstants.cs` + `Program.cs`
- [ ] Namespace: `Pages/Reporting/TratamientoReciclaje/`
- [ ] Si `InferModuleName()` no mapea correctamente → actualizar con caso `"/reporting/tratamiento-reciclaje"`
- [ ] `HumanizeName()` actualizado con nombres en español (evitando conflictos con Mobility)
- [ ] Entradas añadidas en `NavMenu.razor` en la sección Reporting con consulta a `IPagePermissionService`
- [ ] Configuración `RecyclingSettings` añadida en `appsettings.json`
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions`
