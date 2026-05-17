# 🌍 Módulo de Huella de Carbono — Dashboard de Emisiones CO₂ en la Gestión de Residuos Industriales

## 🎯 Objetivo — Caso de Uso Huella de Carbono (UC-HC)

Crear un **nuevo módulo de dashboards "Huella de Carbono"** dentro de la carpeta `Reporting/CarbonFootprint/` que permita **medir, analizar y reportar la huella de carbono generada por la gestión integral de residuos industriales** en el ecosistema GreenTransit.

Este caso de uso materializa el compromiso con las regulaciones europeas para la protección e intercambio estandarizado del dato, y se alinea con las metas de sostenibilidad del **«Objetivo 55» de la Ley Europea del Clima**, posibilitando que los fabricantes de residuos industriales evalúen y minimicen el impacto ambiental de sus operaciones.

Los datos que se explotan para este módulo son:

- **Datos operativos de residuos**: tipos (código LER, flujo de residuo), frecuencia de recolección, métodos de tratamiento (operaciones R/D) y destinos finales (plantas).
- **Datos de uso de combustibles**: `FuelType`, `EuroClass` de cada vehículo, factor de emisión aplicado (`EmissionFactorSets` / `EmissionFactors`).
- **Datos logísticos**: tipo de vehículo (`VehicleType`), kilómetros recorridos (`TransportInfo_TransportDistance`), duración (`TransportInfo_TransportDuration`), matrícula (`TransportInfo_vehicleRegistration`).
- **Eficiencia energética de instalaciones de reciclaje**: consumo energético declarado por planta (`PlantEnergies.KwhTotal`, `Source`, `GridMixRef`, `AllocationMethod`).
- **Emisiones calculadas**: `TransportInfo_TransportCarbonEmissions` (kgCO₂e) ya persistidas a nivel de `WasteMoveResidues`.

**Participantes del ecosistema considerados**:

- **Gestores de residuos (DISPATCH_OFFICE)**: informan la tipología de vehículos empleados en cada servicio y gestionan la operativa logística.
- **SCRAP**: entidades de responsabilidad ampliada del productor; supervisan las emisiones de los servicios bajo su responsabilidad.
- **Productores de residuos industriales (PRODUCER)**: evalúan el impacto ambiental de la gestión de sus residuos y buscan oportunidades de reducción de emisiones.
- **Plantas de tratamiento (PLANT_OP)**: declaran consumo energético y contribuyen al cálculo de Scope 2.
- **Entidades públicas (PUBLIC_ENT)**: monitorizan las emisiones en su ámbito territorial.
- **Coordinadores (COORDINATOR)**: analizan transversalmente las emisiones de los SCRAPs coordinados.
- **AENOR Confía (futuro)**: validación de tasas de emisiones por tipología de vehículo (preparado como integración futura, no implementar ahora).
- **Google (futuro)**: cálculo de rutas sostenibles de mínima emisión (preparado como integración futura, no implementar ahora).

**Objetivos específicos**:

1. Posibilitar el uso de los datos compartidos para identificar oportunidades de innovación en tecnologías que reduzcan las emisiones de carbono durante la gestión de residuos.
2. Facilitar la creación de un repositorio estandarizado — con información de múltiples organismos y sistemas — para propiciar el análisis exhaustivo de la huella de carbono.
3. Proveer KPIs de intensidad de carbono (kgCO₂e/tonelada) desglosados por múltiples dimensiones: tipo de vehículo, combustible, SCRAP, transportista, ruta, zona geográfica, código LER y periodo temporal.
4. Integrar el Scope 1 (transporte) y el Scope 2 (energía de plantas de tratamiento) en una visión consolidada de huella.

---

## 📁 Ubicación de archivos

Todos los dashboards de este módulo deben crearse dentro de la carpeta `Reporting/CarbonFootprint/`, colapsada al mismo nivel que el resto de carpetas de `Reporting` (`HeatMaps`, etc.):

### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/CarbonFootprint/
├── CarbonFootprintOverview.razor            → /reporting/carbon-footprint/overview
├── TransportEmissionsAnalysis.razor         → /reporting/carbon-footprint/transport-emissions
├── PlantEnergyFootprint.razor               → /reporting/carbon-footprint/plant-energy
├── ProducerCarbonReport.razor               → /reporting/carbon-footprint/producer-report
└── PublicEntityCarbonView.razor             → /reporting/carbon-footprint/public-view
```

### Capa Application (CQRS)

```
Application/Features/Reporting/CarbonFootprint/
├── Queries/
│   ├── GetCarbonFootprintOverviewQuery.cs           → Dashboard HC-A (visión consolidada)
│   ├── GetTransportEmissionsAnalysisQuery.cs        → Dashboard HC-B (análisis detallado transporte)
│   ├── GetPlantEnergyFootprintQuery.cs              → Dashboard HC-C (huella energética plantas)
│   ├── GetProducerCarbonReportQuery.cs              → Dashboard HC-D (reporte por productor)
│   ├── GetPublicEntityCarbonViewQuery.cs            → Dashboard HC-E (vista entidad pública)
│   ├── GetEmissionsTrendQuery.cs                    → Widget compartido: evolución temporal
│   ├── GetEmissionsByVehicleTypeQuery.cs            → Widget compartido: desglose por vehículo
│   └── ExportCarbonFootprintToExcelQuery.cs         → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── CarbonFootprintOverviewDto.cs
│   ├── TransportEmissionsAnalysisDto.cs
│   ├── PlantEnergyFootprintDto.cs
│   ├── ProducerCarbonReportDto.cs
│   ├── PublicEntityCarbonViewDto.cs
│   ├── EmissionsTrendDto.cs
│   ├── EmissionsByVehicleTypeDto.cs
│   └── CarbonFootprintExportDto.cs
└── Services/
    └── CarbonFootprintCalculationService.cs         → Servicio de agregación y cálculo de métricas derivadas
