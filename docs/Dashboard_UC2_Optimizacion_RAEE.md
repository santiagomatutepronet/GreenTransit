# Prompt para GitHub Copilot — Dashboard de Optimización Logística RAEE en GreenTransit

> **Contexto del proyecto**: GreenTransit es una plataforma web multi-rol, multi-tenant (`OwnerId`) construida con Blazor Server (.NET), CQRS (MediatR), SQL Server Azure y ApexCharts/Chart.js. El modelo de datos es el v4.1. El sistema ya tiene un dashboard operativo principal (§0.1 del Mapa de Funcionalidades) y un módulo de KPIs regulatorios implementado (`/kpis`).

---

## 🎯 Objetivo

Crear un **módulo de dashboard(s) de optimización logística RAEE** que permita a los SCRAP optimizar sus servicios de recolección de residuos desde los puntos de recogida hasta las plantas de tratamiento. El módulo debe centralizar datos logísticos (ubicación, volumen, rutas, capacidad vehicular, condiciones de tráfico, horarios de planta) y cruzarlos con las políticas de Distribución Urbana de Mercancía (DUM) para reducir emisiones de CO₂ y costos operativos.

---

## 📊 Dashboards a crear (son TRES vistas diferenciadas)

### Dashboard 1 — **Panel de Optimización Logística SCRAP** (`/logistics/optimization`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Propósito**: vista estratégica para que los SCRAP analicen y optimicen sus rutas de recogida de RAEE.

#### Widgets / KPIs requeridos:

1. **Mapa interactivo de puntos de recogida y plantas**
   - Fuente: `Entities` (filtrar por `EntityRole IN ('Producer', 'CAC', 'PublicEntity')`) con `Latitude`/`Longitude` para puntos de recogida; `Entities` con `EntityRole = 'Plant'` para destinos.
   - Capa adicional: polígonos de `DUMZones.GeometryJson` con estado activo (`Status = 'Active'`), coloreados según `DUMRestrictionRules.ActionType` (`Block` = rojo, `Restrict` = naranja, `Allow` = verde, `Notify` = azul).
   - Iconografía diferenciada por `EntityRole`.
   - Al hacer clic en un punto: popup con nombre, dirección, volumen acumulado del periodo y próximas recogidas planificadas.

2. **Volumen RAEE por zona geográfica** (bar chart / treemap)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `ServiceOrders` → agrupar por `Entities.ProvinceCode` o `MunicipalityCode` del `IdPickupPoint`.
   - Filtrar por `ServiceOrders.WasteStream = 'RAEE'` (o el valor equivalente configurado).
   - Métricas: `SUM(WasteMoveResidues.Weight)` agrupado por zona, periodo (mes/trimestre) y `LERCodes.Code`.

3. **Eficiencia de rutas** (card + trend line)
   - Fuente: `WasteMoveResidues` → campos `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`.
   - KPIs:
     - **km promedio por recogida** = `AVG(TransportInfo_TransportDistance)`.
     - **kgCO₂e promedio por recogida** = `AVG(TransportInfo_TransportCarbonEmissions)`.
     - **kgCO₂e por tonelada transportada** = `SUM(TransportCarbonEmissions) / SUM(Weight) * 1000`.
   - Comparativa mes actual vs mes anterior (% variación).

4. **Capacidad vehicular y utilización** (gauge + tabla)
   - Fuente: `WasteMoveResidues` → `VehicleType`, `TransportInfo_vehicleRegistration`.
   - Métricas por vehículo/tipo: nº de viajes, peso total transportado, distancia acumulada.
   - Si se dispone de capacidad máxima (campo futuro o configuración): ratio de utilización.

5. **Cumplimiento de ventanas horarias DUM** (donut chart)
   - Fuente: cruzar `WasteMoves.PlannedPickupStart`/`PlannedPickupEnd` con `DUMRestrictionRules.ConditionsJson` (campo JSON con horarios permitidos).
   - Categorías: "Dentro de ventana DUM", "Fuera de ventana", "Sin zona DUM aplicable".

6. **Horarios de planta vs llegadas reales** (heatmap semanal)
   - Fuente: `EntryPlants.PlantEntryDate` agrupado por día de semana y hora.
   - Objetivo: identificar picos y valles para redistribuir llegadas.

7. **Incidencias logísticas abiertas** (tabla con semáforo)
   - Fuente: `Incidents` WHERE `ClosedAt IS NULL` AND `Type IN ('Retraso', 'AveriVehiculo', 'DescuadrePeso')`.
   - Columnas: referencia del traslado, tipo, severidad, fecha apertura, días abierta.

#### Filtros globales del dashboard:
- `Year`, `Month`/`Quarter`.
- `IdScrap` (solo ADMIN puede ver todos; SCRAP ve solo los suyos, filtrado por `WasteMoves.IdScrap` o `IdScrap2`).
- `AutonomousCommunity` / `ProvinceCode`.
- `WasteStream` (para aislar RAEE de otros flujos).
- `EntityRole` del punto de recogida (Producer, CAC, PublicEntity).

---

