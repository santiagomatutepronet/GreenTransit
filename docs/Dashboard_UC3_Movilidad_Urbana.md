# Prompt para GitHub Copilot — Dashboard UC2: Impacto de Recogida RAEE en Movilidad Urbana

> **Instrucción**: Adjunta este archivo junto con `README.md`, `Crear_BD_v4_1.sql` y `COPILOT_CONTEXT.md` al inicio de la sesión de Copilot Chat.

---

## 📎 Contexto del proyecto

GreenTransit es una plataforma web multi-rol, multi-tenant (`OwnerId`) construida con **Blazor Server (.NET 10)**, **CQRS (MediatR)**, **SQL Server Azure** y **ApexCharts/Chart.js**. El modelo de datos es el **v4.1**. El sistema ya tiene implementados:

- Tres dashboards logísticos (Paso 9 del `COPILOT_CONTEXT.md`):
  - Dashboard 1 — Optimización Logística SCRAP (`/logistics/optimization`)
  - Dashboard 2 — Monitorización Pública (`/logistics/public-monitoring`)
  - Dashboard 3 — Panel Operativo (`/logistics/operations`)
- Módulo de KPIs regulatorios (`/kpis`)
- Sistema de autorización por policies (`PolicyConstants.cs`) y filtrado por `ICurrentUserService.LinkedEntityId`
- Patrón CQRS completo: Query → Handler → DTO → Razor page
- Exportación XLSX con ClosedXML

---

## 🎯 Objetivo — Caso de Uso 2 (UC2)

Crear un **nuevo módulo de dashboard** que evalúe el **impacto de los servicios de recogida RAEE sobre la movilidad urbana**. El objetivo es que los datos operativos de GreenTransit (rutas, horarios, volúmenes, zonas DUM) se crucen con indicadores de movilidad para que:

1. **Los Coordinadores (Clústeres Logísticos)** analicen las variables logísticas que afectan a la movilidad y propongan optimizaciones a los SCRAPs.
2. **Los Ayuntamientos** monitoricen el impacto que las recogidas RAEE tienen sobre la congestión, el transporte público y las operaciones de mantenimiento urbano de su municipio.
3. **La Oficina de Asignación y Coordinación de SCRAP RAEE** provea los datos necesarios para evaluar dicho impacto y coordine la planificación de recogidas para minimizar interferencias con la movilidad.

Este caso de uso se alinea con programas como **Living Lab**, que buscan soluciones de movilidad avanzada sostenible coordinando empresas tecnológicas, operadores logísticos y ayuntamientos.

---

## 📊 Dashboards a crear (son DOS vistas diferenciadas + una vista compartida)

### Dashboard UC3-A — **Panel de Análisis de Impacto en Movilidad — Coordinador** (`/mobility/coordinator-analysis`)

**Destinado a**: perfiles `COORDINATOR` y `ADMIN`.

**Policy de autorización**: `CanViewMobilityCoordinatorAnalysis` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: vista estratégica para que los Clústeres Logísticos analicen el impacto de las recogidas RAEE en la movilidad urbana y propongan optimizaciones.

#### Widgets / KPIs requeridos:

1. **Mapa de densidad de recogidas vs zonas sensibles de movilidad** (mapa interactivo)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `Entities` (punto de recogida con `Latitude`/`Longitude`).
   - Capa 1: heatmap de densidad de recogidas por geolocalización (agrupación por `Entities.MunicipalityCode` del `IdSource`).
   - Capa 2: polígonos de `DUMZones.GeometryJson` con semáforo de `DUMRestrictionRules.ActionType` (`Block` = rojo, `Restrict` = naranja, `Allow` = verde, `Notify` = azul).
   - Capa 3 (datos parametrizables): zonas de interés de movilidad (estaciones de transporte público, obras de mantenimiento, zonas peatonales) — inicialmente como capa estática configurable, preparada para integración futura con APIs municipales.
   - Al hacer clic en un cluster: popup con nº de recogidas del periodo, kg totales, horario predominante y % de recogidas dentro/fuera de ventana DUM.

2. **Distribución temporal de recogidas vs horas pico de movilidad** (heatmap semanal 7×24)
   - Fuente: `WasteMoves.ActualPickupStart` (si disponible) o `WasteMoves.PlannedPickupStart`, agrupado por día de semana y franja horaria.
   - Superposición visual: franjas de hora pico de tráfico urbano (configurable, por defecto 07:30–09:30 y 17:30–19:30) marcadas como bandas rojas en el heatmap.
   - KPI derivado: **% de recogidas en hora pico** = recogidas cuyo inicio cae en franja pico / total recogidas.