```

### Componentes reutilizables

```
Web/Components/Shared/CarbonFootprint/
├── EmissionsSummaryCards.razor               → Cards KPI de emisiones reutilizables
├── EmissionsTrendChart.razor                 → Gráfico de evolución temporal (line/area chart)
├── VehicleTypeEmissionsDonut.razor           → Donut de emisiones por tipo de vehículo
├── FuelTypePieChart.razor                    → Pie de distribución por tipo de combustible
├── CarbonIntensityGauge.razor               → Gauge de intensidad CO₂ (kgCO₂e/t)
└── PlantEnergyComparisonBar.razor           → Bar chart comparativa de consumo energético entre plantas
```

> **Reutilizar** componentes existentes donde aplique: `EmissionsCard.razor`, `WasteVolumeMap.razor` (para mapa con capa de emisiones).

---

## 📊 Dashboards a crear (CINCO vistas diferenciadas)

---

### Dashboard HC-A — **Panel de Visión Consolidada de Huella de Carbono** (`/reporting/carbon-footprint/overview`)

**Destinado a**: perfiles `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewCarbonFootprintOverview` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: visión estratégica consolidada de la huella de carbono total (Scope 1 + Scope 2) generada por la gestión de residuos, con desglose por múltiples dimensiones.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo de huella** (cards de KPI)
   - **Emisiones totales del periodo (kgCO₂e)**: `SUM(WasteMoveResidues.TransportInfo_TransportCarbonEmissions)` del periodo seleccionado. Este es Scope 1 (transporte).
   - **Emisiones Scope 2 del periodo (kgCO₂e)**: derivado de `PlantEnergies.KwhTotal` × factor de conversión del mix eléctrico (`GridMixRef`). El factor de conversión se calcula en el backend (`CarbonFootprintCalculationService`), usando como referencia los factores publicados por REE (Red Eléctrica de España) o el valor configurable en `appsettings.json`.
   - **Huella total combinada (Scope 1 + Scope 2)**.
   - **Intensidad de carbono (kgCO₂e por tonelada gestionada)**: `SUM(TransportCarbonEmissions) / SUM(WasteMoveResidues.Weight) × 1000`.
   - **Variación % vs periodo anterior** en cada KPI (comparando mes vs mes anterior, o trimestre vs trimestre anterior según el filtro).
   - **Toneladas totales gestionadas**: `SUM(WasteMoveResidues.Weight) / 1000`.

2. **Evolución temporal de emisiones** (line chart multi-serie con eje dual)
   - Fuente: `WasteMoveResidues` agrupado por mes/trimestre.
   - Serie 1 (eje izquierdo): emisiones totales Scope 1 (kgCO₂e) por periodo.
   - Serie 2 (eje izquierdo): emisiones totales Scope 2 (kgCO₂e) por periodo.
   - Serie 3 (eje derecho): intensidad de carbono (kgCO₂e/t) por periodo.
   - Referencia visual: línea horizontal punteada con el objetivo de reducción anual (configurable en `appsettings.json`, alineado con «Objetivo 55»: reducción del 55% respecto a niveles de 1990).
   - Periodo: últimos 12 meses por defecto (ajustable con filtros).

3. **Desglose de emisiones por tipo de vehículo** (bar chart horizontal o donut)
   - Fuente: `WasteMoveResidues.VehicleType` + `SUM(TransportInfo_TransportCarbonEmissions)`.
   - Agrupado por `VehicleType` (Furgoneta, Camión 3.5t, Camión 12t, Camión 26t, etc.).
   - Métricas por tipo: emisiones totales (kgCO₂e), nº de traslados, distancia acumulada (km), intensidad (kgCO₂e/km).

4. **Desglose de emisiones por tipo de combustible** (pie chart)
   - Fuente: `WasteMoveResidues.FuelType` + `SUM(TransportInfo_TransportCarbonEmissions)`.
   - Categorías: Diésel, Gasolina, GNC, GNL, Eléctrico, Híbrido, Hidrógeno (según valores existentes).
   - Mostrar % del total y kgCO₂e absolutos por combustible.

5. **Ranking de emisiones por zona geográfica** (tabla + mapa de calor geográfico)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `Entities` (punto de recogida vía `ServiceOrders.IdPickupPoint`).
   - Agrupado por provincia (`Entities.ProvinceCode` → **mostrar `Province.Name`**, NO el código) y opcionalmente por municipio (`Entities.MunicipalityCode` → **mostrar `Municipality.Name`**, NO el código).
   - Métricas por zona: emisiones totales, nº traslados, intensidad, distancia promedio.
   - Semáforo: rojo si la intensidad supera umbral configurable, naranja en rango medio, verde por debajo.
   - Opcional: capa sobre mapa interactivo (reutilizar `WasteVolumeMap.razor` añadiendo capa de emisiones con gradiente de color proporcional a kgCO₂e).

6. **Ranking de emisiones por SCRAP** (tabla ranking + sparklines)
   - Fuente: `WasteMoves.IdScrap` + `WasteMoveResidues`.
   - Columnas: nombre SCRAP (`Entities.Name` vía `WasteMoves.IdScrap`), emisiones totales (kgCO₂e), toneladas gestionadas, intensidad (kgCO₂e/t), nº traslados, distancia total (km), variación % vs periodo anterior.
   - Sparkline: evolución mensual de intensidad por SCRAP.
   - Ordenado por emisiones totales descendente.

7. **Distribución por clase Euro** (stacked bar chart)
   - Fuente: `WasteMoveResidues.EuroClass` + `SUM(TransportInfo_TransportCarbonEmissions)`.
   - Barras apiladas por `EuroClass` (Euro 3, Euro 4, Euro 5, Euro 6, Euro 6d, etc.).
   - Objetivo: evidenciar el impacto de la renovación de flota en la reducción de emisiones.

8. **Tabla de datos exportable** (tabla con exportación XLSX)
   - Fuente: dataset plano combinando `WasteMoves` + `WasteMoveResidues` + `ServiceOrders` + `Entities`.
   - Campos: fecha recogida, SCRAP, transportista (`Entities.Name` vía `WasteMoveResidues.IdCarrier`), origen (nombre del punto de recogida), destino, tipo vehículo, matrícula, combustible, clase Euro, distancia (km), duración (min), peso (kg), emisiones CO₂ (kgCO₂e), intensidad (kgCO₂e/t), factor de emisión aplicado (`EmissionFactorVersion`), código LER.
   - Exportable a XLSX (patrón ClosedXML existente en `ExportKpisToExcelQuery.cs`).
   - Objetivo: repositorio estandarizado para análisis externo y justificación regulatoria.

#### Filtros globales:
- `Year`, `Month`/`Quarter`.
- `IdScrap` (COORDINATOR ve transversalmente los SCRAPs vinculados a sus acuerdos `Agreements.IdCoordinator = LinkedEntityId`; SCRAP ve solo los suyos).
- `ProvinceCode` → mostrar como selector con **nombre de la provincia** (join con `Province.Name`), no código.
- `MunicipalityCode` → mostrar como selector con **nombre del municipio** (join con `Municipality.Name`), no código.
- `VehicleType`.
- `FuelType`.
- `WasteStream`.
- `LERCodes.Code` (con descripción al lado).

---

### Dashboard HC-B — **Panel de Análisis de Emisiones del Transporte** (`/reporting/carbon-footprint/transport-emissions`)

**Destinado a**: perfiles `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `CARRIER` y `ADMIN`.

