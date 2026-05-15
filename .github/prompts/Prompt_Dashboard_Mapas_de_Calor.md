# 🗺️ Módulo de Mapas de Calor — Densidad y Patrones de Residuos (UC Mapas de Calor)

## 🎯 Objetivo

Crear un **nuevo módulo de dashboards "Mapas de Calor"** dentro de la carpeta `Reporting` que permita visualizar **mapas de calor a partir de los datos operativos de reciclado** existentes en GreenTransit. El objetivo es identificar zonas de elevada densidad de residuos, patrones de generación, estacionalidad y tipología, facilitando la planificación urbana y la optimización de recursos para un entorno más sostenible.

Los datos visualizados incluyen:
- **Clasificación detallada del residuo** según composición (código LER, flujo de residuo), con cantidad exacta.
- **Datos georreferenciados** de los puntos de recogida en territorio español (latitud/longitud de las entidades).
- **Frecuencia de generación y recogida** basada en el volumen de solicitudes (órdenes de servicio) en cada punto de recogida.

**Participantes del ecosistema**:
- **Proveedores de datos**: los SCRAPs, que generan la operativa de recogida y tratamiento.
- **Consumidores de datos**: entes públicos (ayuntamientos, CCAA) que acceden a los mapas de calor para planificación urbana.

**Objetivos específicos**:
1. Facilitar la identificación de áreas con alta concentración de residuos por comunidad autónoma, provincia y municipio.
2. Prevenir la acumulación de residuos en zonas sensibles.
3. Analizar patrones temporales (estacionalidad) y de tipología de residuos.

---

## 📁 Ubicación de archivos

Todos los dashboards de este módulo deben crearse dentro de la carpeta `Reporting/HeatMaps/`:

### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/HeatMaps/
├── WasteDensityHeatMap.razor                → /reporting/heat-maps/waste-density
├── WastePatternAnalysis.razor               → /reporting/heat-maps/pattern-analysis
└── PublicEntityHeatMapView.razor            → /reporting/heat-maps/public-view
```

### Capa Application (CQRS)

```
Application/Features/Reporting/HeatMaps/
├── Queries/
│   ├── GetWasteDensityHeatMapQuery.cs       → Dashboard A (SCRAP)
│   ├── GetWastePatternAnalysisQuery.cs      → Dashboard B (SCRAP — análisis temporal)
│   ├── GetPublicEntityHeatMapQuery.cs       → Dashboard C (PUBLIC_ENT)
│   ├── GetWasteFrequencyByPickupPointQuery.cs → Widget compartido: frecuencia de recogida
│   ├── GetSeasonalityAnalysisQuery.cs       → Widget compartido: estacionalidad
│   └── ExportHeatMapDataToExcelQuery.cs     → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── WasteDensityHeatMapDto.cs
│   ├── WastePatternAnalysisDto.cs
│   ├── PublicEntityHeatMapDto.cs
│   ├── WasteFrequencyByPickupPointDto.cs
│   ├── SeasonalityAnalysisDto.cs
│   └── HeatMapExportDto.cs
└── Services/
    └── HeatMapAggregationService.cs         → Servicio de agregación geoespacial