3. **Índice de conflicto logístico-movilidad por municipio** (tabla ranking + sparklines)
   - Fuente: cruce de `WasteMoves` + `DUMRestrictionRules` + datos temporales.
   - Métricas por municipio:
     - Nº total de recogidas en el periodo.
     - % de recogidas en hora pico.
     - % de recogidas fuera de ventana DUM.
     - Nº de incidencias logísticas (`Incidents` WHERE `Type IN ('Retraso', 'AveriaVehiculo')`) en el municipio.
   - **Índice de conflicto** = ponderación configurable de las métricas anteriores (0–100).
   - Ordenado por índice descendente. Semáforo: rojo > 70, naranja 40–70, verde < 40.

4. **Comparativa de eficiencia pre/post optimización** (bar chart comparativo)
   - Fuente: `WasteMoveResidues` → `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`.
   - Permite seleccionar dos periodos (mes A vs mes B) y comparar:
     - km promedio por recogida
     - minutos promedio por recogida
     - kgCO₂e por tonelada transportada
     - % de recogidas en hora pico
   - Objetivo: evidenciar mejoras tras aplicar recomendaciones del Clúster.

5. **Recomendaciones automáticas** (panel con reglas de negocio)
   - Motor de reglas simple que genera sugerencias textuales basadas en umbrales:
     - Si % recogidas en hora pico > 30% → "Considerar redistribuir las recogidas del municipio X fuera de la franja 07:30–09:30".
     - Si % fuera de ventana DUM > 20% → "Revisar la planificación de rutas en la zona DUM Y".
     - Si índice de conflicto > 70 → "Municipio Z requiere intervención prioritaria de coordinación".
   - Las recomendaciones se generan en el backend (Query handler), no en el cliente.

#### Filtros globales:
- `Year`, `Month`/`Quarter`.
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `IdScrap` (el coordinador ve transversalmente los SCRAPs de sus acuerdos — filtrar por `Agreements` donde participa como `IdCoordinator`).
- `WasteStream` (fijo en `RAEE` por defecto, pero configurable).

---

### Dashboard UC3-B — **Panel de Monitorización de Movilidad — Ayuntamiento** (`/mobility/municipal-monitoring`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Policy de autorización**: `CanViewMobilityMunicipalMonitoring` (nueva).

**Propósito**: los ayuntamientos monitorizan cómo los servicios de recogida RAEE afectan a la movilidad de su municipio y verifican el cumplimiento de las ventanas horarias acordadas.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo de impacto** (cards de KPI)
   - **Recogidas totales del periodo** en el municipio: `COUNT(WasteMoves)` WHERE `Entities.MunicipalityCode` del punto de recogida = municipio del ayuntamiento.
   - **Kg RAEE recogidos**: `SUM(WasteMoveResidues.Weight)`.
   - **% de recogidas en horario compatible con movilidad**: recogidas FUERA de hora pico / total.
   - **% de cumplimiento DUM**: recogidas dentro de ventana DUM / total con zona DUM aplicable.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Calendario de recogidas planificadas** (vista semanal/mensual)
   - Fuente: `ServiceOrders` WHERE `Status IN ('Pending', 'Scheduled')` AND `IdPickupPoint` → `Entities.MunicipalityCode` = municipio del ayuntamiento.
   - Visualización tipo calendario con las recogidas planificadas, coloreadas por:
     - Verde: dentro de ventana DUM y fuera de hora pico.
     - Naranja: fuera de ventana DUM o en hora pico.
     - Rojo: fuera de ventana DUM Y en hora pico.
   - Permite al ayuntamiento anticipar posibles conflictos con operaciones municipales (obras, eventos, desvíos).

3. **Histórico de recogidas vs incidencias de movilidad** (line chart dual axis)
   - Eje izquierdo: nº de recogidas por semana/mes.
   - Eje derecho: nº de incidencias logísticas (`Incidents` del periodo en el municipio).
   - Líneas separadas por SCRAP (si hay más de uno operando).
   - Objetivo: correlacionar picos de actividad de recogida con incidencias.

4. **Detalle de cumplimiento por SCRAP** (tabla)
   - Fuente: `WasteMoves` JOIN `ServiceOrders` WHERE `ServiceOrders.IdIssuedBy` = entidad pública logueada o `IdPickupPoint` en su municipio.
   - Columnas por SCRAP: nº traslados, kg totales, % en hora pico, % cumplimiento DUM, nº incidencias, índice de conflicto.
   - Semáforo por fila según índice de conflicto.

5. **Notificaciones activas** (lista tipo inbox)
   - Alertas generadas cuando:
     - Se planifica una recogida en hora pico en su municipio.
     - Se supera el umbral de % fuera de ventana DUM (configurable).
     - Hay una incidencia abierta en su municipio (`Incidents.ClosedAt IS NULL`).
   - Fuente: generadas por el backend al crear/actualizar `ServiceOrders` y `Incidents`.