**Policy de autorización**: `CanViewTransportEmissionsAnalysis` (nueva).

**Propósito**: análisis detallado de las emisiones generadas por el transporte de residuos (Scope 1), con herramientas de comparación para identificar oportunidades de optimización.

#### Widgets / KPIs requeridos:

1. **Eficiencia de la flota** (cards de KPI)
   - **kgCO₂e promedio por recogida**: `AVG(TransportInfo_TransportCarbonEmissions)`.
   - **km promedio por recogida**: `AVG(TransportInfo_TransportDistance)`.
   - **kgCO₂e por km**: `SUM(TransportCarbonEmissions) / SUM(TransportDistance)`.
   - **kgCO₂e por tonelada-km**: `SUM(TransportCarbonEmissions) / (SUM(Weight/1000) × SUM(TransportDistance))`.
   - **% de traslados con vehículo Euro 6 o superior**: indicador de renovación de flota.
   - Variación % vs periodo anterior.

2. **Comparativa pre/post optimización** (bar chart comparativo)
   - Permite seleccionar dos periodos (mes A vs mes B, o trimestre A vs trimestre B).
   - Métricas comparadas:
     - km promedio por recogida.
     - kgCO₂e promedio por recogida.
     - kgCO₂e por tonelada transportada.
     - % de vehículos Euro 6+.
   - Objetivo: evidenciar mejoras tras renovación de flota o cambio de rutas.

3. **Mapa de rutas con emisiones** (mapa interactivo)
   - Fuente: `Entities` (puntos de recogida `IdPickupPoint` con `Latitude`/`Longitude`) + `Entities` (destinos/plantas con `Latitude`/`Longitude`).
   - Líneas entre origen y destino, grosor proporcional al volumen de emisiones.
   - Color del marcador de origen: gradiente por intensidad de carbono.
   - Al hacer clic en un punto: popup con nº de recogidas, kg totales, emisiones acumuladas, distancia promedio, transportista más frecuente.

4. **Rendimiento por transportista** (tabla ranking)
   - Fuente: `WasteMoveResidues.IdCarrier` → `Entities.Name`.
   - Columnas: transportista, nº traslados, distancia total (km), emisiones totales (kgCO₂e), intensidad por km (kgCO₂e/km), intensidad por tonelada (kgCO₂e/t), clase Euro predominante, tipo combustible predominante.
   - Semáforo por intensidad: compara con la media general.
   - Ordenado por emisiones totales descendente.

5. **Heatmap de emisiones por día y hora** (heatmap semanal 7×24)
   - Fuente: `WasteMoves.ActualPickupStart` (si disponible) o `WasteMoves.PlannedPickupStart`, cruzado con `WasteMoveResidues.TransportInfo_TransportCarbonEmissions`.
   - Ejes: día de semana × franja horaria.
   - Intensidad de color: emisiones acumuladas en cada celda.
   - Objetivo: identificar franjas horarias de alta emisión para proponer redistribución.