```

### Componentes reutilizables

```
Web/Components/Shared/HeatMaps/
├── WasteHeatMapLayer.razor                  → Capa de heatmap de densidad sobre mapa Leaflet
├── SeasonalityChart.razor                   → Gráfico de estacionalidad (line/area chart)
├── FrequencyByPointTable.razor              → Tabla de frecuencia por punto de recogida
└── WasteTypologyDonut.razor                 → Donut de tipología de residuos (por código LER)
```

---

## 📊 Dashboards a crear (son TRES vistas diferenciadas)

### Dashboard HM-A — **Mapa de Calor de Densidad de Residuos — SCRAP** (`/reporting/heat-maps/waste-density`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Policy de autorización**: `CanViewHeatMapWasteDensity` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: vista estratégica para que los SCRAPs visualicen la distribución geográfica de la densidad de residuos en los puntos de recogida bajo su ámbito, identificando zonas de alta concentración y patrones por tipología.

#### Widgets / KPIs requeridos:

1. **Mapa de calor de densidad de residuos** (mapa interactivo con capa heatmap)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `ServiceOrders` → coordenadas del punto de recogida vía `ServiceOrders.IdPickupPoint` → `Entities.Latitude` / `Entities.Longitude`.
   - Capa heatmap: intensidad basada en `SUM(WasteMoveResidues.Weight)` por punto de recogida georreferenciado.
   - Filtrable por `LERCodes.Code` (flujo de residuo) para aislar tipologías concretas.
   - Capa adicional: polígonos de `DUMZones.GeometryJson` con semáforo de `DUMRestrictionRules.ActionType` (`Block` = rojo, `Restrict` = naranja, `Allow` = verde, `Notify` = azul).
   - Al hacer clic en un cluster/punto: popup con nombre de la entidad, dirección, kg totales del periodo, nº de recogidas, tipología predominante (código LER más frecuente) y última recogida.

2. **Densidad de residuos por zona geográfica** (bar chart / treemap)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `ServiceOrders` → agrupar por `Entities.ProvinceCode` o `Entities.MunicipalityCode` del `IdPickupPoint`.
   - Métricas: `SUM(WasteMoveResidues.Weight)` agrupado por zona y `LERCodes.Code`.
   - Drill-down: de Comunidad Autónoma → Provincia → Municipio.

3. **Tipología de residuos por zona** (donut chart + tabla)
   - Fuente: `WasteMoveResidues` JOIN `Residues` JOIN `LERCodes`.
   - Distribución porcentual por `LERCodes.Code` (capítulo/subcapítulo) del peso total recogido.
   - Indicar `LERCodes.IsDangerous` con icono de advertencia.

4. **Top 20 puntos de recogida por volumen** (tabla ranking)
   - Fuente: `ServiceOrders.IdPickupPoint` → `Entities`, `SUM(WasteMoveResidues.Weight)`.
   - Columnas: nombre entidad, municipio, provincia, kg totales, nº recogidas, kg promedio por recogida, tipología predominante.
   - Ordenado por kg totales descendente. Semáforo: rojo > percentil 90, naranja P75–P90, verde < P75.

5. **Frecuencia de recogida por punto** (card + sparklines)
   - Fuente: `COUNT(ServiceOrders)` agrupado por `IdPickupPoint` y periodo (semana/mes).
   - KPIs:
     - **Frecuencia promedio** = nº medio de recogidas por punto de recogida por mes.
     - **Puntos con frecuencia anómala** = puntos cuya frecuencia supera 2× la media.
   - Sparkline de evolución mensual.

6. **Comparativa de densidad entre periodos** (bar chart comparativo)
   - Permite seleccionar dos periodos (mes A vs mes B, o trimestre A vs trimestre B).
   - Compara por zona geográfica: kg totales, nº recogidas, kg/recogida promedio.

#### Filtros globales del dashboard:
- `Year`, `Month` / `Quarter`.
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `LERCodes.Code` (filtro por flujo/tipología de residuo).
- `WasteStream` (para aislar flujos concretos: RAEE, envases, etc.).
- `IdScrap`: **ADMIN** puede ver todos; **SCRAP** ve solo los suyos (filtrado por `WasteMoves.IdScrap` o `WasteMoves.IdScrap2`).

---

### Dashboard HM-B — **Análisis de Patrones y Estacionalidad — SCRAP** (`/reporting/heat-maps/pattern-analysis`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Policy de autorización**: `CanViewHeatMapPatternAnalysis` (nueva).

**Propósito**: análisis temporal de los patrones de generación de residuos: estacionalidad, tendencias, picos y valles para optimizar la planificación de recogidas y anticipar acumulaciones.

#### Widgets / KPIs requeridos:

1. **Heatmap temporal de generación de residuos** (heatmap 12 meses × tipología)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` → `SUM(Weight)` agrupado por mes y `LERCodes.Code` (capítulo).
   - Matriz de calor: eje X = meses del año, eje Y = tipología de residuo, intensidad = kg totales.
   - Permite identificar estacionalidad por tipo de residuo.