### Dashboard 2 — **Panel de Monitorización para Entidades Públicas** (`/logistics/public-monitoring`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Propósito**: los ayuntamientos adscritos a la oficina de asignación monitorizan los servicios prestados por los SCRAP (actuales e históricos) y consultan las facturas de compensación.

#### Widgets / KPIs requeridos:

1. **Servicios prestados por SCRAP** (tabla + filtros)
   - Fuente: `WasteMoves` JOIN `ServiceOrders` WHERE `ServiceOrders.IdIssuedBy` = entidad pública logueada.
   - Columnas: SCRAP (`Entities.Name` vía `WasteMoves.IdScrap`), nº traslados, kg totales (`SUM(EntryPlants.NetWeight)`), periodo, estado actual del traslado.

2. **Histórico de recogidas** (line chart mensual)
   - Fuente: `EntryPlants.PlantEntryDate` + `EntryPlantResidues.Weight`, agrupado por mes.
   - Líneas separadas por SCRAP (si hay más de uno asignado).

3. **Liquidaciones / Facturas de compensación** (tabla)
   - Fuente: `Settlements` WHERE `IdPublicEntity` = entidad logueada.
   - Columnas: `SettlementNumber`, `Year`, `Month`, `Status` (Pending/Approved/Rejected), `TotalAmount`, `Currency`, `ValidatedAt`.
   - Enlace al detalle de la liquidación existente.

4. **Emisiones evitadas / generadas** (card comparativa)
   - Fuente: `SUM(WasteMoveResidues.TransportInfo_TransportCarbonEmissions)` del periodo vs periodo anterior.

5. **Cumplimiento de objetivos municipales** (progress bar)
   - Fuente: `MarketShares` WHERE `IdScrap` IN (SCRAPs vinculados) AND `AutonomousCommunity` = la del ayuntamiento.
   - Real vs objetivo: `SUM(EntryPlants.NetWeight)` vs `MarketShares.Weight`.

#### Filtros:
- `Year`, `Month`.
- `IdScrap` (los SCRAPs que operan en su municipio).
- `WasteStream`.

---

### Dashboard 3 — **Panel Operativo de Gestores / CACs / Plantas** (`/logistics/operations`)

**Destinado a**: perfiles `DISPATCH_OFFICE`, `CAC_OP`, `PLANT_OP` y `ADMIN`.

**Propósito**: visión operativa en tiempo real para los actores que ejecutan la logística y el tratamiento.

#### Widgets / KPIs por perfil:

**Para `DISPATCH_OFFICE` (Gestor de residuos / Oficina de asignación):**
1. **Órdenes de servicio pendientes de planificar** → `ServiceOrders` WHERE `Status = 'Pending'`.
2. **Traslados en curso** → embudo de `WasteMoves` por `ServiceStatus`.
3. **Planificación semanal** → calendario con `ServiceOrders.PlannedPickupStart` de los próximos 7 días.
4. **Incidencias abiertas asignadas** → `Incidents` filtradas por su ámbito.

**Para `CAC_OP`:**
1. **Entradas en CAC hoy** → `EntryCACs` WHERE `CACEntryDate` = hoy.
2. **Stock acumulado por tipología** → `SUM(EntryCACResidues.Weight)` agrupado por `Residues.Name` / `LERCodes.Code`.
3. **Tickets de pesaje pendientes de envío** → entradas sin `TicketScale` o sin peso registrado.

**Para `PLANT_OP`:**
1. **Entradas en planta hoy** → `EntryPlants` WHERE `PlantEntryDate` = hoy.
2. **Balance de tratamiento** → `TreatmentPlantResidues`: `WeightReused` + `WeightValued` vs `WeightRemove` (tasa de reciclaje en tiempo real).
3. **Impropios detectados** → `SUM(TreatmentPlants.ImproperWeight)` del periodo.
4. **Incidencias de planta abiertas** → `Incidents` vinculadas a sus `TreatmentPlants`.

---

## 🗄️ Modelo de datos — Tablas y campos clave a consultar

