# 📊 Módulo de Análisis y Cumplimiento Normativo — Dashboards SCRAP (UC-CN)

> **Prompt para GitHub Copilot** — Adjuntar junto con `README.md`, `Crear_BD_v4_1.sql`, `COPILOT_CONTEXT.md` y `Mapa_Funcionalidades.md` al inicio de la sesión Copilot.

---

## 🎯 Objetivo — Caso de Uso Cumplimiento Normativo (UC-CN)

Crear un **nuevo módulo de dashboards "Análisis y Cumplimiento Normativo"** dentro de la carpeta `Reporting/RegulatoryCompliance/` que permita a los diferentes actores del ecosistema GreenTransit **monitorizar, certificar y auditar el cumplimiento de la Responsabilidad Ampliada del Productor (RAP)** según la Ley 22/2011 de residuos y suelos contaminados.

El sistema debe centralizar la información normativa de diferentes SCRAPs, monitorear su cumplimiento, identificar áreas de riesgo y facilitar decisiones correctivas, cumpliendo los siguientes objetivos:

1. **Monitorizar y certificar el reparto de servicios entre los SCRAPs** garantizando el principio de proporcionalidad por cuota de mercado.
2. **Obtener la información necesaria para auditar el reparto de cuota de responsabilidad** entre los SCRAPs.
3. **Optimizar la monitorización** para verificar que los SCRAPs cumplen con las regulaciones pertinentes.
4. **Agilizar respuestas y desarrollar soluciones** para casos de incumplimiento.
5. **Facilitar a la administración el correcto cumplimiento de los convenios** establecidos con los SCRAPs.

**Participantes clave del ecosistema**:
- **AENOR Confía**: verifica, valida y certifica la información de las transacciones compartidas.
- **Oficina de Coordinación de los SCRAP RAEE (OFIRAEE)**: provee datos de convenios a nivel de oficina y de cada SCRAP constituyente, con la Administración Pública y entre ellos. Suministra información de objetivos de recogida por flujos (doméstico y profesional).
- **SCRAP Cartón Circular**: flujos de residuos de envases comerciales e industriales, con tipología de convenios diferenciada.
- **ECOEMBES**: flujo de envases domésticos (adherencia futura prevista).
- **Entes locales**: suministran información de toneladas obtenidas por metodologías de recolección (servicios públicos de recogida).

**Datos compartidos entre participantes**:
- Datos de cumplimiento de cada SCRAP con regulaciones locales y nacionales (tasas de reciclaje, certificados de tratamiento y disposición final).
- Datos de cambios y actualizaciones normativas en gestión de residuos.
- Datos de convenios con comunidades autónomas (características del servicio, facturas de compensación al ente público).

---

## 📁 Ubicación de archivos

Todos los dashboards de este módulo deben crearse dentro de la carpeta `Reporting/RegulatoryCompliance/`:

### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/RegulatoryCompliance/
├── ScrapComplianceOverview.razor                → /reporting/regulatory-compliance/scrap-overview
├── MarketShareAudit.razor                       → /reporting/regulatory-compliance/market-share-audit
├── AgreementComplianceMonitoring.razor          → /reporting/regulatory-compliance/agreement-monitoring
├── PublicEntityComplianceView.razor             → /reporting/regulatory-compliance/public-view
└── DispatchOfficeComplianceData.razor           → /reporting/regulatory-compliance/dispatch-data
```

### Capa Application (CQRS)

```
Application/Features/Reporting/RegulatoryCompliance/
├── Queries/
│   ├── GetScrapComplianceOverviewQuery.cs       → Dashboard CN-A (SCRAP)
│   ├── GetMarketShareAuditQuery.cs              → Dashboard CN-B (COORDINATOR / DISPATCH_OFFICE)
│   ├── GetAgreementComplianceMonitoringQuery.cs → Dashboard CN-C (COORDINATOR / DISPATCH_OFFICE)
│   ├── GetPublicEntityComplianceViewQuery.cs    → Dashboard CN-D (PUBLIC_ENT)
│   ├── GetDispatchOfficeComplianceDataQuery.cs  → Dashboard CN-E (DISPATCH_OFFICE)
│   ├── GetComplianceAlertsSummaryQuery.cs       → Widget compartido: alertas de incumplimiento
│   ├── GetRecyclingRateByFlowQuery.cs           → Widget compartido: tasa de reciclaje por flujo
│   └── ExportComplianceDataToExcelQuery.cs      → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── ScrapComplianceOverviewDto.cs
│   ├── MarketShareAuditDto.cs
│   ├── AgreementComplianceMonitoringDto.cs
│   ├── PublicEntityComplianceViewDto.cs
│   ├── DispatchOfficeComplianceDataDto.cs
│   ├── ComplianceAlertDto.cs
│   ├── RecyclingRateByFlowDto.cs
│   └── ComplianceExportDto.cs
└── Services/
    └── ComplianceMonitoringService.cs           → Motor de alertas, cálculo de desviaciones y recomendaciones
