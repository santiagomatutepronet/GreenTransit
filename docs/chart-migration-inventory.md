# Inventario de Migración de Gráficos — ApexCharts/Chart.js → Radzen Blazor Charts

> Generado como parte de la rama `refactor/migrate-charts-to-radzen`.

---

## Tabla de inventario

| # | Archivo (.razor o .js) | Tipo de gráfico | Fuente de datos (DTO/Query/método) | Funcionalidad / Dashboard | Dependencia |
|---|---|---|---|---|---|
| 1 | `Pages/Dashboard.razor` | Bar (horizontal) — Traslados por estado | `GetDashboardSummaryQuery → DashboardSummaryDto` | Dashboard principal — Embudo traslados | ApexCharts |
| 2 | `Pages/Dashboard.razor` | Donut — Tasas de tratamiento | `GetDashboardSummaryQuery → DashboardSummaryDto` | Dashboard principal — Tasas mes | ApexCharts |
| 3 | `Pages/Dashboard.razor` | Bar agrupado — Kg recogidos vs tratados | `GetDashboardSummaryQuery → MonthlyKgDto` | Dashboard principal — Últimos 6 meses | ApexCharts |
| 4 | `Reporting/RegulatoryKpis.razor` | Line + Bar combo — Evolución trimestral | `GetRegulatoryKpisQuery → QuarterlyKpiDto` | KPIs Regulatorios — §5.2 | ApexCharts |
| 5 | `Reporting/RegulatoryCompliance/AgreementComplianceMonitoring.razor` | Bar agrupado — Cobertura por CCAA | `GetAgreementComplianceMonitoringQuery → CoverageByRegion` | CN-C — Cobertura convenios | ApexCharts (JS interop vía `complianceCharts.js`) |
| 6 | `Reporting/RegulatoryCompliance/AgreementComplianceMonitoring.razor` | Line multiplex — Liquidaciones mensuales por SCRAP | `GetAgreementComplianceMonitoringQuery → SettlementMonthlyByScrap` | CN-C — Evolución liquidaciones | ApexCharts (JS interop vía `complianceCharts.js`) |
| 7 | `Reporting/RegulatoryCompliance/DispatchOfficeComplianceData.razor` | Bar — Distribución por CCAA/LER | `GetDispatchOfficeComplianceQuery` | CN-B — Despacho Oficina | ApexCharts (JS interop vía `complianceCharts.js`) |
| 8 | `Reporting/RegulatoryCompliance/DispatchOfficeComplianceData.razor` | Line — Tendencia mensual | `GetDispatchOfficeComplianceQuery` | CN-B — Evolución temporal | ApexCharts (JS interop vía `complianceCharts.js`) |
| 9 | `Reporting/RegulatoryCompliance/MarketShareAudit.razor` | Bar — Cuotas por SCRAP | `GetMarketShareAuditQuery` | CN-D — Auditoría cuotas mercado | ApexCharts (JS interop vía `complianceCharts.js`) |
| 10 | `Reporting/RegulatoryCompliance/MarketShareAudit.razor` | Line — Tendencia cumplimiento | `GetMarketShareAuditQuery` | CN-D — Auditoría cuotas | ApexCharts (JS interop vía `complianceCharts.js`) |
| 11 | `Reporting/RegulatoryCompliance/MarketShareAudit.razor` | Donut — Distribución tipo | `GetMarketShareAuditQuery` | CN-D — Distribución | ApexCharts (JS interop vía `complianceCharts.js`) |
| 12 | `Reporting/RegulatoryCompliance/PublicEntityComplianceView.razor` | Bar — Tasas por CCAA | `GetPublicEntityComplianceQuery` | CN-E — Entidad pública | ApexCharts (JS interop vía `complianceCharts.js`) |
| 13 | `Reporting/RegulatoryCompliance/PublicEntityComplianceView.razor` | Line — Evolución temporal | `GetPublicEntityComplianceQuery` | CN-E — Entidad pública | ApexCharts (JS interop vía `complianceCharts.js`) |
| 14 | `Reporting/RegulatoryCompliance/ScrapComplianceOverview.razor` | Line — Evolución trimestral tasas | `GetScrapComplianceOverviewQuery` | CN-A — Panel SCRAP | ApexCharts (JS interop vía `complianceCharts.js`) |
| 15 | `Reporting/RegulatoryCompliance/ScrapComplianceOverview.razor` | Bar — Cuotas por CCAA/flujo | `GetScrapComplianceOverviewQuery` | CN-A — Panel SCRAP | ApexCharts (JS interop vía `complianceCharts.js`) |
| 16 | `Reporting/CarbonFootprint/CarbonFootprintOverview.razor` | Area — Evolución mensual emisiones | `carbonCharts.renderMonthlyEvolution` | HC-A — Visión global | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 17 | `Reporting/CarbonFootprint/CarbonFootprintOverview.razor` | Donut — Desglose por combustible | `carbonCharts.renderFuelDonut` | HC-A — Visión global | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 18 | `Reporting/CarbonFootprint/PlantEnergyFootprint.razor` | Bar — Emisiones por planta | `carbonCharts.renderBarGrouped` | HC-C — Huella energía plantas | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 19 | `Reporting/CarbonFootprint/PlantEnergyFootprint.razor` | Donut — Fuentes de energía | `carbonCharts.renderFuelDonut` | HC-C — Huella energía plantas | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 20 | `Reporting/CarbonFootprint/ProducerCarbonReport.razor` | Bar — Emisiones por código LER | `carbonCharts.renderBarGrouped` | HC-D — Informe productor | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 21 | `Reporting/CarbonFootprint/ProducerCarbonReport.razor` | Area — Evolución mensual | `carbonCharts.renderMonthlyEvolution` | HC-D — Informe productor | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 22 | `Reporting/CarbonFootprint/PublicEntityCarbonView.razor` | Bar — Emisiones por SCRAP | `carbonCharts.renderBarGrouped` | HC-E — Entidad pública | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 23 | `Reporting/CarbonFootprint/PublicEntityCarbonView.razor` | Area — Evolución mensual | `carbonCharts.renderMonthlyEvolution` | HC-E — Entidad pública | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 24 | `Reporting/CarbonFootprint/TransportEmissionsAnalysis.razor` | Area — Emisiones mensuales transporte | `carbonCharts.renderMonthlyEvolution` | HC-B — Análisis transporte | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 25 | `Reporting/CarbonFootprint/TransportEmissionsAnalysis.razor` | Bar — Emisiones por SCRAP | `carbonCharts.renderBarGrouped` | HC-B — Análisis transporte | ApexCharts (JS interop vía `carbonFootprintCharts.js`) |
| 26 | `Reporting/HeatMaps/WastePatternAnalysis.razor` | Heatmap — Generación 12 meses × LER | `heatMapCharts.renderTemporalHeatmap` | HM-B — Patrones estacionalidad | ApexCharts (JS interop vía `heatMapCharts.js`) — **Sin equivalente nativo Radzen → tabla coloreada CSS** |
| 27 | `Reporting/HeatMaps/WastePatternAnalysis.razor` | Line — Tendencia mensual top-5 tipologías | `heatMapCharts.renderMonthlyTrend` | HM-B — Tendencia | ApexCharts (JS interop vía `heatMapCharts.js`) |
| 28 | `Reporting/HeatMaps/WastePatternAnalysis.razor` | Heatmap 7×24 — Recogidas por hora/día | `heatMapCharts.renderWeeklyHeatmap` | HM-B — Heatmap semanal | ApexCharts (JS interop vía `heatMapCharts.js`) — **Sin equivalente nativo Radzen → tabla coloreada CSS** |
| 29 | `Reporting/HeatMaps/WasteDensityHeatMap.razor` | Bar/Column — Densidad residuos por municipio | `heatMapCharts.renderDensityBar` | HM-A — Densidad geográfica | ApexCharts (JS interop vía `heatMapCharts.js`) |
| 30 | `Reporting/HeatMaps/WasteDensityHeatMap.razor` | Line — Tendencia temporal | `heatMapCharts.renderTrendLine` | HM-A — Densidad geográfica | ApexCharts (JS interop vía `heatMapCharts.js`) |
| 31 | `Reporting/HeatMaps/PublicEntityHeatMapView.razor` | Bar — Residuos por zona/punto recogida | `heatMapCharts.renderEntityBar` | HM-C — Entidad pública | ApexCharts (JS interop vía `heatMapCharts.js`) |
| 32 | `Reporting/HeatMaps/PublicEntityHeatMapView.razor` | Line — Evolución mensual | `heatMapCharts.renderEntityTrend` | HM-C — Entidad pública | ApexCharts (JS interop vía `heatMapCharts.js`) |
| 33 | `Mobility/MunicipalMonitoring.razor` | Bar+Line combo — Recogidas e incidencias mensuales | `mobilityMunicipalHistory.render` | UC3 — Monitorización municipal | ApexCharts (JS interop vía `mobilityCharts.js`) |
| 34 | `Mobility/MunicipalMonitoring.razor` | Bar — Recogidas por municipio | `mobilityMunicipalByZone.render` | UC3 — Distribución geográfica | ApexCharts (JS interop vía `mobilityCharts.js`) |
| 35 | `Mobility/CoordinatorAnalysis.razor` | Heatmap 7×24 — Recogidas por hora/día | `mobilityHeatmap.render` | UC3 — Análisis coordinador | ApexCharts (JS interop vía `mobilityCharts.js`) — **Sin equivalente nativo Radzen → tabla coloreada CSS** |
| 36 | `Mobility/CoordinatorAnalysis.razor` | Bar — Distribución por tipo de residuo | `mobilityComparisonByType.render` | UC3 — Análisis coordinador | ApexCharts (JS interop vía `mobilityCharts.js`) |
| 37 | `Mobility/DispatchData.razor` | Bar/Line — Histórico mensual traslados | `mobilityMonthlyTrend.render` | UC3 — Datos despacho | ApexCharts (JS interop vía `mobilityCharts.js`) |
| 38 | `wwwroot/js/carbonFootprintCharts.js` | Configuración | ApexCharts JS global | Todos los dashboards HC | ApexCharts |
| 39 | `wwwroot/js/complianceCharts.js` | Configuración | ApexCharts JS global | Todos los dashboards CN | ApexCharts |
| 40 | `wwwroot/js/heatMapCharts.js` | Configuración | ApexCharts JS global | Todos los dashboards HM | ApexCharts |
| 41 | `wwwroot/js/mobilityCharts.js` | Configuración | ApexCharts JS global | Todos los dashboards UC3 | ApexCharts |