6. **Detalle por factor de emisión aplicado** (tabla informativa)
   - Fuente: `EmissionFactorSets` JOIN `EmissionFactors` WHERE `EmissionFactorSets.Status = 'Active'`.
   - Columnas: `FactorSetName`, `Version`, `VehicleType`, `FuelType`, `EuroClass`, `Value`, `Unit`.
   - Objetivo: transparencia sobre los factores utilizados en los cálculos. Solo lectura.

7. **Recomendaciones automáticas de reducción** (panel con reglas de negocio)
   - Motor de reglas simple que genera sugerencias textuales basadas en umbrales (cálculo en backend en `CarbonFootprintCalculationService.cs`, no en el cliente):
     - Si % de vehículos Euro ≤ 4 > 30% → "Considerar renovar la flota: el 30% de los traslados utiliza vehículos Euro 4 o inferior, cuya intensidad de emisión es un X% superior a Euro 6".
     - Si intensidad kgCO₂e/t del periodo > 110% de la media del año anterior → "La intensidad de carbono ha aumentado un X% respecto al periodo anterior. Revisar rutas y asignación de vehículos".
     - Si hay transportista con intensidad > 150% de la media → "El transportista X presenta una intensidad de emisión significativamente superior a la media. Considerar revisión de flota o reasignación de rutas".
     - Si distancia promedio ha aumentado > 15% → "La distancia media por recogida ha aumentado un X%. Evaluar la consolidación de recogidas o reubicación de puntos intermedios".
   - Las recomendaciones se generan en el backend, nunca en el cliente.

#### Filtros globales:
- `Year`, `Month`/`Quarter`.
- `IdScrap`.
- `IdCarrier` (selector de transportista → `Entities.Name` donde `EntityRole = 'Carrier'`).
- `VehicleType`, `FuelType`, `EuroClass`.
- `ProvinceCode` → **nombre de la provincia**.
- `MunicipalityCode` → **nombre del municipio**.

---

### Dashboard HC-C — **Panel de Huella Energética de Plantas de Tratamiento** (`/reporting/carbon-footprint/plant-energy`)

**Destinado a**: perfiles `PLANT_OP`, `SCRAP`, `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewPlantEnergyFootprint` (nueva).

**Propósito**: análisis de las emisiones Scope 2 derivadas del consumo energético de las instalaciones de reciclaje y tratamiento.

#### Widgets / KPIs requeridos:

1. **Resumen de consumo energético** (cards de KPI)
   - **kWh totales del periodo**: `SUM(PlantEnergies.KwhTotal)` para las plantas visibles.
   - **kgCO₂e Scope 2 del periodo**: kWh × factor de mix eléctrico (configurable, por defecto factor REE España).
   - **kgCO₂e Scope 2 por tonelada tratada**: Scope 2 total / `SUM(EntryPlants.NetWeight) / 1000`.
   - **Variación % vs periodo anterior**.

2. **Comparativa de consumo entre plantas** (bar chart horizontal)
   - Fuente: `PlantEnergies` agrupado por `PlantName`.
   - Barras: kWh totales por planta en el periodo.
   - Línea superpuesta: kgCO₂e/tonelada tratada por planta (usando `EntryPlants.NetWeight` de la misma planta y periodo).
   - Objetivo: identificar plantas con alta intensidad energética.

3. **Evolución mensual de consumo energético** (area chart)
   - Fuente: `PlantEnergies` agrupado por `Year`/`Month`.
   - Series separadas por planta (si hay más de una visible).
   - Eje derecho opcional: toneladas tratadas por mes para correlacionar volumen con consumo.

4. **Desglose por fuente energética** (pie chart o donut)
   - Fuente: `PlantEnergies.Source` + `SUM(KwhTotal)`.
   - Categorías: Red eléctrica, Solar, Eólica, Gas natural, etc. (según valores de `Source`).
   - Objetivo: evidenciar el mix energético y el peso de renovables.

5. **Eficiencia energética por tipo de operación de tratamiento** (tabla)
   - Fuente: `TreatmentPlants` JOIN `TreatmentOperations` + `PlantEnergies` (correlación por planta y periodo).
   - Columnas: operación de tratamiento (`TreatmentOperations.Code` + `ShortDescription`), toneladas procesadas, kWh estimados (prorrateo por volumen si no hay método de imputación detallado: `AllocationMethod`), kgCO₂e/tonelada.
   - Nota: si `AllocationMethod` no está disponible, indicar "Prorrateo por volumen" como método por defecto.

6. **Tabla de datos exportable** (exportación XLSX)
   - Dataset: planta, año, mes, kWh totales, fuente energética, referencia mix de red, método imputación, toneladas tratadas, kgCO₂e Scope 2, intensidad (kgCO₂e/t).
   - Exportable a XLSX (patrón ClosedXML).

#### Filtros:
- `Year`, `Month`.
- `PlantName` o `PlantCenterCode` (selector con **nombre de la planta**, no código).
- `Source` (fuente energética).
- `ProvinceCode` → **nombre de la provincia** (si la planta tiene datos geográficos vía `Entities`).

---

### Dashboard HC-D — **Reporte de Huella de Carbono para Productores** (`/reporting/carbon-footprint/producer-report`)

**Destinado a**: perfiles `PRODUCER` y `ADMIN`.

**Policy de autorización**: `CanViewProducerCarbonReport` (nueva).