```

### Componentes reutilizables

```
Web/Components/Shared/RegulatoryCompliance/
├── ComplianceGaugeCard.razor                    → Gauge de % cumplimiento con semáforo
├── MarketShareProportionalityChart.razor        → Gráfico de proporcionalidad real vs objetivo por cuota
├── ComplianceAlertInbox.razor                   → Panel de alertas de incumplimiento tipo inbox
├── RecyclingRateProgressBar.razor               → Barra de progreso tasa de reciclaje (reutiliza ProgressBar.razor)
└── AgreementStatusTimeline.razor                → Timeline del estado de cumplimiento de convenios
```

> **Reutilizar** de los dashboards existentes: `ProgressBar.razor`, `EmissionsCard.razor`, `IncidentsBadge.razor`.

---

## 📊 Dashboards a crear (son CINCO vistas diferenciadas)

---

### Dashboard CN-A — **Panel de Cumplimiento Normativo — Visión SCRAP** (`/reporting/regulatory-compliance/scrap-overview`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Policy de autorización**: `CanViewScrapComplianceOverview` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: cada SCRAP visualiza su propio nivel de cumplimiento normativo: tasas de reciclaje alcanzadas, progreso de objetivos de recogida, estado de sus convenios y alertas de riesgo de incumplimiento.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo de cumplimiento** (cards de KPI)
   - **Tasa de reciclaje global** = `SUM(TreatmentPlantResidues.WeightTotal WHERE TreatmentOperations.IsRecycling = 1)` / `SUM(EntryPlantResidues.Weight)` × 100, filtrado por `WasteMoves.IdScrap = LinkedEntityId`.
   - **Tasa de valorización** = `SUM(TreatmentPlantResidues.WeightTotal WHERE TreatmentOperations.IsEnergyRecovery = 1)` / `SUM(EntryPlantResidues.Weight)` × 100.
   - **Tasa de preparación para reutilización** = `SUM(TreatmentPlantResidues.WeightTotal WHERE TreatmentOperations.IsPreparationForReuse = 1)` / `SUM(EntryPlantResidues.Weight)` × 100.
   - **% cumplimiento de cuotas de mercado** = `SUM(EntryPlantResidues.Weight)` real / `MarketShares.Weight` objetivo × 100, para el año seleccionado.
   - **Nº de convenios activos** = `COUNT(Agreements WHERE IdScrap = LinkedEntityId AND Status = 'Active')`.
   - **Variación % vs periodo anterior** en cada KPI.
   - Semáforo: verde ≥ objetivo regulatorio (`RegulatoryTargets`), naranja 80–99%, rojo < 80%.

2. **Evolución trimestral de tasas de cumplimiento** (line chart multi-serie con ApexCharts)
   - Series: tasa de reciclaje, tasa de valorización, tasa de reutilización, objetivo regulatorio (línea horizontal de referencia desde `RegulatoryTargets`).
   - Fuente: agregación trimestral de `TreatmentPlantResidues` + `TreatmentOperations`, cruzado con `WasteMoves.IdScrap`.
   - **Obligatorio**: al menos un gráfico por dashboard — este es el gráfico principal del CN-A.

3. **Cumplimiento de cuotas por categoría y comunidad autónoma** (tabla + barras de progreso)
   - Fuente: `MarketShares` JOIN `EntryPlantResidues` (cálculo análogo al existente en `GetMarketShareComplianceQuery`).
   - Columnas: categoría (`MarketShares.Category`), comunidad autónoma (`MarketShares.AutonomousCommunity` — **mostrar nombre, no código**), tipo flujo (`MarketShares.FlowType`), peso objetivo (kg), peso real (kg), % cumplimiento, estado (semáforo `IsAtRisk`).
   - Sparkline de evolución mensual del % cumplimiento por fila.

4. **Estado de convenios del SCRAP** (tabla con timeline visual)
   - Fuente: `Agreements WHERE IdScrap = LinkedEntityId`.
   - Columnas: nº acuerdo (`AgreementNumber`), entidad pública (`Entities.Name` vía `IdPublicEntity`), comunidad autónoma (`Agreements.AutonomousCommunity` — **nombre**), provincia (`Agreements.ProvinceCode` → `Province.Name`), municipio (`Agreements.MunicipalityCode` → `Municipality.Name`), flujo de residuo (`WasteStream`), estado (`Status`), vigencia (`EffectiveFrom` — `EffectiveTo`), días para vencimiento.
   - Semáforo: rojo si `EffectiveTo` < 30 días, naranja si < 90 días.
   - Drill-down: al hacer clic, enlace a `/agreements/{id}`.

5. **Liquidaciones pendientes y facturación** (bar chart apilado + tabla)
   - Fuente: `Settlements WHERE IdScrap = LinkedEntityId`.
   - **Gráfico (bar chart apilado)**: importe total (`TotalAmount`) por mes, apilado por `ValidationStatus` (Pending = naranja, Approved = verde, Rejected = rojo).
   - Tabla: `SettlementNumber`, año, mes, entidad pública (`Entities.Name` vía `IdPublicEntity`), `TotalAmount`, `Currency`, `ValidationStatus`, `ValidatedAt`.
   - Enlace al detalle de la liquidación.

6. **Alertas de incumplimiento** (panel tipo inbox)
   - Generadas por `ComplianceMonitoringService.cs` en el backend cuando:
     - Tasa de reciclaje cae bajo el objetivo regulatorio.
     - % cumplimiento de cuota de mercado < 80% a fecha proporcional al mes en curso.
     - Convenio a < 30 días de vencimiento sin renovación.
     - Liquidación rechazada.
   - Fuente: cálculos sobre `MarketShares`, `RegulatoryTargets`, `Agreements`, `Settlements`.

#### Filtros globales:
- `Year`, `Quarter` / `Month`.
- `AutonomousCommunity` (selector con nombres, no códigos).
- `Category` (dependiente de `FlowType`, reutilizar catálogo `CategoriesByFlow`).
- `FlowType` (`WasteStream`).

---

### Dashboard CN-B — **Panel de Auditoría de Cuotas de Mercado — Reparto entre SCRAPs** (`/reporting/regulatory-compliance/market-share-audit`)

**Destinado a**: perfiles `COORDINATOR`, `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewMarketShareAudit` (nueva).

**Propósito**: la Oficina de Coordinación y los coordinadores auditan el reparto proporcional de la responsabilidad entre SCRAPs, verificando que cada uno asume su cuota de mercado correspondiente según la legislación (principio de proporcionalidad).

#### Widgets / KPIs requeridos:

1. **Proporcionalidad global de cuotas** (donut chart + tabla resumen)
   - Fuente: `MarketShares` agrupado por `IdScrap` para el año seleccionado.
   - **Donut chart**: peso objetivo total por SCRAP como proporción del total de todos los SCRAPs del tenant.
   - Tabla resumen por SCRAP (`Entities.Name`): objetivo total (kg), real total (kg), % cumplimiento, desviación (kg), % desviación.
   - **Obligatorio**: este donut es el gráfico principal del CN-B.

2. **Comparativa de cumplimiento por SCRAP y categoría** (heatmap / tabla coloreada)
   - Fuente: `MarketShares` + `EntryPlantResidues` cruzado por `WasteMoves.IdScrap`.
   - Filas: SCRAP (`Entities.Name`).
   - Columnas: categorías del catálogo (`MarketShares.Category`).
   - Celdas: % cumplimiento con color de fondo (verde ≥ 100%, naranja 80–99%, rojo < 80%).
   - Permite identificar rápidamente áreas vulnerables por SCRAP × categoría.

3. **Evolución mensual del reparto real vs objetivo** (stacked area chart)
   - Fuente: `EntryPlantResidues.Weight` agrupado por mes y `WasteMoves.IdScrap`.
   - Líneas de referencia: objetivo mensual prorrateado de `MarketShares.Weight` / 12.
   - Cada serie (área) = un SCRAP. Permite ver si algún SCRAP está absorbiendo más o menos de lo que le corresponde.

4. **Desglose por comunidad autónoma y flujo** (tabla expandible)
   - Fuente: `MarketShares` filtrada por `AutonomousCommunity` (**nombre, no código**) y `FlowType`.
   - Filas agrupables: comunidad autónoma → SCRAP → categoría.
   - Columnas: objetivo (kg), real (kg), % cumplimiento, estado.
   - Permite a la Oficina de Coordinación auditar si la distribución territorial respeta la proporcionalidad.

5. **Índice de desviación por SCRAP** (bar chart horizontal)
   - Fórmula: `(Real - Objetivo) / Objetivo × 100` por SCRAP.
   - Barras verdes (positivas, superan objetivo) y rojas (negativas, por debajo).
   - Línea central en 0%. Umbral de alerta configurable (p.ej. ±15%).

6. **Exportación de datos de auditoría** (botón XLSX)
   - Dataset: SCRAP, categoría, comunidad autónoma, flujo, objetivo, real, desviación, % cumplimiento.
   - Patrón ClosedXML existente.

#### Filtros:
- `Year`.
- `AutonomousCommunity` (nombre).
- `FlowType` / `Category`.
- `IdScrap` (el COORDINATOR ve transversalmente los SCRAPs vinculados vía `Agreements.IdCoordinator = LinkedEntityId`).

---

### Dashboard CN-C — **Panel de Monitorización de Convenios — Coordinador** (`/reporting/regulatory-compliance/agreement-monitoring`)

**Destinado a**: perfiles `COORDINATOR`, `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewAgreementComplianceMonitoring` (nueva).

**Propósito**: monitorizar el estado y cumplimiento de los convenios entre SCRAPs y entidades públicas, verificar las tarifas de compensación, y detectar desviaciones en los servicios pactados.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo de convenios** (cards de KPI)
   - **Total convenios activos**: `COUNT(Agreements WHERE Status = 'Active')` dentro del ámbito del coordinador.
   - **Convenios próximos a vencer** (< 90 días): `COUNT(Agreements WHERE EffectiveTo < DATEADD(day, 90, GETUTCDATE()) AND Status = 'Active')`.
   - **Liquidaciones pendientes**: `COUNT(Settlements WHERE ValidationStatus = 'Pending')`.
   - **Importe total liquidado (año)**: `SUM(Settlements.TotalAmount WHERE Year = @Year AND ValidationStatus = 'Approved')`.
   - **Variación % vs año anterior**.

2. **Mapa de cobertura de convenios** (gráfico de barras agrupadas por comunidad autónoma)
   - Fuente: `Agreements` agrupado por `AutonomousCommunity` (**nombre**).
   - **Gráfico (bar chart agrupado)**: barras por SCRAP dentro de cada comunidad autónoma, altura = nº de acuerdos activos.
   - Superpuesto: línea con total de toneladas gestionadas (de `EntryPlants.NetWeight`).
   - **Obligatorio**: este es el gráfico principal del CN-C.

3. **Estado de convenios por SCRAP y entidad pública** (tabla interactiva)
   - Fuente: `Agreements` con JOINs a `Entities` para resolver nombres de SCRAP, entidad pública y coordinador.
   - Columnas: SCRAP (`Entities.Name`), entidad pública (`Entities.Name`), comunidad autónoma (**nombre**), provincia (**nombre**), municipio (**nombre**), flujo (`WasteStream`), subflujo (`SubStream`), estado (`Status`), vigencia, modelo tarifario (`TariffModelType`).
   - Semáforo por estado: Active = verde, Draft = azul, Expired = rojo, Cancelled = gris.
   - Filtrable y paginada.

4. **Seguimiento de liquidaciones por convenio** (line chart + tabla)
   - **Gráfico (line chart)**: evolución mensual del importe liquidado (`Settlements.TotalAmount`) por SCRAP, últimos 12 meses.
   - Tabla: `SettlementNumber`, SCRAP, entidad pública, año, mes, importe base, ajustes (eco-modulación), impuestos, total, estado de validación.
   - Drill-down al detalle del settlement.

5. **Servicios prestados vs compromisos** (tabla comparativa)
   - Fuente: `WasteMoves` + `ServiceOrders` + `Agreements`.
   - Por cada convenio activo: nº servicios realizados (traslados completados), toneladas gestionadas, vs mínimos del convenio (`MinimumsJson`).
   - Semáforo: verde si cumple mínimos, rojo si no.

6. **Alertas de convenios** (panel tipo inbox)
   - Convenio a < 30 días de vencimiento.
   - Liquidación rechazada pendiente de resolución.
   - Servicios por debajo de mínimos pactados.
   - Generadas en `ComplianceMonitoringService.cs`.

#### Filtros:
- `Year`, `Month`.
- `IdScrap`.
- `AutonomousCommunity` (**nombre**).
- `ProvinceCode` → `Province.Name`.
- `MunicipalityCode` → `Municipality.Name`.
- `WasteStream` / `SubStream`.
- `Agreement.Status`.

---

### Dashboard CN-D — **Panel de Cumplimiento Normativo — Entidad Pública** (`/reporting/regulatory-compliance/public-view`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Policy de autorización**: `CanViewPublicEntityComplianceView` (nueva).

**Propósito**: los ayuntamientos y entidades públicas monitorizan que los SCRAPs con los que tienen convenios cumplen con sus obligaciones: toneladas recogidas, servicios prestados, liquidaciones de compensación y cumplimiento de la legislación en su ámbito territorial.

#### Widgets / KPIs requeridos:

1. **Resumen de servicios recibidos** (cards de KPI)
   - **Toneladas totales recogidas** en el municipio: `SUM(EntryPlantResidues.Weight)` filtrado por traslados cuyo punto de recogida (`WasteMoves.IdSource` → `Entities.MunicipalityCode`) pertenece al municipio del ente público, o `ServiceOrders.IdIssuedBy = LinkedEntityId`.
   - **Nº de servicios completados**: `COUNT(WasteMoves WHERE ServiceStatus IN ('EN PLANTA', 'CLASIFICADO'))`.
   - **Nº de SCRAPs operando**: `COUNT(DISTINCT WasteMoves.IdScrap)` en su ámbito.
   - **Importe total compensado (año)**: `SUM(Settlements.TotalAmount WHERE IdPublicEntity = LinkedEntityId AND Year = @Year AND ValidationStatus = 'Approved')`.
   - **Variación % vs periodo anterior**.

2. **Evolución mensual de recogidas por SCRAP** (bar chart apilado)
   - Fuente: `EntryPlantResidues.Weight` agrupado por mes y `WasteMoves.IdScrap` → `Entities.Name`.
   - **Gráfico (bar chart apilado)**: cada barra = un mes, segmentos = SCRAPs, altura = toneladas.
   - Línea superpuesta con objetivo prorrateado de `MarketShares.Weight` para el ámbito de la entidad.
   - **Obligatorio**: este es el gráfico principal del CN-D.

3. **Cumplimiento de objetivos por SCRAP en el territorio** (tabla con semáforo)
   - Fuente: `MarketShares` + `EntryPlantResidues`, filtrado por la comunidad autónoma de la entidad pública.
   - Por cada SCRAP: objetivo (kg), real (kg), % cumplimiento, categoría, flujo.
   - Barra de progreso coloreada (reutilizar `ProgressBar.razor`).

4. **Detalle de liquidaciones de compensación** (tabla)
   - Fuente: `Settlements WHERE IdPublicEntity = LinkedEntityId`.
   - Columnas: SCRAP (`Entities.Name`), nº liquidación, año, mes, importe base, ajustes, total, estado, fecha validación.
   - Totales acumulados por SCRAP y año.
   - Enlace al detalle.

5. **Toneladas por método de recolección** (pie / donut chart)
   - Fuente: `Agreements.CoveredMethodsJson` cruzado con `ServiceOrders` + `WasteMoves`.
   - **Donut chart**: distribución de toneladas por método de recolección cubierto en los convenios.
   - Permite al ente local reportar y validar las toneladas obtenidas por cada metodología.

6. **Incidencias y reclamaciones** (tabla + badge)
   - Fuente: `Incidents WHERE ClosedAt IS NULL` vinculadas al ámbito del municipio.
   - Columnas: tipo, severidad, traslado vinculado, SCRAP responsable, fecha apertura, días abierta.
   - Badge de conteo en el título del widget (reutilizar `IncidentsBadge.razor`).

#### Filtros:
- `Year`, `Month`.
- `IdScrap` (SCRAPs que operan en su territorio — derivado de `WasteMoves` o `Agreements`).
- `WasteStream`.
- `Category`.

---

### Dashboard CN-E — **Panel de Datos de Cumplimiento — Oficina de Asignación** (`/reporting/regulatory-compliance/dispatch-data`)

**Destinado a**: perfiles `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewDispatchOfficeComplianceData` (nueva).

**Propósito**: la Oficina de Asignación y Coordinación consolida todos los datos de cumplimiento normativo del ecosistema. Provee datasets exportables para auditorías externas (AENOR Confía) y análisis regulatorio.

#### Widgets / KPIs requeridos:

1. **Dashboard ejecutivo consolidado** (cards de KPI)
   - **Tasa de reciclaje global del ecosistema** (todos los SCRAPs).
   - **Tasa de valorización global**.
   - **Tasa de reutilización global**.
   - **Nº SCRAPs activos**.
   - **Nº convenios activos totales**.
   - **Importe total liquidado (año)**.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Ranking de SCRAPs por cumplimiento** (bar chart horizontal + tabla)
   - Fuente: `MarketShares` + `EntryPlantResidues` + `TreatmentPlantResidues` + `TreatmentOperations`, agrupados por `IdScrap`.
   - **Gráfico (bar chart horizontal)**: % cumplimiento global por SCRAP, ordenado descendente.
   - Línea vertical de referencia al 100%.
   - Semáforo: verde ≥ 100%, naranja 80–99%, rojo < 80%.
   - Tabla: SCRAP, objetivo total, real total, tasa reciclaje, tasa valorización, nº convenios, importe liquidado.
   - **Obligatorio**: este es el gráfico principal del CN-E.

3. **Tabla exportable para auditoría** (tabla con exportación XLSX)
   - Dataset plano con campos: SCRAP (`Entities.Name`), categoría, comunidad autónoma (**nombre**), provincia (**nombre**), municipio (**nombre**), flujo, año, periodo, peso objetivo (kg), peso real (kg), % cumplimiento, tasa reciclaje, tasa valorización, nº convenios activos, importe liquidado.
   - Exportable a XLSX (patrón ClosedXML).
   - Objetivo: proveer datos limpios y estructurados a AENOR Confía y auditores externos.

4. **Evolución interanual de tasas** (line chart multi-año)
   - Fuente: agregación anual de tasas de reciclaje, valorización y reutilización por el ecosistema completo.
   - Líneas de referencia: objetivos regulatorios de `RegulatoryTargets`.
   - Permite identificar tendencias plurianuales.

5. **Mapa de calor de cumplimiento geográfico** (tabla coloreada por comunidad autónoma × SCRAP)
   - Fuente: `MarketShares` + `EntryPlantResidues`.
   - Filas: comunidad autónoma (**nombre**).
   - Columnas: SCRAPs.
   - Celdas: % cumplimiento con gradiente de color.

6. **Resumen de cambios normativos y actualizaciones** (panel informativo)
   - Fuente: `RegulatoryTargets` + `EmissionFactorSets.ValidFrom/ValidTo` + `EcoModulationRuleSets.ValidFrom/ValidTo`.
   - Lista de cambios recientes: nuevos objetivos regulatorios, nuevas versiones de factores de emisión, nuevas reglas de eco-modulación.
   - Timeline visual de vigencias.

#### Filtros:
- `Year`.
- `IdScrap`.
- `AutonomousCommunity` (**nombre**).
- `FlowType` / `Category`.

---

## 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el UC-CN se alimenta de las tablas existentes del modelo v4.1. Las métricas derivadas (tasas de reciclaje, desviaciones, índices de cumplimiento) se calculan en las Queries CQRS.

| Tabla | Campos principales para este UC-CN |
|-------|--------------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `ProvinceCode`, `MunicipalityCode`, `AutonomousCommunity` |
| `MarketShares` | `Id`, `IdScrap`, `Category`, `AutonomousCommunity`, `Year`, `Weight`, `Period`, `EffectiveFrom`, `EffectiveTo`, `FlowType`, `OwnerId` |
| `Agreements` | `Id`, `AgreementNumber`, `Status`, `EffectiveFrom`, `EffectiveTo`, `IdScrap`, `IdPublicEntity`, `IdCoordinator`, `WasteStream`, `SubStream`, `AutonomousCommunity`, `ProvinceCode`, `MunicipalityCode`, `TariffModelType`, `TariffRulesJson`, `MinimumsJson`, `CoveredMethodsJson`, `OwnerId` |
| `AgreementDocuments` | `AgreementId`, `DocumentType`, `DocumentId`, `DocumentHash`, `SignedAt` |
| `Settlements` | `Id`, `SettlementNumber`, `Status`, `AgreementId`, `Year`, `Month`, `IdScrap`, `IdPublicEntity`, `BaseAmount`, `AdjustmentsAmount`, `TaxAmount`, `TotalAmount`, `ValidationStatus`, `ValidatedAt`, `OwnerId` |
| `SettlementLines` | `SettlementId`, `ProductCategory`, `IdLERCode`, `WeightKg`, `PricePerKg`, `Amount` |
| `WasteMoves` | `Id`, `IdScrap`, `IdScrap2`, `IdSource`, `IdDestination`, `ServiceOrderId`, `ServiceStatus`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `Weight`, `IdCarrier` |
| `ServiceOrders` | `Id`, `Status`, `IdPickupPoint`, `IdIssuedBy`, `WasteStream`, `OwnerId` |
| `EntryPlants` | `Id`, `PlantEntryDate`, `NetWeight`, `ServiceOrderId` |
| `EntryPlantResidues` | `EntryPlantId`, `Weight`, `IdResidue` |
| `TreatmentPlants` | `Id`, `IdTreatmentOperation`, `EntryPlantId` |
| `TreatmentPlantResidues` | `TreatmentPlantId`, `WeightTotal`, `WeightReused`, `WeightValued`, `WeightRejected` |
| `TreatmentOperations` | `Id`, `Code`, `IsRecycling`, `IsEnergyRecovery`, `IsPreparationForReuse` |
| `RegulatoryTargets` | `OwnerId`, `Category`, `Year`, `MinRecyclingPercent`, `MinReusePercent` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `WasteMoveReference`, `OwnerId` |
| `Residues` | `Id`, `ResidueType`, `ProductCategory`, `IdLERCode` |
| `LERCodes` | `Id`, `Code`, `Description`, `IsDangerous`, `IsRAEE` |
| `EcoModulationRuleSets` | `RuleSetName`, `Version`, `ValidFrom`, `ValidTo` |
| `EmissionFactorSets` | `FactorSetName`, `Version`, `Status`, `ValidFrom`, `ValidTo` |

**JOINs geográficos obligatorios**: en toda query, tabla o filtro que use `ProvinceCode` o `MunicipalityCode`, resolver siempre a `Province.Name` y `Municipality.Name`. **Nunca mostrar códigos al usuario**.

---

## 🔒 Reglas de autorización y filtrado de datos

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | CN-A | Solo ve sus propios datos: `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. `MarketShares.IdScrap = LinkedEntityId`. `Agreements.IdScrap = LinkedEntityId`. `Settlements.IdScrap = LinkedEntityId`. |
| `COORDINATOR` | CN-B, CN-C | Ve transversalmente los SCRAPs vinculados a sus acuerdos: `Agreements.IdCoordinator = LinkedEntityId`. |
| `PUBLIC_ENT` | CN-D | Solo ve datos del ámbito de su municipio: punto de recogida → `Entities.MunicipalityCode` = municipio de su entidad, o `ServiceOrders.IdIssuedBy = LinkedEntityId`. Convenios: `Agreements.IdPublicEntity = LinkedEntityId`. Liquidaciones: `Settlements.IdPublicEntity = LinkedEntityId`. |
| `DISPATCH_OFFICE` | CN-B, CN-C, CN-E | Ve todos los datos del tenant (`OwnerId`). Visión operativa completa. |
| `ADMIN` | CN-A, CN-B, CN-C, CN-D, CN-E | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, entidad pública, coordinador o zona geográfica. |