#### Filtros:
- `Year`, `Month`.
- `IdScrap` (los SCRAPs que operan en su municipio — derivado de `WasteMoves` históricas o `Agreements`).
- `WasteStream` (por defecto `RAEE`).

---

### Vista compartida UC3-C — **Datos de Impacto RAEE en Movilidad — Oficina de Asignación** (`/mobility/dispatch-data`)

**Destinado a**: perfiles `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewMobilityDispatchData` (nueva).

**Propósito**: la Oficina de Asignación y Coordinación de SCRAP RAEE provee los datos operativos necesarios para la evaluación del impacto en movilidad y ajusta la planificación.

#### Widgets / KPIs requeridos:

1. **Panel de datos exportables para análisis externo** (tabla con exportación XLSX)
   - Fuente: `WasteMoves` + `WasteMoveResidues` + `ServiceOrders` + `Entities` + `DUMZones`.
   - Dataset plano con campos: fecha recogida, municipio, provincia, SCRAP, tipo vehículo, peso, distancia, duración, emisiones CO₂, código zona DUM, cumplimiento DUM (sí/no), en hora pico (sí/no).
   - Exportable a XLSX (patrón ClosedXML existente).
   - Objetivo: proveer datos limpios y estructurados a los Coordinadores y ayuntamientos para análisis externos (Living Lab, estudios de movilidad).

2. **Resumen operativo por SCRAP** (cards agrupadas)
   - Para cada SCRAP bajo su coordinación:
     - Nº de recogidas del periodo.
     - % en hora pico.
     - % cumplimiento DUM.
     - Nº incidencias abiertas.
   - Con drill-down al detalle.

3. **Planificación semanal con indicadores de movilidad** (calendario + semáforo)
   - Reutiliza el patrón de planificación del Dashboard 3 operativo (`ServiceOrders` de los próximos 7 días).
   - Añade indicadores visuales de conflicto: las SO planificadas en hora pico o fuera de ventana DUM se marcan con semáforo.
   - Permite a la oficina reasignar horarios antes de confirmar la planificación.

4. **Evolución mensual de métricas de movilidad** (line chart multi-serie)
   - Series: % recogidas en hora pico, % cumplimiento DUM, índice de conflicto promedio.
   - Fuente: agregación mensual de las métricas ya calculadas.
   - Objetivo: ver tendencia a lo largo del año para informar a programas como Living Lab.

#### Filtros:
- `Year`, `Month`.
- `IdScrap`.
- `ProvinceCode` / `MunicipalityCode`.

---

## 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el UC3 se alimenta de las tablas existentes del modelo v4.1. Las nuevas métricas (% hora pico, índice de conflicto) se calculan en las Queries CQRS.

| Tabla | Campos principales para este UC3 |
|-------|----------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `Latitude`, `Longitude`, `ProvinceCode`, `MunicipalityCode` |
| `ServiceOrders` | `Id`, `Status`, `IdPickupPoint`, `IdIssuedBy`, `PlannedPickupStart`, `PlannedPickupEnd`, `WasteStream`, `OwnerId` |
| `WasteMoves` | `Id`, `IdSource`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `ServiceStatus`, `ActualPickupStart`, `ActualPickupEnd`, `PlannedPickupStart`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `Weight`, `VehicleType`, `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions` |
| `DUMZones` | `Id`, `ZoneCode`, `GeometryJson`, `Status` |
| `DUMRestrictionRules` | `ZoneId`, `ConditionsJson`, `ActionType`, `ValidFrom`, `ValidTo` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `WasteMoveReference` |
| `Agreements` | `Id`, `IdScrap`, `IdCoordinator`, `IdPublicEntity` |
| `EntryPlants` | `PlantEntryDate`, `NetWeight`, `ServiceOrderId` |

---

## 🔒 Reglas de autorización y filtrado de datos

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `COORDINATOR` | UC3-A | Ve transversalmente los SCRAPs vinculados a sus acuerdos (`Agreements.IdCoordinator = LinkedEntityId`). |
| `PUBLIC_ENT` | UC3-B | Solo ve recogidas cuyo punto de recogida (`IdPickupPoint` → `Entities.MunicipalityCode`) pertenece a su municipio, o cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`). |
| `DISPATCH_OFFICE` | UC3-C | Ve todos los traslados del tenant (`OwnerId`). |
| `ADMIN` | UC3-A, UC3-B, UC3-C | Sin restricciones dentro del tenant. Puede filtrar por cualquier coordinador, ayuntamiento o SCRAP. |

**Patrón de filtrado**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` (ya implementado).

Estas regals de autorización deben segir el patron de autorización definido en PageDefinitions, no aplicar reglas hardcodeadas, dar permisos a quien se indica siguiendo esta funcionalidad
---

## 🏗️ Arquitectura de implementación