**Propósito**: permitir que los fabricantes de residuos industriales evalúen el impacto ambiental de la gestión de sus residuos, en línea con el «Objetivo 55».

#### Widgets / KPIs requeridos:

1. **Resumen de huella del productor** (cards de KPI)
   - **Emisiones totales asociadas a mis residuos (kgCO₂e)**: `SUM(WasteMoveResidues.TransportInfo_TransportCarbonEmissions)` de traslados cuya SO fue emitida por el productor (`ServiceOrders.IdIssuedBy = LinkedEntityId`).
   - **Toneladas de residuos gestionadas**: `SUM(WasteMoveResidues.Weight) / 1000`.
   - **Intensidad de carbono (kgCO₂e/t)**: emisiones / toneladas.
   - **Nº de traslados del periodo**.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Evolución mensual de mi huella** (line chart)
   - Fuente: `WasteMoveResidues` de traslados del productor, agrupado por mes.
   - Series: emisiones totales (kgCO₂e), intensidad (kgCO₂e/t).
   - Objetivo: ver tendencia interanual para justificar ante auditorías la evolución de su huella.

3. **Desglose por tipo de residuo** (bar chart)
   - Fuente: `WasteMoveResidues` JOIN `Residues` JOIN `LERCodes`.
   - Agrupado por `LERCodes.Code` + `LERCodes.Description`.
   - Métricas: emisiones totales, peso total, intensidad, nº traslados.
   - Objetivo: identificar qué tipos de residuos generan más emisiones en su transporte.

4. **Desglose por destino (planta de tratamiento)** (tabla)
   - Fuente: `WasteMoves.IdDestination` → `Entities.Name` (planta).
   - Columnas: planta destino (nombre), distancia promedio (km), emisiones totales (kgCO₂e), intensidad (kgCO₂e/t), nº traslados.
   - Objetivo: evaluar si un cambio de planta destino reduciría la huella.

5. **Comparativa de mi intensidad vs media del ecosistema** (gauge)
   - Indicador de gauge que muestra la intensidad del productor vs la media del tenant.
   - Zonas: verde (por debajo de la media), amarillo (en la media), rojo (por encima).
   - Nota: la media del ecosistema se calcula solo sobre datos visibles según el filtrado multi-tenant (`OwnerId`).

6. **Certificado de huella descargable** (exportación PDF/XLSX)
   - Resumen del periodo con los KPIs principales, firmado con fecha de generación.
   - Exportable a XLSX (patrón ClosedXML) con todos los datos tabulados.
   - Objetivo: documento para adjuntar a memorias de sostenibilidad o declaraciones ambientales.

#### Filtros:
- `Year`, `Month`/`Quarter`.
- `LERCodes.Code` (con descripción).
- `WasteStream`.

---

### Dashboard HC-E — **Panel de Emisiones para Entidades Públicas** (`/reporting/carbon-footprint/public-view`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Policy de autorización**: `CanViewPublicEntityCarbonView` (nueva).

**Propósito**: los ayuntamientos y entidades públicas monitorizan las emisiones de CO₂ generadas por la gestión de residuos en su ámbito territorial.

#### Widgets / KPIs requeridos:

1. **Resumen de emisiones en mi municipio** (cards de KPI)
   - **Emisiones totales (kgCO₂e)**: de traslados cuyo punto de recogida (`ServiceOrders.IdPickupPoint` → `Entities.MunicipalityCode`) pertenece al municipio de la entidad logueada, o cuya SO fue emitida por la entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`).
   - **Toneladas gestionadas**.
   - **Intensidad (kgCO₂e/t)**.
   - **Nº de traslados**.
   - **Variación % vs periodo anterior**.

2. **Evolución mensual** (line chart)
   - Análogo al HC-D pero filtrado al ámbito territorial de la entidad pública.

3. **Desglose por SCRAP operante** (tabla)
   - Fuente: `WasteMoves.IdScrap` → `Entities.Name`, filtrado al municipio.
   - Columnas: SCRAP, emisiones totales, toneladas, intensidad, nº traslados, distancia promedio.
   - Semáforo por intensidad.

4. **Comparativa mensual por tipo de combustible** (stacked bar chart)
   - Fuente: `WasteMoveResidues.FuelType` agrupado por mes, filtrado al municipio.
   - Barras apiladas: contribución de cada combustible a las emisiones mensuales.
   - Objetivo: ver la evolución del mix de combustibles en su territorio.

5. **Notificaciones de umbrales** (lista tipo inbox)
   - Alertas generadas cuando:
     - La intensidad del municipio supera la media del tenant en más de un X% (configurable).
     - Las emisiones del mes actual superan las del mes anterior en más de un Y%.
   - Fuente: generadas por el backend al procesar los datos del periodo.

#### Filtros:
- `Year`, `Month`.
- `IdScrap` (los SCRAPs que operan en su municipio — derivado de `WasteMoves` históricas o `Agreements`).
- `WasteStream`.
- `LERCodes.Code` (con descripción).

---

## 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas.** Todo el módulo de Huella de Carbono se alimenta de las tablas existentes del modelo v4.1. Las métricas derivadas (intensidad, Scope 2, comparativas, recomendaciones) se calculan en las Queries CQRS y en `CarbonFootprintCalculationService.cs`.

| Tabla | Campos principales para este módulo |
|-------|--------------------------------------|
| `WasteMoveResidues` | `IdWasteMove`, `IdResidue`, `IdCarrier`, `Weight`, `VehicleType`, `FuelType`, `EuroClass`, `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`, `EmissionFactorSetId`, `EmissionFactorVersion`, `TransportInfo_vehicleRegistration` |
| `WasteMoves` | `Id`, `IdSource`, `IdDestination`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `ServiceStatus`, `ActualPickupStart`, `ActualPickupEnd`, `PlannedPickupStart`, `OwnerId` |
| `ServiceOrders` | `Id`, `Status`, `IdPickupPoint`, `IdIssuedBy`, `PlannedPickupStart`, `WasteStream`, `IdLERCode`, `OwnerId` |
| `Entities` | `Id`, `Name`, `EntityRole`, `Latitude`, `Longitude`, `ProvinceCode`, `MunicipalityCode`, `CenterCode` |
| `EmissionFactorSets` | `Id`, `FactorSetName`, `Version`, `Status`, `ValidFrom`, `ValidTo` |
| `EmissionFactors` | `Id`, `FactorSetId`, `VehicleType`, `FuelType`, `EuroClass`, `Unit`, `Value` |
| `PlantEnergies` | `Id`, `PlantName`, `PlantCenterCode`, `Year`, `Month`, `KwhTotal`, `Source`, `GridMixRef`, `AllocationMethod` |
| `EntryPlants` | `Id`, `IdWasteMove`, `PlantEntryDate`, `NetWeight`, `ServiceOrderId`, `OwnerId` |
| `TreatmentPlants` | `Id`, `IdWasteMove`, `IdTreatmentOperation`, `PlantTreatmentDate`, `ServiceOrderId` |
| `TreatmentOperations` | `Id`, `Code`, `OperationType`, `Description`, `ShortDescription`, `IsRecycling`, `IsEnergyRecovery` |
| `Residues` | `Id`, `Name`, `Reference`, `ResidueType`, `IdLERCode`, `IdProducer` |
| `LERCodes` | `Id`, `Code`, `Description`, `IsDangerous`, `ChapterCode`, `SubChapterCode` |
| `Agreements` | `Id`, `IdScrap`, `IdCoordinator`, `IdPublicEntity` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `WasteMoveReference` |
| `Province` | `id`, `idState`, `Ref`, `Code`, **`Name`** |
| `Municipality` | `Id`, `Id_Province`, `Code`, **`Name`** |
| `TerritoryState` | `id`, `idCountry`, `Ref`, `Code`, **`Name`** |

---

## 🔒 Reglas de autorización y filtrado de datos

> **IMPORTANTE**: El acceso a estos dashboards se gestiona mediante el **sistema de autorización por pantalla configurable desde la interfaz de administración** (`/security/page-permissions`) utilizando las tablas `PageDefinitions` y `PagePermissions`. **No se hardcodea el acceso en código.** Las policies en código (`CanViewCarbonFootprintOverview`, etc.) actúan como mínimo de seguridad estático; el control fino se delega al sistema dinámico de BD.

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | HC-A, HC-B | Solo ve datos de traslados donde `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. |
| `PRODUCER` | HC-D | Solo ve traslados cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`). |
| `CARRIER` | HC-B | Solo ve traslados donde está asignado como transportista (`WasteMoveResidues.IdCarrier = LinkedEntityId`). |
| `PLANT_OP` | HC-C | Solo ve datos de `PlantEnergies` de su planta (`PlantCenterCode` = código de centro de su entidad) y `EntryPlants` de su planta. |
| `PUBLIC_ENT` | HC-E | Solo ve datos de traslados cuyo punto de recogida (`ServiceOrders.IdPickupPoint` → `Entities.MunicipalityCode`) pertenece a su municipio, o cuya SO fue emitida por su entidad. |
| `COORDINATOR` | HC-A | Ve transversalmente los SCRAPs vinculados a sus acuerdos (`Agreements.IdCoordinator = LinkedEntityId`). |
| `DISPATCH_OFFICE` | HC-A, HC-B, HC-C | Ve todos los datos del tenant (`OwnerId`). Visión completa equivalente a ADMIN dentro de su ámbito operativo. |
| `ADMIN` | HC-A, HC-B, HC-C, HC-D, HC-E | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, transportista, planta, productor o entidad pública. |

**Patrón de filtrado de datos**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` + `IDataScopeService.ApplyScope()` (ya implementado).

**Control de acceso a pantallas**: los permisos se gestionan dinámicamente desde `/security/page-permissions` mediante el sistema `PageDefinitions`/`PagePermissions`. La tabla anterior documenta la **configuración recomendada por defecto**. Las policies en código actúan como mínimo de seguridad; el control fino se delega al sistema dinámico de BD.

---

## 📊 Matriz de permisos recomendada por defecto

> ⚠️ Esta matriz documenta la **configuración recomendada por defecto** para aplicar desde `/security/page-permissions` tras el despliegue. **No es un hardcodeo en código.**

| Pantalla | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **HC-A Visión Consolidada** | — | — | R | — | — | — | R | R | R |
| **HC-B Emisiones Transporte** | — | R | R | — | — | — | R | R | R |
| **HC-C Huella Energética Plantas** | — | — | R | — | — | R | — | R | R |
| **HC-D Reporte Productor** | R | — | — | — | — | — | — | — | R |
| **HC-E Vista Entidad Pública** | — | — | — | R | — | — | — | R | R |

Leyenda: **R** = Lectura, **—** = Sin acceso.

---

## 🗺️ Regla obligatoria: Datos geográficos siempre como nombre, nunca como código