**Patrón de filtrado de datos**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` + `IDataScopeService.ApplyScope()` (ya implementado).

**Control de acceso a pantallas**: los permisos se gestionan **dinámicamente** desde `/security/page-permissions` mediante el sistema `PageDefinitions`/`PagePermissions`. La tabla anterior documenta la **configuración recomendada por defecto** que el administrador debe aplicar tras el despliegue. Las policies en código (`CanViewScrapComplianceOverview`, etc.) actúan como **mínimo de seguridad estático**; el control fino se delega al sistema dinámico de BD. **No hardcodear permisos en código**.

---

## 🏗️ Arquitectura de implementación

### Capa Application (CQRS)

```
Application/Features/Reporting/RegulatoryCompliance/
├── Queries/
│   ├── GetScrapComplianceOverviewQuery.cs       → CN-A (SCRAP)
│   ├── GetMarketShareAuditQuery.cs              → CN-B (COORDINATOR / DISPATCH_OFFICE)
│   ├── GetAgreementComplianceMonitoringQuery.cs → CN-C (COORDINATOR / DISPATCH_OFFICE)
│   ├── GetPublicEntityComplianceViewQuery.cs    → CN-D (PUBLIC_ENT)
│   ├── GetDispatchOfficeComplianceDataQuery.cs  → CN-E (DISPATCH_OFFICE)
│   ├── GetComplianceAlertsSummaryQuery.cs       → Widget compartido: alertas
│   ├── GetRecyclingRateByFlowQuery.cs           → Widget compartido: tasa reciclaje
│   └── ExportComplianceDataToExcelQuery.cs      → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── ScrapComplianceOverviewDto.cs
│   ├── MarketShareAuditDto.cs
│   ├── AgreementComplianceMonitoringDto.cs
│   ├── PublicEntityComplianceViewDto.cs
│   ├── DispatchOfficeComplianceDataDto.cs
│   ├── ComplianceAlertDto.cs
│   ├── RecyclingRateByFlowDto.cs
│   └── ComplianceExportDto.cs
└── Services/
    └── ComplianceMonitoringService.cs           → Motor de alertas, cálculos de desviación, umbrales