### Capa Application (CQRS)

```
Application/Features/Mobility/
├── Queries/
│   ├── GetMobilityCoordinatorAnalysisQuery.cs   → UC2-A (COORDINATOR)
│   ├── GetMobilityMunicipalMonitoringQuery.cs    → UC2-B (PUBLIC_ENT)
│   ├── GetMobilityDispatchDataQuery.cs           → UC2-C (DISPATCH_OFFICE)
│   ├── GetMobilityConflictIndexQuery.cs          → Widget compartido: índice de conflicto
│   ├── GetPeakHourComplianceQuery.cs             → Widget compartido: % hora pico
│   └── ExportMobilityDataToExcelQuery.cs         → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── MobilityCoordinatorAnalysisDto.cs
│   ├── MobilityMunicipalMonitoringDto.cs
│   ├── MobilityDispatchDataDto.cs
│   ├── MobilityConflictIndexDto.cs
│   ├── PeakHourComplianceDto.cs
│   └── MobilityRecommendationDto.cs
└── Services/
    └── MobilityRecommendationEngine.cs           → Motor de reglas para recomendaciones
```

### Capa Web (Blazor)

```
Web/Components/Pages/Mobility/
├── CoordinatorAnalysis.razor          → /mobility/coordinator-analysis
├── MunicipalMonitoring.razor          → /mobility/municipal-monitoring
└── DispatchData.razor                 → /mobility/dispatch-data
```

### Componentes reutilizables (complementan los ya existentes)

- `MobilityHeatmap.razor` — heatmap 7×24 con bandas de hora pico configurables.
- `ConflictIndexTable.razor` — tabla con ranking de municipios por índice de conflicto + sparklines.
- `PickupCalendar.razor` — calendario de recogidas con semáforo de movilidad.
- `MobilityRecommendations.razor` — panel de recomendaciones automáticas.

> **Reutilizar** de los dashboards existentes: `WasteVolumeMap.razor`, `DUMComplianceDonut.razor`, `EmissionsCard.razor`, `IncidentsBadge.razor`.

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
4. El mapa interactivo reutiliza el componente de mapa ya implementado, añadiendo la capa de heatmap de densidad.
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en UC2-C (patrón ya implementado con ClosedXML).
7. Responsive mobile-first.
8. Modo oscuro/claro (consistente con `MainLayout.razor`).
9. Las **franjas de hora pico** son configurables (no hardcodeadas): se almacenan como parámetros del sistema o como constantes en `appsettings.json`.
10. El **índice de conflicto** usa pesos configurables para cada métrica (inicialmente: 40% hora pico + 30% fuera DUM + 20% incidencias + 10% volumen relativo).
11. Las recomendaciones se generan en el backend (`MobilityRecommendationEngine.cs`), no en el cliente.

---

## 🔗 Integración con módulos existentes

- **Dashboard 1 (Optimización Logística)**: el widget de cumplimiento DUM y el mapa interactivo comparten fuentes de datos. Los componentes `DUMComplianceDonut.razor` y `WasteVolumeMap.razor` se reutilizan.
- **Dashboard 2 (Monitorización Pública)**: el UC2-B extiende la visión del ayuntamiento añadiendo la perspectiva de movilidad. Enlace cruzado desde UC2-B al Dashboard 2 para ver liquidaciones y servicios prestados.
- **Dashboard 3 (Panel Operativo)**: el UC2-C complementa la planificación semanal añadiendo indicadores de conflicto de movilidad.
- **Trazabilidad (§5.1)**: desde cualquier recogida en los dashboards UC2, enlace directo a `/traceability?term={WasteMoveReference}`.
- **Incidencias (§4.3)**: los widgets de incidencias enlazan a `/incidents/{id}`.
- **Zonas DUM (§4.4)**: reutilización completa de la capa DUM del Dashboard 1.

---

## 📌 Notas para Copilot

1. Este prompt es el **Paso 10** del proyecto. Los pasos 1–9 están completados (ver `COPILOT_CONTEXT.md`).
2. Sigue el mismo patrón arquitectónico de los dashboards del Paso 9: Query → Handler → DTO → Razor page con ApexCharts.
3. Registra las tres nuevas policies en `PolicyConstants.cs` y los profiles autorizados en `ClaimsTransformation`.
4. Añade las nuevas rutas al menú lateral (`NavMenu.razor`) bajo una nueva sección **"Movilidad Urbana"**, visible solo para los perfiles autorizados.
5. El `MobilityRecommendationEngine.cs` es un servicio inyectable (`ITransient`) que recibe los DTOs de métricas y devuelve una lista de `MobilityRecommendationDto`.
6. Toda la configuración de franjas horarias y pesos del índice se lee de `IConfiguration` (`appsettings.json` → sección `MobilitySettings`).