---

## Decisiones de migración

### Heatmaps (tipos sin equivalente nativo en Radzen)
Los gráficos de tipo **heatmap** (2D) no tienen equivalente directo en Radzen Blazor Charts.  
Decisión: reemplazar por **tabla HTML coloreada con CSS** donde el color de fondo de la celda se calcula proporcionalmente al valor.  
Se documenta con `<!-- MIGRACIÓN: heatmap → tabla coloreada CSS -->` en cada archivo afectado.

### Archivos JS a eliminar tras la migración
- `wwwroot/js/carbonFootprintCharts.js`
- `wwwroot/js/complianceCharts.js`
- `wwwroot/js/heatMapCharts.js`
- `wwwroot/js/mobilityCharts.js`

### Paquete NuGet a eliminar
- `Blazor-ApexCharts` del proyecto `GreenTransit.Web.csproj`

---

## Estado de migración

| Módulo | Estado |
|---|---|
| Infraestructura (ChartPalette, AppChart) | ⏳ Pendiente |
| Dashboard principal | ⏳ Pendiente |
| KPIs Regulatorios | ⏳ Pendiente |
| RegulatoryCompliance (5 páginas) | ⏳ Pendiente |
| CarbonFootprint (5 páginas) | ⏳ Pendiente |
| HeatMaps (3 páginas) | ⏳ Pendiente |
| Mobility (3 páginas) | ⏳ Pendiente |
| Limpieza dependencias | ⏳ Pendiente |
| Documentación actualizada | ⏳ Pendiente |