```

### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/RegulatoryCompliance/
├── ScrapComplianceOverview.razor                → /reporting/regulatory-compliance/scrap-overview
├── MarketShareAudit.razor                       → /reporting/regulatory-compliance/market-share-audit
├── AgreementComplianceMonitoring.razor          → /reporting/regulatory-compliance/agreement-monitoring
├── PublicEntityComplianceView.razor             → /reporting/regulatory-compliance/public-view
└── DispatchOfficeComplianceData.razor           → /reporting/regulatory-compliance/dispatch-data
```

### Componentes reutilizables (complementan los ya existentes)

- `ComplianceGaugeCard.razor` — gauge circular de % cumplimiento con semáforo (verde/naranja/rojo).
- `MarketShareProportionalityChart.razor` — gráfico de proporcionalidad real vs objetivo por SCRAP.
- `ComplianceAlertInbox.razor` — panel de alertas de incumplimiento tipo inbox con severidad.
- `RecyclingRateProgressBar.razor` — barra de progreso de tasa de reciclaje (extiende `ProgressBar.razor`).
- `AgreementStatusTimeline.razor` — timeline horizontal del estado de convenios.

> **Reutilizar** de los dashboards existentes: `ProgressBar.razor`, `EmissionsCard.razor`, `IncidentsBadge.razor`.

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
4. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
5. Exportación a XLSX disponible en CN-B y CN-E como mínimo (patrón ya implementado con ClosedXML en `ExportKpisToExcelQuery.cs`).
6. Responsive mobile-first.
7. Modo oscuro/claro (consistente con `MainLayout.razor`).
8. Los **umbrales de alertas** (% cumplimiento, días vencimiento) son configurables en `appsettings.json`, no hardcodeados.
9. Las alertas y recomendaciones se generan en el backend (`ComplianceMonitoringService.cs`), no en el cliente.
10. **No se crean nuevas entidades de dominio.** Todo se implementa con las entidades del modelo v4.1 existente.
11. El acceso a cada dashboard **no está hardcodeado en código**. Se gestiona mediante el sistema de autorización por pantalla (`PageDefinitions`/`PagePermissions`) configurable desde `/security/page-permissions`.
12. Cada usuario solo ve datos de las entidades asignadas a él o creadas por él, **a excepción de `ADMIN` y `DISPATCH_OFFICE`** que ven todos los datos del tenant.
13. Datos geográficos (provincia, municipio, comunidad autónoma) se muestran siempre como **nombre**, nunca como código, tanto en tablas como en filtros.
14. **Todos los dashboards incluyen al menos un gráfico** (chart de ApexCharts): no se permiten dashboards compuestos exclusivamente de tablas y cards.