| Tabla | Campos principales para el dashboard |
|-------|---------------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `Latitude`, `Longitude`, `ProvinceCode`, `MunicipalityCode`, `CenterCode` |
| `ServiceOrders` | `Id`, `ServiceOrderNumber`, `Status`, `Priority`, `IdIssuedBy`, `IdPickupPoint`, `WasteStream`, `IdLERCode`, `PlannedPickupStart`, `PlannedPickupEnd`, `OwnerId` |
| `WasteMoves` | `Id`, `WasteMoveReference`, `ServiceStatus`, `IdSource`, `IdDestination`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `IdResidue`, `IdCarrier`, `Weight`, `VehicleType`, `FuelType`, `EuroClass`, `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`, `TransportInfo_vehicleRegistration` |
| `EntryPlants` | `Id`, `IdWasteMove`, `TicketScale`, `PlantEntryDate`, `GrossWeight`, `TareWeight`, `NetWeight`, `ServiceOrderId`, `OwnerId` |
| `EntryPlantResidues` | `IdEntryPlant`, `IdResidue`, `Weight`, `MeasureUnit` |
| `EntryCACs` | `Id`, `IdWasteMove`, `CACEntryDate`, `CollectionMethod`, `OwnerId` |
| `EntryCACResidues` | `IdEntryCAC`, `IdResidue`, `Weight`, `MeasureUnit` |
| `TreatmentPlants` | `Id`, `IdTreatmentOperation`, `ImproperWeight`, `ServiceOrderId`, `IncidentId`, `OwnerId` |
| `TreatmentPlantResidues` | `IdTreatmentPlant`, `IdResidue`, `WeightTotal`, `WeightReused`, `WeightValued`, `WeightRemove` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `ServiceOrderId`, `WasteMoveReference`, `Description` |
| `DUMZones` | `Id`, `ZoneCode`, `GeometryJson`, `Status` |
| `DUMRestrictionRules` | `ZoneId`, `RuleCode`, `ConditionsJson`, `ActionType`, `ValidFrom`, `ValidTo` |
| `MarketShares` | `IdScrap`, `Category`, `AutonomousCommunity`, `Year`, `Weight`, `Period` |
| `Settlements` | `Id`, `SettlementNumber`, `Status`, `IdScrap`, `IdPublicEntity`, `Year`, `Month`, `TotalAmount`, `Currency` |
| `LERCodes` | `Id`, `Code`, `Description` |
| `Residues` | `Id`, `Name`, `Reference`, `ResidueType`, `IdLERCode` |
| `EmissionFactorSets` | `Id`, `Name`, `Version`, `IsActive` |

---

## 🔒 Reglas de autorización y filtrado de datos

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | Dashboard 1 | Solo ve traslados donde `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. |
| `PUBLIC_ENT` | Dashboard 2 | Solo ve traslados cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`) o liquidaciones donde `Settlements.IdPublicEntity = LinkedEntityId`. |
| `DISPATCH_OFFICE` | Dashboard 3 | Ve todos los traslados del tenant (`OwnerId`). |
| `CAC_OP` | Dashboard 3 | Solo ve entradas en su CAC (filtrado por `EntryCACs` vinculadas a su entidad). |
| `PLANT_OP` | Dashboard 3 | Solo ve entradas y tratamientos de su planta (filtrado por `EntryPlants`/`TreatmentPlants` de su entidad). |
| `ADMIN` | Todos (1, 2, 3) | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, entidad pública o planta. |
| `COORDINATOR` | Dashboard 1 (lectura) | Lectura transversal del ámbito de los acuerdos en los que participa. |

**Patrón de filtrado**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` (ya implementado en la capa `Application`).

---

## 🏗️ Arquitectura de implementación sugerida

### Capa Application (CQRS)
```
Application/Features/Logistics/
├── Queries/
│   ├── GetLogisticsOptimizationQuery.cs     → Dashboard 1 (SCRAP)
│   ├── GetPublicMonitoringQuery.cs          → Dashboard 2 (PUBLIC_ENT)
│   ├── GetOperationalDashboardQuery.cs      → Dashboard 3 (DISPATCH/CAC/PLANT)
│   └── GetDUMComplianceQuery.cs             → Widget de cumplimiento DUM (compartido)
└── DTOs/
    ├── LogisticsOptimizationDto.cs
    ├── PublicMonitoringDto.cs
    ├── OperationalDashboardDto.cs
    └── DUMComplianceDto.cs
```

### Capa Web (Blazor)
```
Web/Components/Pages/Logistics/
├── LogisticsOptimization.razor              → /logistics/optimization
├── PublicMonitoring.razor                   → /logistics/public-monitoring
└── OperationalDashboard.razor               → /logistics/operations
```

### Componentes reutilizables
- `WasteVolumeMap.razor` — mapa Leaflet/Mapbox con capas de entidades + DUM.
- `EmissionsCard.razor` — card de CO₂ con comparativa temporal.
- `DUMComplianceDonut.razor` — donut de cumplimiento de ventanas DUM.
- `IncidentsBadge.razor` — badge con conteo de incidencias abiertas por severidad.

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan ApexCharts (consistente con el módulo de KPIs existente).
4. El mapa interactivo renderiza polígonos DUM desde `GeometryJson` (formato GeoJSON).
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en cada dashboard (patrón ya implementado con ClosedXML en `ExportKpisToExcelQuery.cs`).
7. Responsive mobile-first (operadores de campo).
8. Modo oscuro/claro (consistente con `MainLayout.razor`).

---

## 🔗 Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de este módulo pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **Trazabilidad (§5.1)**: desde cualquier traslado en los dashboards, enlace directo a `/traceability?term={WasteMoveReference}`.
- **KPIs regulatorios (§5.2)**: el KPI de "intensidad CO₂ por tonelada" ya existe en `/kpis`; aquí se desglosa por ruta/zona.
- **Incidencias (§4.3)**: los widgets de incidencias enlazan a `/incidents/{id}`.
- **Zonas DUM (§4.4)**: la capa del mapa reutiliza la misma fuente de datos de `DUMZones`/`DUMRestrictionRules`.