2. **Tendencia de volumen mensual** (line chart multi-serie)
   - Series separadas por `LERCodes.Code` (top 5 tipologías por volumen).
   - Línea de tendencia con media móvil de 3 meses.
   - Eje Y: kg totales. Eje X: meses.

3. **Heatmap semanal de frecuencia de recogidas** (heatmap 7×24)
   - Fuente: `ServiceOrders.PlannedPickupStart` o `WasteMoves.ActualPickupStart`, agrupado por día de semana y franja horaria.
   - Intensidad: nº de recogidas por celda.
   - Objetivo: identificar concentraciones horarias para redistribuir.

4. **Índice de concentración geográfica** (card + gauge)
   - Métrica calculada: coeficiente de Gini o índice de Herfindahl sobre la distribución de kg por punto de recogida.
   - Valor alto = residuos muy concentrados en pocos puntos → riesgo de acumulación.
   - Valor bajo = distribución más uniforme.
   - Comparativa con periodo anterior (% variación).

5. **Alertas de acumulación** (panel tipo inbox)
   - Motor de reglas simple que genera alertas basadas en umbrales (calculadas en backend):
     - Si un punto de recogida supera X kg sin recogida en Y días → "Punto [nombre] en [municipio] acumula [kg] sin recogida desde [fecha]".
     - Si un municipio supera el percentil 95 de densidad → "Municipio [nombre] presenta concentración anormalmente alta".
     - Si la frecuencia de recogida baja más de un 30% respecto al periodo anterior → "Frecuencia de recogida reducida en [zona]".
   - Las alertas se generan en el backend (`HeatMapAggregationService.cs`), no en el cliente.

#### Filtros globales:
- `Year`, `Month` / `Quarter`.
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `LERCodes.Code` (flujo de residuo).
- `WasteStream`.

---

### Dashboard HM-C — **Vista de Mapas de Calor para Entidades Públicas** (`/reporting/heat-maps/public-view`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Policy de autorización**: `CanViewHeatMapPublicView` (nueva).

**Propósito**: los entes públicos (ayuntamientos, diputaciones) acceden a mapas de calor de su ámbito territorial para identificar zonas de alta densidad de residuos, analizar patrones de generación y planificar actuaciones preventivas.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo territorial** (cards de KPI)
   - **Kg totales recogidos** en su ámbito territorial (municipio): `SUM(WasteMoveResidues.Weight)` WHERE `Entities.MunicipalityCode` del `IdPickupPoint` = municipio de la entidad pública logueada.
   - **Nº de puntos de recogida activos**: `COUNT(DISTINCT ServiceOrders.IdPickupPoint)` del periodo.
   - **Tipología predominante**: código LER con mayor peso acumulado.
   - **Frecuencia media de recogida**: recogidas/punto/mes.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Mapa de calor territorial** (mapa interactivo)
   - Fuente: misma que HM-A pero filtrado por `Entities.MunicipalityCode` = municipio de la entidad pública logueada (o `ServiceOrders.IdIssuedBy = LinkedEntityId`).
   - Capa heatmap de densidad por kg en puntos de recogida de su municipio.
   - Al hacer clic: popup con detalle del punto.

3. **Distribución de residuos por tipología** (donut chart)
   - Distribución porcentual por `LERCodes.Code` del peso total recogido en su ámbito.

4. **Evolución temporal de recogidas** (line chart mensual)
   - Eje Y: kg totales. Eje X: meses.
   - Líneas separadas por SCRAP (si hay más de uno operando en el municipio).
   - Permite detectar estacionalidad a nivel municipal.

5. **Detalle de puntos de recogida** (tabla con exportación)
   - Columnas: nombre del punto, dirección, kg periodo, nº recogidas, última recogida, tipología predominante.
   - Exportable a XLSX.

6. **Indicadores de zonas sensibles** (tabla + semáforo)
   - Puntos de recogida cercanos a zonas sensibles (cruce con `DUMZones` si aplica, o configuración estática de zonas de interés: centros escolares, hospitales, zonas peatonales).
   - Semáforo: rojo si el punto supera umbral de acumulación en zona sensible.