---

## 🔗 Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de tasa de reciclaje y cumplimiento de cuotas pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **KPIs regulatorios (§5.2)**: el módulo de KPIs (`/kpis`) ya calcula tasas de reciclaje y cumplimiento de `MarketShares`. Los dashboards CN complementan con la dimensión de auditoría inter-SCRAP, convenios y liquidaciones. Reutilizar el patrón de `GetRegulatoryKpisQuery` y `GetMarketShareComplianceQuery`.
- **Trazabilidad (§5.1)**: desde cualquier traslado en los dashboards CN, enlace directo a `/traceability?term={WasteMoveReference}`.
- **Dashboard Monitorización Pública (§5.4.2)**: CN-D extiende la visión de la entidad pública añadiendo la perspectiva de cumplimiento normativo y auditoría de cuotas. Enlace cruzado desde CN-D a `/logistics/public-monitoring`.
- **Cuotas de Mercado (§2.3)**: la vista `/market-shares` muestra el CRUD de cuotas; los dashboards CN explotan esos datos en forma analítica y comparativa. Reutilizar `ProgressBar.razor`.
- **Acuerdos (§2.1)**: desde tablas de convenios en CN-C, enlace directo a `/agreements/{id}`.
- **Liquidaciones (§2.2)**: desde tablas de liquidaciones en CN-A/CN-D, enlace directo al detalle del settlement.
- **Incidencias (§4.3)**: los widgets de incidencias enlazan a `/incidents/{id}`.
- **Huella de Carbono (§5.7)**: enlace cruzado desde CN-A para contextualizar el cumplimiento con el impacto ambiental.