En **todas** las pantallas, tablas, filtros y exportaciones de este módulo:

- Cuando se muestre `ProvinceCode` → resolver siempre a `Province.Name` mediante JOIN con la tabla `Province` (clave: `Entities.ProvinceCode = Province.Code` vía `Province.idState` → `TerritoryState`).
- Cuando se muestre `MunicipalityCode` → resolver siempre a `Municipality.Name` mediante JOIN con la tabla `Municipality` (clave: `Entities.MunicipalityCode = Municipality.Code`).
- Cuando se muestre `StateCode` → resolver siempre a `TerritoryState.Name`.
- En los selectores/filtros de provincia y municipio, mostrar `Name` como label y `Code` como value interno.
- En las exportaciones XLSX, incluir columnas con el **nombre** de la provincia/municipio, no el código. Opcionalmente incluir el código en una columna adicional si es relevante para interoperabilidad.

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`) según la tabla de reglas de autorización.
2. **El acceso a cada dashboard NO está hardcodeado en código.** Se gestiona mediante el sistema de autorización por pantalla (`PageDefinitions`/`PagePermissions`) configurable desde `/security/page-permissions`.
3. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
4. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en HC-A y HC-C como mínimo (patrón ya implementado con ClosedXML en `ExportKpisToExcelQuery.cs`).
7. Responsive mobile-first (operadores de campo).
8. Modo oscuro/claro (consistente con `MainLayout.razor`).
9. El **factor de conversión kWh → kgCO₂e** para Scope 2 es configurable (en `appsettings.json` o como parámetro del sistema), no hardcodeado. Valor por defecto: factor del mix eléctrico español publicado por REE.
10. Los **umbrales de recomendaciones y alertas** son configurables (en `appsettings.json` o como parámetros del sistema).
11. Las recomendaciones se generan en el backend (`CarbonFootprintCalculationService.cs`), no en el cliente.
12. **No se crean nuevas entidades de dominio.** Todo se implementa con las entidades del modelo v4.1 existente.
13. Cada usuario solo ve datos de las entidades asignadas a él o creadas por él, **a excepción de `ADMIN` y `DISPATCH_OFFICE` que ven todos los datos del tenant**.
14. En todos los puntos donde se muestren datos geográficos (provincia, municipio) — ya sea en tablas, filtros, selectores o exportaciones — se muestra **siempre el nombre** (`Province.Name`, `Municipality.Name`), **nunca el código** (`ProvinceCode`, `MunicipalityCode`).

---

## 🔗 Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de emisiones totales e intensidad de carbono pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **Trazabilidad (§5.1)**: desde cualquier traslado en los dashboards HC, enlace directo a `/traceability?term={WasteMoveReference}`.
- **KPIs regulatorios (§5.2)**: el KPI de "intensidad CO₂ por tonelada" ya existe en `/kpis`; en los dashboards HC se desglosa por múltiples dimensiones adicionales (vehículo, combustible, ruta, zona, SCRAP, transportista).
- **Dashboard Optimización Logística (§5.6)**: comparte fuentes de datos con el widget de eficiencia de rutas (cards de kgCO₂e por recogida). Los componentes `EmissionsCard.razor` se reutilizan.
- **Módulo de Movilidad Urbana (UC3)**: enlace cruzado desde HC-A al UC3-A para correlacionar emisiones con impacto en movilidad.
- **Mapas de Calor**: enlace cruzado desde HC-A a `/reporting/heat-maps/waste-density` para correlacionar zonas de alta densidad de residuos con zonas de alta emisión.
- **Incidencias (§4.3)**: los widgets de incidencias logísticas (averías de vehículos, retrasos) enlazan a `/incidents/{id}` para correlacionar incidencias con picos de emisiones.
- **Consumo energético de plantas (§4.2)**: el dashboard HC-C extiende la vista existente de `PlantEnergies` añadiendo la perspectiva de huella de carbono Scope 2.

---

## 🛠️ Registro de pantallas y auto-descubrimiento

### Actualización de `PageDiscoveryService.InferModuleName()`

Las rutas `/reporting/carbon-footprint/` deben clasificarse en el módulo **Reporting** existente. Si la ruta base `Reporting` ya está mapeada (como indica la tabla actual de `InferModuleName`), no es necesario añadir un nuevo caso. **Verificar** que las rutas con el patrón `/reporting/carbon-footprint/*` se clasifican correctamente.

### Actualización de `PageDiscoveryService.HumanizeName()`

Nombres legibles recomendados para las pantallas de Huella de Carbono:

| Componente | Nombre legible |
|---|---|
| `CarbonFootprintOverview` | `Huella de Carbono — Visión Consolidada` |
| `TransportEmissionsAnalysis` | `Huella de Carbono — Emisiones del Transporte` |
| `PlantEnergyFootprint` | `Huella de Carbono — Huella Energética Plantas` |
| `ProducerCarbonReport` | `Huella de Carbono — Reporte Productor` |
| `PublicEntityCarbonView` | `Huella de Carbono — Vista Entidad Pública` |

### Nuevas policies a registrar

Añadir en `Domain/Authorization/PolicyConstants.cs`:

```csharp
public const string CanViewCarbonFootprintOverview = nameof(CanViewCarbonFootprintOverview);
public const string CanViewTransportEmissionsAnalysis = nameof(CanViewTransportEmissionsAnalysis);
public const string CanViewPlantEnergyFootprint = nameof(CanViewPlantEnergyFootprint);
public const string CanViewProducerCarbonReport = nameof(CanViewProducerCarbonReport);
public const string CanViewPublicEntityCarbonView = nameof(CanViewPublicEntityCarbonView);
```

Registrar en `Program.cs` con los perfiles permitidos como mínimo de seguridad:

```csharp
options.AddPolicy(PolicyConstants.CanViewCarbonFootprintOverview, p =>
    p.RequireRole("ADMIN", "SCRAP", "COORDINATOR", "DISPATCH_OFFICE"));

options.AddPolicy(PolicyConstants.CanViewTransportEmissionsAnalysis, p =>
    p.RequireRole("ADMIN", "SCRAP", "COORDINATOR", "DISPATCH_OFFICE", "CARRIER"));

options.AddPolicy(PolicyConstants.CanViewPlantEnergyFootprint, p =>
    p.RequireRole("ADMIN", "PLANT_OP", "SCRAP", "DISPATCH_OFFICE"));

options.AddPolicy(PolicyConstants.CanViewProducerCarbonReport, p =>
    p.RequireRole("ADMIN", "PRODUCER"));

options.AddPolicy(PolicyConstants.CanViewPublicEntityCarbonView, p =>
    p.RequireRole("ADMIN", "PUBLIC_ENT", "DISPATCH_OFFICE"));
```

### Entrada en `NavMenu.razor`

Añadir en la sección **Reporting** del menú, como subcarpeta colapsable "Huella de Carbono":

```razor
@* — Huella de Carbono (colapsable) — *@
<NavSection Title="Huella de Carbono" Icon="leaf" Collapsed="true">
    <NavLink href="/reporting/carbon-footprint/overview"
             Visible="@(await PagePermissionService.CanAccessRouteAsync("/reporting/carbon-footprint/overview"))">
        Visión Consolidada
    </NavLink>
    <NavLink href="/reporting/carbon-footprint/transport-emissions"
             Visible="@(await PagePermissionService.CanAccessRouteAsync("/reporting/carbon-footprint/transport-emissions"))">
        Emisiones del Transporte
    </NavLink>
    <NavLink href="/reporting/carbon-footprint/plant-energy"
             Visible="@(await PagePermissionService.CanAccessRouteAsync("/reporting/carbon-footprint/plant-energy"))">
        Huella Energética Plantas
    </NavLink>
    <NavLink href="/reporting/carbon-footprint/producer-report"
             Visible="@(await PagePermissionService.CanAccessRouteAsync("/reporting/carbon-footprint/producer-report"))">
        Reporte Productor
    </NavLink>
    <NavLink href="/reporting/carbon-footprint/public-view"
             Visible="@(await PagePermissionService.CanAccessRouteAsync("/reporting/carbon-footprint/public-view"))">
        Vista Entidad Pública
    </NavLink>
</NavSection>
```

---

## ⚙️ Configuración en `appsettings.json`

```json
{
  "CarbonFootprint": {
    "Scope2": {
      "DefaultGridMixFactor_kgCO2e_per_kWh": 0.22,
      "FactorSource": "REE Spain 2024",
      "FactorNotes": "Factor medio del mix eléctrico peninsular español"
    },
    "Thresholds": {
      "IntensityAlertPercent": 110,
      "OldFleetAlertPercent": 30,
      "MaxEuroClassForOldFleet": 4,
      "DistanceIncreaseAlertPercent": 15,
      "TransporterIntensityAlertPercent": 150,
      "MunicipalEmissionIncreaseAlertPercent": 20
    },
    "ReferenceTarget": {
      "ReductionPercent2030vs1990": 55,
      "Notes": "Objetivo 55 — Ley Europea del Clima"
    }
  }
}
```

---

## ✅ Checklist técnico al implementar

- [ ] Crear carpeta `Web/Components/Pages/Reporting/CarbonFootprint/` con las 5 páginas `.razor`.
- [ ] Crear carpeta `Application/Features/Reporting/CarbonFootprint/` con Queries, DTOs y Services.
- [ ] Crear carpeta `Web/Components/Shared/CarbonFootprint/` con componentes reutilizables.
- [ ] Registrar las 5 policies nuevas en `PolicyConstants.cs` y `Program.cs`.
- [ ] Verificar que `InferModuleName()` clasifica las rutas `/reporting/carbon-footprint/*` en el módulo "Reporting".
- [ ] Actualizar `HumanizeName()` con los 5 nombres legibles en español.
- [ ] Añadir sección "Huella de Carbono" en `NavMenu.razor` dentro de Reporting, colapsada.
- [ ] Añadir configuración `CarbonFootprint` en `appsettings.json`.
- [ ] Implementar `CarbonFootprintCalculationService.cs` con lógica de Scope 2, recomendaciones y umbrales.
- [ ] Cada Query handler aplica filtro `OwnerId` + `IDataScopeService.ApplyScope()`.
- [ ] Todos los JOINs geográficos resuelven `ProvinceCode` → `Province.Name` y `MunicipalityCode` → `Municipality.Name`.
- [ ] Exportación XLSX en HC-A y HC-C mínimo (patrón ClosedXML).
- [ ] Gráficos con ApexCharts, responsive, modo oscuro/claro.
- [ ] Filtros persistidos en query string.
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions` según la matriz recomendada.
- [ ] Verificar que las pantallas nuevas aparecen en amarillo en `/security/page-permissions` hasta que el admin las configure.