#### Filtros:
- `Year`, `Month`.
- `LERCodes.Code` (flujo de residuo).
- `WasteStream`.
- `IdScrap` (los SCRAPs que operan en su municipio — derivado de `WasteMoves` históricas o `Agreements`).

---

## 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el módulo de Mapas de Calor se alimenta de las tablas existentes del modelo v4.1. Las métricas derivadas (frecuencia, índice de concentración, alertas) se calculan en las Queries CQRS y en `HeatMapAggregationService.cs`.

| Tabla | Campos principales para este módulo |
|-------|--------------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `Latitude`, `Longitude`, `ProvinceCode`, `MunicipalityCode`, `CenterCode` |
| `ServiceOrders` | `Id`, `Status`, `IdPickupPoint`, `IdIssuedBy`, `PlannedPickupStart`, `PlannedPickupEnd`, `WasteStream`, `IdLERCode`, `OwnerId` |
| `WasteMoves` | `Id`, `IdSource`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `ServiceStatus`, `ActualPickupStart`, `ActualPickupEnd`, `PlannedPickupStart`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `IdResidue`, `Weight`, `MeasureUnit`, `VehicleType`, `TransportInfo_TransportDistance`, `TransportInfo_TransportCarbonEmissions` |
| `Residues` | `Id`, `Name`, `Reference`, `ResidueType`, `IdLERCode`, `IdProducer` |
| `LERCodes` | `Id`, `Code`, `Description`, `IsDangerous`, `ChapterCode`, `SubChapterCode` |
| `DUMZones` | `Id`, `ZoneCode`, `GeometryJson`, `Status` |
| `DUMRestrictionRules` | `ZoneId`, `ConditionsJson`, `ActionType`, `ValidFrom`, `ValidTo` |
| `Agreements` | `Id`, `IdScrap`, `IdCoordinator`, `IdPublicEntity` |
| `EntryPlants` | `PlantEntryDate`, `NetWeight`, `ServiceOrderId`, `OwnerId` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `WasteMoveReference` |

---

## 🔒 Reglas de autorización y filtrado de datos

> **IMPORTANTE**: El acceso a estos dashboards se gestiona mediante el **sistema de autorización por pantalla configurable desde la interfaz de administración** (`/security/page-permissions`) utilizando las tablas `PageDefinitions` y `PagePermissions`. **No se hardcodea el acceso en código.** Las policies en código actúan como mínimo de seguridad estático; el control fino se delega al sistema dinámico de BD.

### Permisos por perfil y filtrado de datos

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | HM-A, HM-B | Solo ve datos de recogidas donde `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. |
| `PUBLIC_ENT` | HM-C | Solo ve recogidas cuyo punto de recogida (`ServiceOrders.IdPickupPoint` → `Entities.MunicipalityCode`) pertenece a su municipio, o cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`). |
| `DISPATCH_OFFICE` | HM-A, HM-B, HM-C | Ve todos los datos del tenant (`OwnerId`). Visión completa equivalente a ADMIN dentro de su ámbito operativo. |
| `ADMIN` | HM-A, HM-B, HM-C | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, entidad pública o municipio. |

### Patrón de filtrado de datos

Usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` (ya implementado en la capa `Application`).

En cada Query handler:

```csharp
public async Task<WasteDensityHeatMapDto> Handle(GetWasteDensityHeatMapQuery request, CancellationToken ct)
{
    var query = _db.WasteMoves
        .Where(wm => wm.OwnerId == _currentUser.OwnerId)  // 1. Filtro multi-tenant
        .AsQueryable();

    // 2. Filtro por perfil (datos propios)
    if (_currentUser.IsInAnyProfile("SCRAP"))
    {
        var entityId = _currentUser.LinkedEntityId;
        query = query.Where(wm => wm.IdScrap == entityId || wm.IdScrap2 == entityId);
    }
    else if (_currentUser.IsInAnyProfile("PUBLIC_ENT"))
    {
        var entityId = _currentUser.LinkedEntityId;
        query = query.Where(wm =>
            wm.ServiceOrder.IdPickupPoint.HasValue &&
            wm.ServiceOrder.PickupPoint.MunicipalityCode == _currentUser.MunicipalityCode
            || wm.ServiceOrder.IdIssuedBy == entityId);
    }
    // DISPATCH_OFFICE y ADMIN: sin filtro adicional (ven todo el tenant)

    // ... resto de la consulta de agregación ...
}
```

### Control de acceso a pantallas

Los permisos se gestionan dinámicamente desde `/security/page-permissions` mediante el sistema `PageDefinitions`/`PagePermissions`. La tabla anterior documenta la **configuración recomendada por defecto** que el administrador debe aplicar tras el despliegue.

Las policies en código (`CanViewHeatMapWasteDensity`, `CanViewHeatMapPatternAnalysis`, `CanViewHeatMapPublicView`) actúan como **mínimo de seguridad estático**.

---

## 🔧 Policies de autorización nuevas (a registrar en `PolicyConstants.cs` + `Program.cs`)

```
Policy                              Perfiles permitidos
──────────────────────────────────── ────────────────────────────────────────────
CanViewHeatMapWasteDensity          SCRAP, DISPATCH_OFFICE, ADMIN
CanViewHeatMapPatternAnalysis       SCRAP, DISPATCH_OFFICE, ADMIN
CanViewHeatMapPublicView            PUBLIC_ENT, DISPATCH_OFFICE, ADMIN
```

---

## 📋 Actualización de la Matriz de Permisos (§4.4 — REPORTING)

Se añaden las siguientes filas a la tabla de Reporting:

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Dash. Mapa Calor Densidad** | `WasteMoveResidues`, `Entities`, `LERCodes` | — | — | **R** | — | — | — | — | **R** | R |
| **Dash. Mapa Calor Patrones** | `WasteMoveResidues`, `ServiceOrders`, `LERCodes` | — | — | **R** | — | — | — | — | **R** | R |
| **Dash. Mapa Calor Público** | `WasteMoveResidues`, `Entities`, `LERCodes` | — | — | — | **R** | — | — | — | **R** | R |

---

## 📋 Pantalla nueva en la matriz de `PageDiscoveryService`

### Actualizar `InferModuleName()` en `Infrastructure/Services/PageDiscoveryService.cs`

Las rutas `/reporting/heat-maps/` deben mapear al módulo **Reporting** (ya cubierto por la regla existente `Reporting` · `/traceability` · `/kpis` · `/documents`). Si la ruta no es inferida automáticamente, añadir:

| Namespace / Ruta | Módulo asignado |
|---|---|
| `Reporting/HeatMaps` · `/reporting/heat-maps/` | Reporting |

### Actualizar `HumanizeName()` con nombres legibles en español

| Componente | Nombre legible |
|---|---|
| `WasteDensityHeatMap` | `Mapa de Calor — Densidad de Residuos` |
| `WastePatternAnalysis` | `Mapa de Calor — Patrones y Estacionalidad` |
| `PublicEntityHeatMapView` | `Mapa de Calor — Vista Entidad Pública` |

---

## 🏗️ Patrones de implementación Blazor

### Patrón de autorización en las páginas

Cada página usa `@attribute [Authorize(Policy = ...)]` como mínimo de seguridad estático. El acceso efectivo se controla desde `PagePermissions` en BD:

```razor
@* WasteDensityHeatMap.razor *@
@page "/reporting/heat-maps/waste-density"
@attribute [Authorize(Policy = PolicyConstants.CanViewHeatMapWasteDensity)]