---

## 🧭 Navegación — Integración en NavMenu

Añadir en `NavMenu.razor`, dentro de la sección **Reporting**, una subcarpeta colapsable **"Análisis y Cumplimiento Normativo"** (colapsada por defecto, al igual que el resto de carpetas de su nivel), con consulta a `IPagePermissionService.CanAccessRouteAsync` para cada enlace:

```razor
@* Sección Reporting — subcarpeta Análisis y Cumplimiento Normativo *@
<NavMenuGroup Title="Análisis y Cumplimiento Normativo" Icon="gavel">
    @if (await PagePermissionService.CanAccessRouteAsync("/reporting/regulatory-compliance/scrap-overview"))
    {
        <NavMenuLink Href="/reporting/regulatory-compliance/scrap-overview"
                     Text="Cumplimiento SCRAP" />
    }
    @if (await PagePermissionService.CanAccessRouteAsync("/reporting/regulatory-compliance/market-share-audit"))
    {
        <NavMenuLink Href="/reporting/regulatory-compliance/market-share-audit"
                     Text="Auditoría Cuotas de Mercado" />
    }
    @if (await PagePermissionService.CanAccessRouteAsync("/reporting/regulatory-compliance/agreement-monitoring"))
    {
        <NavMenuLink Href="/reporting/regulatory-compliance/agreement-monitoring"
                     Text="Monitorización Convenios" />
    }
    @if (await PagePermissionService.CanAccessRouteAsync("/reporting/regulatory-compliance/public-view"))
    {
        <NavMenuLink Href="/reporting/regulatory-compliance/public-view"
                     Text="Cumplimiento — Entidad Pública" />
    }
    @if (await PagePermissionService.CanAccessRouteAsync("/reporting/regulatory-compliance/dispatch-data"))
    {
        <NavMenuLink Href="/reporting/regulatory-compliance/dispatch-data"
                     Text="Datos de Cumplimiento — Oficina" />
    }
</NavMenuGroup>
```

---

## 🔧 Configuración en `appsettings.json`

Añadir la siguiente sección:

```json
{
  "RegulatoryCompliance": {
    "Alerts": {
      "MarketShareRiskThresholdPercent": 80,
      "AgreementExpiryWarningDays": 90,
      "AgreementExpiryCriticalDays": 30,
      "MinServicesThresholdPercent": 70
    },
    "Defaults": {
      "DefaultMinRecyclingPercent": 55,
      "DefaultMinReusePercent": 5,
      "DefaultMinValorizationPercent": 65
    }
  }
}
```

---

## 🏷️ PageDiscoveryService — Actualizar `InferModuleName()` y `HumanizeName()`

### InferModuleName

La ruta `/reporting/regulatory-compliance/` ya debería inferirse como módulo **Reporting** gracias al patrón existente `Reporting` → Reporting. Si no se infiere automáticamente, añadir el caso en `Infrastructure/Services/PageDiscoveryService.cs`.

### HumanizeName

Añadir nombres legibles en español:

| Componente | Nombre legible |
|---|---|
| `ScrapComplianceOverview` | `Cumplimiento Normativo — Visión SCRAP` |
| `MarketShareAudit` | `Cumplimiento Normativo — Auditoría Cuotas de Mercado` |
| `AgreementComplianceMonitoring` | `Cumplimiento Normativo — Monitorización Convenios` |
| `PublicEntityComplianceView` | `Cumplimiento Normativo — Vista Entidad Pública` |
| `DispatchOfficeComplianceData` | `Cumplimiento Normativo — Datos Oficina` |