@* Los filtros y widgets se cargan condicionalmente según el perfil *@
```

```razor
@* WastePatternAnalysis.razor *@
@page "/reporting/heat-maps/pattern-analysis"
@attribute [Authorize(Policy = PolicyConstants.CanViewHeatMapPatternAnalysis)]
```

```razor
@* PublicEntityHeatMapView.razor *@
@page "/reporting/heat-maps/public-view"
@attribute [Authorize(Policy = PolicyConstants.CanViewHeatMapPublicView)]
```

### Entrada en `NavMenu.razor`

Añadir en la sección de Reporting, condicionada por `IPagePermissionService`:

```razor
@* Sección Mapas de Calor dentro de Reporting *@
@if (await PagePermissionService.CanAccessRouteAsync(userProfile, "/reporting/heat-maps/waste-density"))
{
    <NavLink href="/reporting/heat-maps/waste-density">Mapa de Calor — Densidad</NavLink>
}
@if (await PagePermissionService.CanAccessRouteAsync(userProfile, "/reporting/heat-maps/pattern-analysis"))
{
    <NavLink href="/reporting/heat-maps/pattern-analysis">Mapa de Calor — Patrones</NavLink>
}
@if (await PagePermissionService.CanAccessRouteAsync(userProfile, "/reporting/heat-maps/public-view"))
{
    <NavLink href="/reporting/heat-maps/public-view">Mapa de Calor — Vista Pública</NavLink>
}
```

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
4. El mapa interactivo reutiliza el componente de mapa ya implementado (`WasteVolumeMap.razor`), añadiendo la capa de heatmap de densidad (`WasteHeatMapLayer.razor`).
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en HM-A y HM-C (patrón ya implementado con ClosedXML en `ExportKpisToExcelQuery.cs`).
7. Responsive mobile-first.
8. Modo oscuro/claro (consistente con `MainLayout.razor`).
9. Las alertas de acumulación se generan en el backend (`HeatMapAggregationService.cs`), no en el cliente.
10. Los umbrales de alertas son configurables (en `appsettings.json` o como parámetros del sistema).
11. **No se crean nuevas entidades de dominio**. Todo se implementa con las entidades del modelo v4.1 existente.
12. El acceso a cada dashboard **no está hardcodeado en código**. Se gestiona mediante el sistema de autorización por pantalla (`PageDefinitions`/`PagePermissions`) configurable desde `/security/page-permissions`.
13. Cada usuario solo ve datos de las entidades asignadas a él o creadas por él, a excepción de `ADMIN` y `DISPATCH_OFFICE` que ven todos los datos del tenant.

---

## 🔗 Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de densidad y frecuencia pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **Dashboard Optimización Logística (UC2)**: el mapa de calor comparte fuentes de datos con el mapa interactivo del Dashboard 1. Reutilizar componentes: `WasteVolumeMap.razor`, `DUMComplianceDonut.razor`.
- **Dashboard Movilidad Urbana (UC3)**: los datos de densidad y frecuencia complementan el análisis de impacto en movilidad. Enlace cruzado desde HM-A al UC3-A para correlacionar densidad con conflicto de movilidad.
- **Trazabilidad (§5.1)**: desde cualquier punto de recogida en los mapas de calor, enlace directo a `/traceability?term={WasteMoveReference}`.
- **KPIs regulatorios (§5.2)**: los KPIs de volumen por zona complementan los indicadores regulatorios existentes en `/kpis`.

---

## ✅ Checklist al crear estas páginas

- [ ] `@page "/reporting/heat-maps/..."` definida en cada componente.
- [ ] `@attribute [Authorize(Policy = PolicyConstants.CanViewHeatMap...)]` con policy adecuada.
- [ ] Policies nuevas añadidas en `PolicyConstants.cs` + registradas en `Program.cs`.
- [ ] Namespace: `Pages/Reporting/HeatMaps/` para que `InferModuleName()` clasifique como Reporting.
- [ ] Si la ruta no mapea automáticamente → actualizar `InferModuleName()`.
- [ ] Nombres legibles añadidos en `HumanizeName()`.
- [ ] Entradas añadidas en `NavMenu.razor` en la sección Reporting con consulta a `IPagePermissionService`.
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions`.
- [ ] Verificar que `ADMIN` y `DISPATCH_OFFICE` ven todos los datos del tenant.
- [ ] Verificar que `SCRAP` solo ve datos de sus traslados (`IdScrap` / `IdScrap2`).
- [ ] Verificar que `PUBLIC_ENT` solo ve datos de su municipio.