---

## 🔒 Policies a registrar

En `Domain/Authorization/PolicyConstants.cs`:

```csharp
public const string CanViewScrapComplianceOverview = nameof(CanViewScrapComplianceOverview);
public const string CanViewMarketShareAudit = nameof(CanViewMarketShareAudit);
public const string CanViewAgreementComplianceMonitoring = nameof(CanViewAgreementComplianceMonitoring);
public const string CanViewPublicEntityComplianceView = nameof(CanViewPublicEntityComplianceView);
public const string CanViewDispatchOfficeComplianceData = nameof(CanViewDispatchOfficeComplianceData);
```

En `Program.cs`, registrar con los perfiles indicados en la tabla de autorización:

```csharp
options.AddPolicy(PolicyConstants.CanViewScrapComplianceOverview, p =>
    p.RequireRole("SCRAP", "ADMIN"));
options.AddPolicy(PolicyConstants.CanViewMarketShareAudit, p =>
    p.RequireRole("COORDINATOR", "DISPATCH_OFFICE", "ADMIN"));
options.AddPolicy(PolicyConstants.CanViewAgreementComplianceMonitoring, p =>
    p.RequireRole("COORDINATOR", "DISPATCH_OFFICE", "ADMIN"));
options.AddPolicy(PolicyConstants.CanViewPublicEntityComplianceView, p =>
    p.RequireRole("PUBLIC_ENT", "ADMIN"));
options.AddPolicy(PolicyConstants.CanViewDispatchOfficeComplianceData, p =>
    p.RequireRole("DISPATCH_OFFICE", "ADMIN"));
```

---

## 📊 Matriz de permisos — Configuración recomendada por defecto

| Pantalla | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR | DISPATCH_OFFICE |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| CN-A Cumplimiento SCRAP | R | R | — | — | — | — | — | — | — |
| CN-B Auditoría Cuotas | R | — | — | — | — | — | — | R | R |
| CN-C Monitorización Convenios | R | — | — | — | — | — | — | R | R |
| CN-D Vista Entidad Pública | R | — | — | — | — | — | R | — | — |
| CN-E Datos Oficina | R | — | — | — | — | — | — | — | R |

Leyenda: **R** = Lectura, **—** = Sin acceso.

> Esta es la **configuración recomendada por defecto**, no permisos hardcodeados. El administrador la aplica desde `/security/page-permissions` tras el despliegue.

---

## ✅ Checklist técnico de implementación

- [ ] Policies registradas en `PolicyConstants.cs`: `CanViewScrapComplianceOverview`, `CanViewMarketShareAudit`, `CanViewAgreementComplianceMonitoring`, `CanViewPublicEntityComplianceView`, `CanViewDispatchOfficeComplianceData`.
- [ ] Policies registradas en `Program.cs` con los perfiles indicados.
- [ ] Páginas Blazor creadas en `Web/Components/Pages/Reporting/RegulatoryCompliance/` con `@attribute [Authorize(Policy = ...)]`.
- [ ] Queries CQRS creadas en `Application/Features/Reporting/RegulatoryCompliance/Queries/`.
- [ ] DTOs creados en `Application/Features/Reporting/RegulatoryCompliance/DTOs/`.
- [ ] `ComplianceMonitoringService.cs` implementado con motor de alertas, cálculos de desviación y umbrales configurables.
- [ ] Componentes reutilizables creados en `Web/Components/Shared/RegulatoryCompliance/`.
- [ ] Entradas en `NavMenu.razor` en sección Reporting como subcarpeta colapsable "Análisis y Cumplimiento Normativo" con consulta a `IPagePermissionService`.
- [ ] `InferModuleName()` verifica que `/reporting/regulatory-compliance/` → Reporting.
- [ ] `HumanizeName()` actualizado con nombres legibles en español para los 5 componentes.
- [ ] Configuración `RegulatoryCompliance` añadida en `appsettings.json` (umbrales de alertas, objetivos por defecto).
- [ ] Filtrado multi-tenant (`OwnerId`) aplicado en todos los Query handlers.
- [ ] Filtrado por perfil (`LinkedEntityId`) aplicado: SCRAP (IdScrap/IdScrap2), PUBLIC_ENT (MunicipalityCode/IdIssuedBy), COORDINATOR (Agreements.IdCoordinator).
- [ ] Todos los JOINs geográficos resuelven `ProvinceCode` → `Province.Name`, `MunicipalityCode` → `Municipality.Name` y `AutonomousCommunity` como nombre (no código).
- [ ] Exportación XLSX implementada en CN-B y CN-E (patrón ClosedXML).
- [ ] Gráficos con ApexCharts en TODOS los dashboards (mínimo uno por dashboard), responsive, modo oscuro/claro.
- [ ] Filtros persistidos en query string.
- [ ] Todos los dashboards incluyen al menos un gráfico (no solo tablas y cards).
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions`.

---

*Prompt generado para GreenTransit — Módulo de Análisis y Cumplimiento Normativo (UC-CN). Basado en el modelo de datos v4.1, el Mapa de Funcionalidades unificado y los patrones de implementación de los módulos UC3 (Movilidad Urbana), Mapas de Calor y Huella de Carbono.*
