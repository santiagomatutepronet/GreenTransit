# Prompt para GitHub Copilot — Módulo de Ecomodulación (UC5)

> **Instrucción**: copia este prompt completo y pégalo en GitHub Copilot Chat adjuntando los archivos `README.md`, `Crear_BD_v4_1.sql`, `COPILOT_CONTEXT.md` y `Mapa_Funcionalidades.md`.

---

## Prompt

Necesito que crees un **nuevo módulo de dashboard titulado "Ecomodulación"** (UC5) dentro de la aplicación GreenTransit. Este módulo debe facilitar el control de datos de trazabilidad de residuos por parte de los SCRAPs (proveedores del dato) y su consumo por parte de autoridades reguladoras, organismos europeos interesados en el Pasaporte Digital de Producto (DPP), responsables de sostenibilidad, coordinadores logísticos, entidades públicas y certificadores/validadores.

El objetivo es sincronizar la documentación y el seguimiento de los residuos a partir de los datos compartidos en el ecosistema de datos (Data Space), alineándose con estándares europeos para promover el ecodiseño de productos y posibilitar la convergencia con el DPP.

---

### 📁 Estructura de carpetas

Todos los dashboards de este módulo deben crearse dentro de la carpeta **`Ecomodulación`** bajo la carpeta **`Reporting`** existente. Sigue esta estructura:

#### Capa Application (CQRS)

```
Application/Features/Ecomodulation/
├── Queries/
│   ├── GetEcomodulationScrapOverviewQuery.cs       → UC5-A (SCRAP — proveedor del dato)
│   ├── GetEcomodulationRegulatoryViewQuery.cs       → UC5-B (Regulador / Certificador)
│   ├── GetEcomodulationDppReadinessQuery.cs          → UC5-C (DPP — vista Pasaporte Digital)
│   ├── GetEcomodulationRuleImpactQuery.cs            → Widget compartido: impacto reglas eco-modulación
│   ├── GetCircularityIndexQuery.cs                   → Widget compartido: índice de circularidad
│   └── ExportEcomodulationDataToExcelQuery.cs        → Exportación XLSX (patrón ClosedXML existente)
├── DTOs/
│   ├── EcomodulationScrapOverviewDto.cs
│   ├── EcomodulationRegulatoryViewDto.cs
│   ├── EcomodulationDppReadinessDto.cs
│   ├── EcomodulationRuleImpactDto.cs
│   ├── CircularityIndexDto.cs
│   └── EcomodulationExportRowDto.cs
└── Services/
    └── EcomodulationRecommendationEngine.cs          → Motor de recomendaciones de ecodiseño
```

#### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/Ecomodulación/
├── ScrapOverview.razor            → /reporting/ecomodulation/scrap-overview
├── RegulatoryView.razor           → /reporting/ecomodulation/regulatory-view
└── DppReadiness.razor             → /reporting/ecomodulation/dpp-readiness
```

#### Componentes reutilizables

```
Web/Components/Pages/Reporting/Ecomodulación/Components/
├── CircularityGauge.razor         → gauge de índice de circularidad por producto/categoría
├── EcomodulationRuleCard.razor    → card de regla de eco-modulación con impacto económico
├── DppComplianceTable.razor       → tabla de preparación DPP por producto
├── MaterialCompositionChart.razor → gráfico de composición de materiales (ApexCharts)
└── EcodesignScorecard.razor       → scorecard de ecodiseño (reparabilidad, desmontaje, reciclado)
```

> **Reutilizar** de los dashboards existentes: `EmissionsCard.razor`, `IncidentsBadge.razor`.

---

### 📊 Dashboards a crear (son TRES vistas diferenciadas)

---

#### Dashboard UC5-A — **Panel de Datos de Ecomodulación — SCRAP (Proveedor del dato)** (`/reporting/ecomodulation/scrap-overview`)

**Destinado a**: perfiles `SCRAP`, `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewEcomodulationScrapOverview` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: los SCRAPs, como proveedores principales de datos en el Espacio de Datos compartido, visualizan y validan la calidad de los datos de ecodiseño de los productos que gestionan, controlan el estado de las fichas técnicas (`ProductSpecs`) y monitorizan el impacto económico de las reglas de eco-modulación sobre sus liquidaciones.

##### Widgets / KPIs requeridos:

1. **Cobertura de fichas de ecodiseño** (cards de KPI + donut chart)
   - Fuente: `Residues` WHERE `ResidueType = 'ProductSpec'` cruzado con `ProductSpecs`.
   - KPIs:
     - **Total de productos gestionados** = `COUNT(Residues)` WHERE `ResidueType IN ('Product', 'ProductSpec')` y vinculados a traslados del SCRAP.
     - **% con ficha de ecodiseño completa** = productos con `ReparabilityIndex IS NOT NULL AND RecycledContentPercent IS NOT NULL AND CompositionJson IS NOT NULL` / total.
     - **% con código LER potencial al fin de vida** = productos con `PotentialLERCodesJson IS NOT NULL` / total.
   - Donut: "Con ficha completa" vs "Ficha parcial" vs "Sin ficha".

2. **Índice de circularidad agregado por categoría** (bar chart horizontal)
   - Fuente: `Residues` (ProductSpec) → campos `RecycledContentPercent`, `ReparabilityIndex`, `DisassemblyEase`, `ContainsHazardous`.
   - **Índice de circularidad** = ponderación configurable (por defecto: 30% RecycledContentPercent + 25% ReparabilityIndex normalizado + 20% DisassemblyEase normalizado + 15% !ContainsHazardous + 10% tiene PotentialLERCodesJson).
   - Agrupado por `ProductCategory`.
   - Semáforo: verde > 70, naranja 40–70, rojo < 40.

3. **Impacto económico de las reglas de eco-modulación** (tabla + sparklines)
   - Fuente: `EcoModulationRuleSets` JOIN `EcoModulationRules` cruzado con `SettlementLines` del periodo.
   - Columnas por regla aplicada: nombre de la regla, nº de productos afectados, ajuste económico total (€), tipo de impacto (Percent/Fixed/PerUnit), variación vs periodo anterior.
   - Sparkline de evolución mensual del ajuste.

4. **Estado de validación de fichas técnicas por productor** (tabla con semáforo)
   - Fuente: `ProductSpecs` JOIN `Residues` JOIN `Entities` (Producer).
   - Columnas: Productor, nº productos declarados, nº con ficha completa, nº con ficha parcial, nº sin ficha, % cobertura.
   - Semáforo por fila según % cobertura (verde > 80%, naranja 50–80%, rojo < 50%).
   - Drill-down al detalle del productor.

5. **Composición de materiales agregada** (treemap o stacked bar chart)
   - Fuente: `Residues.CompositionJson` (ProductSpec) — parsear el JSON para extraer materiales y porcentajes.
   - Visualización: distribución de materiales predominantes en el portafolio del SCRAP.
   - Objetivo: identificar oportunidades de mejora en reciclabilidad.

6. **Panel de datos exportables para análisis externo / Data Space** (tabla con exportación XLSX)
   - Fuente: `ProductSpecs` + `Residues` (ProductSpec) + `Entities` (Producer) + `EcoModulationRules`.
   - Dataset plano con campos: referencia producto, productor, categoría, código LER, índice reparabilidad, % contenido reciclado, facilidad desmontaje, contiene peligrosos (sí/no), composición, regla eco-modulación aplicable, ajuste económico.
   - Exportable a XLSX (patrón ClosedXML existente).
   - Objetivo: proveer datos limpios y estructurados para publicación en el Data Space (EDC) o análisis externo.

##### Filtros globales:
- `Year`.
- `ProductCategory`.
- `IdProducer` (filtrar por productor específico vinculado al SCRAP vía `Agreements`).
- `EcoModulationRuleSetId` (versión de reglas a aplicar).

---

#### Dashboard UC5-B — **Panel de Monitorización Regulatoria — Autoridad / Certificador** (`/reporting/ecomodulation/regulatory-view`)

**Destinado a**: perfiles `PUBLIC_ENT`, `COORDINATOR` y `ADMIN`.

**Policy de autorización**: `CanViewEcomodulationRegulatoryView` (nueva).

**Propósito**: las autoridades reguladoras, entidades públicas, coordinadores y certificadores consumen los datos del ecosistema para verificar el cumplimiento de criterios de ecodiseño, validar la calidad de los datos compartidos y evaluar la preparación del sector para la convergencia con el DPP europeo.

##### Widgets / KPIs requeridos:

1. **Resumen ejecutivo de ecomodulación** (cards de KPI)
   - **Total de SCRAPs con datos publicados**: `COUNT(DISTINCT WasteMoves.IdScrap)` WHERE tienen `ProductSpecs` asociadas.
   - **Nº total de fichas de ecodiseño**: `COUNT(ProductSpecs)`.
   - **Índice de circularidad medio del ecosistema**: media ponderada del índice por categoría.
   - **% de productos con potencial de mejora**: productos con `ReparabilityIndex < 5` o `RecycledContentPercent < 30%`.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Ranking de SCRAPs por madurez de datos de ecodiseño** (tabla ranking + sparklines)
   - Fuente: `Entities` (SCRAP) cruzado con `ProductSpecs` y `Residues`.
   - Métricas por SCRAP:
     - Nº de productos en portafolio.
     - % cobertura de fichas de ecodiseño.
     - Índice de circularidad medio.
     - Nº de reglas de eco-modulación activas.
   - **Índice de madurez** = ponderación configurable (0–100).
   - Ordenado por índice descendente. Semáforo: verde > 70, naranja 40–70, rojo < 40.

3. **Análisis comparativo de ecodiseño por categoría de producto** (radar chart o grouped bar chart)
   - Fuente: `Residues` (ProductSpec) agrupado por `ProductCategory`.
   - Ejes: Reparabilidad media, % reciclado medio, facilidad desmontaje (% Easy), % sin sustancias peligrosas, cobertura LER potencial.
   - Permite a los reguladores identificar categorías con menor desempeño ambiental.

4. **Evolución temporal de indicadores de ecodiseño** (line chart multi-serie)
   - Series: índice de circularidad medio, % cobertura fichas, % contenido reciclado medio.
   - Fuente: agregación trimestral/anual de las métricas ya calculadas.
   - Objetivo: verificar la tendencia de mejora del sector a lo largo del tiempo.

5. **Alertas de cumplimiento** (lista tipo inbox)
   - Alertas generadas cuando:
     - Un SCRAP tiene < 50% de cobertura de fichas de ecodiseño.
     - El índice de circularidad de una categoría cae por debajo del umbral configurable.
     - Se detectan productos con `ContainsHazardous = 1` y sin `DangerousCode` informado.
   - Fuente: generadas por el backend al recalcular indicadores.

##### Filtros:
- `Year`.
- `IdScrap` (los SCRAPs del ecosistema — para reguladores: todos los del tenant; para coordinadores: los de sus acuerdos vía `Agreements.IdCoordinator`).
- `ProductCategory`.
- `ProvinceCode` / `AutonomousCommunity`.

---

#### Dashboard UC5-C — **Panel de Preparación para el Pasaporte Digital de Producto (DPP)** (`/reporting/ecomodulation/dpp-readiness`)

**Destinado a**: perfiles `SCRAP`, `COORDINATOR`, `PUBLIC_ENT`, `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewEcomodulationDppReadiness` (nueva).

**Propósito**: evalúa el grado de preparación de los datos del ecosistema para cumplir con los requisitos del Pasaporte Digital de Producto europeo. Identifica campos faltantes, valida la completitud de las fichas técnicas y genera un score de preparación DPP por producto y por categoría.

##### Widgets / KPIs requeridos:

1. **Score global de preparación DPP** (gauge grande + trend)
   - **Score DPP** = % de campos requeridos por el DPP que están informados en las fichas de ecodiseño del ecosistema.
   - Campos DPP evaluados (mapeados a `Residues` ProductSpec):
     - `Name` → Nombre del producto ✓
     - `Reference` → Identificador único ✓
     - `IdLERCode` → Clasificación de residuo al fin de vida ✓
     - `ReparabilityIndex` → Índice de reparabilidad ✓
     - `DisassemblyEase` → Facilidad de desmontaje ✓
     - `RecycledContentPercent` → Contenido reciclado ✓
     - `CompositionJson` → Composición de materiales ✓
     - `ContainsHazardous` + `DangerousCode` → Sustancias peligrosas ✓
     - `PotentialLERCodesJson` → Códigos LER potenciales ✓
     - `IdProducer` → Productor identificado ✓
   - Score = (campos informados / campos requeridos) × 100, promediado sobre todos los productos.

2. **Detalle de preparación DPP por producto** (tabla con checkmarks)
   - Fuente: `ProductSpecs` JOIN `Residues` (ProductSpec).
   - Columnas: Referencia, Nombre, Productor, y una columna por cada campo DPP con ✓ (informado) o ✗ (faltante).
   - Score individual por producto.
   - Filtrable y ordenable por score ascendente (para priorizar productos que necesitan datos).

3. **Mapa de calor de completitud por categoría × campo DPP** (heatmap)
   - Ejes: categorías de producto (filas) × campos DPP (columnas).
   - Valores: % de productos en esa categoría que tienen el campo informado.
   - Color: verde > 80%, naranja 50–80%, rojo < 50%.

4. **Productos prioritarios para completar** (tabla top-N)
   - Los N productos con peor score DPP, ordenados ascendentemente.
   - Incluye: referencia, productor, campos faltantes (listados), score actual.
   - Objetivo: guía de acción para los SCRAPs y productores.

5. **Comparativa de preparación DPP por SCRAP** (bar chart horizontal)
   - Fuente: `ProductSpecs` agrupadas por SCRAP (vía `Entities.IdProducer` → `Agreements.IdScrap`).
   - Barra: score DPP medio por SCRAP.
   - Permite a los coordinadores y reguladores identificar qué SCRAPs están más/menos preparados.

6. **Histórico de evolución del score DPP** (line chart)
   - Serie: score DPP global por trimestre/año.
   - Objetivo: demostrar progreso hacia la convergencia con el DPP europeo.

##### Filtros:
- `Year`.
- `IdScrap`.
- `ProductCategory`.
- `IdProducer`.

---

### 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el UC5 se alimenta de las tablas existentes del modelo v4.1. Los nuevos índices (circularidad, score DPP, madurez) se calculan en las Queries CQRS.

| Tabla | Campos principales para este UC5 |
|-------|----------------------------------|
| `Residues` | `Id`, `ResidueType` (filtrar por `Product` y `ProductSpec`), `Name`, `Reference`, `IdLERCode`, `ReparabilityIndex`, `DisassemblyEase`, `ContainsHazardous`, `RecycledContentPercent`, `CompositionJson`, `PotentialLERCodesJson`, `MaterialsJson`, `IdProducer`, `ProductCategory`, `IsActive` |
| `ProductSpecs` | `Id`, `ProductRef`, `IdResidue` (FK → Residues ProductSpec), `ProductUse`, `ProductCategory`, `IdProducer`, `Version`, `Hash` |
| `ProductDeclaration` | `Id`, `IdProducer`, `Period`, `Year`, `State`, `OwnerId` |
| `Products` | `Id`, `IdProductDeclaration`, `IdResidue` (FK → Residues Product), `Reference`, `Quantity`, `MeasureUnit` |
| `EcoModulationRuleSets` | `Id`, `RuleSetName`, `Version`, `ValidFrom`, `ValidTo`, `Status` |
| `EcoModulationRules` | `Id`, `RuleSetId`, `CriteriaJson`, `ImpactType` (Percent/Fixed/PerUnit), `ImpactValue` |
| `Entities` | `Id`, `Name`, `EntityRole`, `ProvinceCode`, `MunicipalityCode` |
| `Agreements` | `Id`, `IdScrap`, `IdCoordinator`, `IdPublicEntity` |
| `Settlements` | `Id`, `IdScrap`, `AgreementId`, `Year`, `Month`, `Status` |
| `SettlementLines` | `Id`, `SettlementId`, `IdLERCode`, `Weight`, `Amount` |
| `LERCodes` | `Id`, `Code`, `Description`, `IsDangerous`, `IsRAEE` |

---

### 🔒 Reglas de autorización y filtrado de datos

> ⚠️ **CRÍTICO**: El acceso a estos dashboards **NO se hardcodea en código**. Se gestiona dinámicamente mediante el sistema `PageDefinitions`/`PagePermissions` existente, configurable desde la interfaz de administración `/security/page-permissions`. Las policies en código actúan como **mínimo de seguridad estático** (suelo).

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | UC5-A, UC5-C | Solo ve datos de productos vinculados a sus acuerdos (`Agreements.IdScrap = LinkedEntityId`). Los productores cuyas fichas visualiza son los adheridos a sus acuerdos. |
| `PUBLIC_ENT` | UC5-B, UC5-C | Solo ve datos de SCRAPs que operan en su ámbito territorial (`Agreements.IdPublicEntity = LinkedEntityId` o `Entities.MunicipalityCode` de productores en su municipio). |
| `COORDINATOR` | UC5-B, UC5-C | Ve transversalmente los SCRAPs vinculados a sus acuerdos (`Agreements.IdCoordinator = LinkedEntityId`). |
| `DISPATCH_OFFICE` | UC5-A, UC5-C | Ve todos los datos del tenant (`OwnerId`). Acceso equivalente a ADMIN en cuanto a filtrado de datos. |
| `ADMIN` | UC5-A, UC5-B, UC5-C | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, productor o categoría. |

**Patrón de filtrado de datos**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` (ya implementado). Aplicar `IDataScopeService.ApplyScope()` en todos los Query handlers.

**Excepción para ADMIN y DISPATCH_OFFICE**: estos perfiles **deben ver todos los datos** del tenant sin restricción adicional de `LinkedEntityId`. El `ApplyScope` solo aplica `OwnerId` para ellos.

**Control de acceso a pantallas**: los permisos se gestionan dinámicamente desde `/security/page-permissions` mediante el sistema `PageDefinitions`/`PagePermissions`. La tabla anterior documenta la **configuración recomendada por defecto** que el administrador debe aplicar tras el despliegue. Las policies en código actúan como mínimo de seguridad; el control fino se delega al sistema dinámico de BD.

---

### 🔑 Policies de autorización nuevas (mínimo de seguridad en código)

Añadir en `Domain/Authorization/PolicyConstants.cs`:

```
Policy                              Perfiles permitidos
─────────────────────────────────── ─────────────────────────────────────
CanViewEcomodulationScrapOverview   SCRAP, DISPATCH_OFFICE, ADMIN
CanViewEcomodulationRegulatoryView  PUBLIC_ENT, COORDINATOR, ADMIN
CanViewEcomodulationDppReadiness    SCRAP, COORDINATOR, PUBLIC_ENT, DISPATCH_OFFICE, ADMIN
```

Registrar cada policy en `Program.cs` con los perfiles indicados.

---

### 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`), con la excepción explícita de `ADMIN` y `DISPATCH_OFFICE` que ven todos los datos del tenant.
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
4. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
5. Exportación a XLSX disponible en UC5-A (patrón ya implementado con ClosedXML en `ExportKpisToExcelQuery.cs`).
6. Responsive mobile-first (operadores de campo).
7. Modo oscuro/claro (consistente con `MainLayout.razor`).
8. El **índice de circularidad** usa pesos configurables para cada métrica (inicialmente: 30% RecycledContentPercent + 25% ReparabilityIndex + 20% DisassemblyEase + 15% !ContainsHazardous + 10% PotentialLERCodesJson). Los pesos se almacenan como parámetros en `appsettings.json`, no hardcodeados.
9. El **score DPP** se calcula en el backend (Query handler), no en el cliente.
10. Las recomendaciones de ecodiseño se generan en el backend (`EcomodulationRecommendationEngine.cs`), no en el cliente.

---

### 🔗 Integración con módulos existentes

- **KPIs regulatorios (§5.2)**: el KPI de "tasa de reciclaje" y "preparación para reutilización" ya existe en `/kpis`; aquí se desglosa por producto y se cruza con datos de ecodiseño.
- **Declaraciones de producto (§10)**: las fichas de ecodiseño se nutren de las declaraciones de producto (`ProductDeclaration` + `Products`). Enlace cruzado desde UC5-A a `/product-declarations`.
- **Liquidaciones (§2.2)**: el impacto económico de las reglas de eco-modulación se calcula sobre `SettlementLines`. Enlace desde el widget de impacto económico a `/settlements/{id}`.
- **Trazabilidad (§5.1)**: desde cualquier producto en los dashboards UC5, enlace directo a `/traceability?term={ProductRef}`.
- **Data Space / EcoDataNet (§5.5 y §11)**: el dataset exportable del UC5-A está preparado para publicación vía conectores EDC (`Users.PortalEDCProvider`).
- **Dashboard principal (§0.1)**: los widgets de circularidad y score DPP pueden integrarse como cards adicionales en la home, condicionados al perfil.

---

### 🏗️ Checklist de implementación

#### Paso 1 — Capa Domain
- [ ] Añadir las 3 policies nuevas en `PolicyConstants.cs`.

#### Paso 2 — Capa Application
- [ ] Crear la estructura de carpetas `Features/Ecomodulation/` con Queries, DTOs y Services.
- [ ] Implementar los 3 Query handlers principales (UC5-A, UC5-B, UC5-C).
- [ ] Implementar los 2 Query handlers de widgets compartidos (CircularityIndex, RuleImpact).
- [ ] Implementar `ExportEcomodulationDataToExcelQuery` (patrón ClosedXML).
- [ ] Implementar `EcomodulationRecommendationEngine` (motor de recomendaciones).
- [ ] En todos los handlers: aplicar `OwnerId` + `IDataScopeService.ApplyScope()`.
- [ ] Para ADMIN y DISPATCH_OFFICE: asegurar que `ApplyScope` solo filtra por `OwnerId`, sin restricción adicional por `LinkedEntityId`.

#### Paso 3 — Capa Web
- [ ] Registrar las 3 policies en `Program.cs`.
- [ ] Crear las 3 páginas Blazor en `Pages/Reporting/Ecomodulación/`.
- [ ] Cada página con `@attribute [Authorize(Policy = PolicyConstants.CanViewEcomodulation...)]`.
- [ ] Crear los 5 componentes reutilizables en `Pages/Reporting/Ecomodulación/Components/`.
- [ ] Añadir entradas en `NavMenu.razor` bajo la sección **Reporting → Ecomodulación** con consulta a `IPagePermissionService.CanAccessRouteAsync`.

#### Paso 4 — Auto-descubrimiento de pantallas
- [ ] Actualizar `PageDiscoveryService.InferModuleName()` en `Infrastructure/Services/PageDiscoveryService.cs` para añadir el nuevo caso:
  ```
  | `Ecomodulation` · `/reporting/ecomodulation/` | Ecomodulación |
  ```
- [ ] Actualizar `PageDiscoveryService.HumanizeName()` con las traducciones:
  - `ScrapOverview` → `"Ecomodulación — Visión SCRAP"`
  - `RegulatoryView` → `"Ecomodulación — Vista Regulatoria"`
  - `DppReadiness` → `"Ecomodulación — Preparación DPP"`

#### Paso 5 — Configuración post-despliegue
- [ ] Tras el primer arranque, las pantallas aparecerán en `/security/page-permissions` destacadas en amarillo.
- [ ] El administrador debe configurar los permisos según la tabla de la sección "Reglas de autorización y filtrado de datos".
- [ ] Verificar que cada usuario solo ve datos de las entidades asignadas o creadas por el mismo.
- [ ] Verificar que ADMIN y DISPATCH_OFFICE ven todos los datos del tenant.

---

### 📊 Actualización de la Matriz de Permisos (§8 del Mapa de Funcionalidades)

Añadir las siguientes filas a la tabla de la sección 8:

| Funcionalidad | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR | DISPATCH_OFFICE |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Ecomodulación — Visión SCRAP (UC5-A) | R | **R** | — | — | — | — | — | — | R |
| Ecomodulación — Vista Regulatoria (UC5-B) | R | — | — | — | — | — | **R** | **R** | — |
| Ecomodulación — Preparación DPP (UC5-C) | R | **R** | — | — | — | — | **R** | **R** | R |

---

### 📊 Actualización de la Matriz de Autorización por Pantalla (§4 del Mapa)

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Ecomod. Visión SCRAP** | `Residues`/`ProductSpecs`/`EcoModulationRules` | — | — | **R-P** | — | — | — | — | R | R |
| **Ecomod. Vista Regulatoria** | `Residues`/`ProductSpecs`/`Entities` | — | — | — | **R-P** | — | — | **R-P** | — | R |
| **Ecomod. Preparación DPP** | `Residues`/`ProductSpecs` | — | — | **R-P** | **R-P** | — | — | **R-P** | R | R |

---

### ⚙️ Parámetros configurables en `appsettings.json`

```json
{
  "Ecomodulation": {
    "CircularityIndex": {
      "WeightRecycledContentPercent": 30,
      "WeightReparabilityIndex": 25,
      "WeightDisassemblyEase": 20,
      "WeightNoHazardous": 15,
      "WeightPotentialLERCodes": 10
    },
    "DppRequiredFields": [
      "Name", "Reference", "IdLERCode", "ReparabilityIndex",
      "DisassemblyEase", "RecycledContentPercent", "CompositionJson",
      "ContainsHazardous", "PotentialLERCodesJson", "IdProducer"
    ],
    "Alerts": {
      "MinCoverageThreshold": 50,
      "MinCircularityThreshold": 40
    }
  }
}
```

---

### 📝 Notas de implementación importantes

1. **No hardcodear permisos de acceso a pantalla**: el acceso se gestiona con `PageDefinitions`/`PagePermissions` desde `/security/page-permissions`. Las `[Authorize(Policy = ...)]` en código solo actúan como suelo de seguridad.
2. **Cada usuario ve solo datos de SUS entidades**: el filtrado usa `ICurrentUserService.LinkedEntityId` + `IDataScopeService.ApplyScope()`. Excepción: ADMIN y DISPATCH_OFFICE ven todo el tenant.
3. **No se crean tablas nuevas en BD**: toda la lógica se apoya en las tablas existentes del modelo v4.1.
4. **Parseo de JSON**: los campos `CompositionJson`, `PotentialLERCodesJson`, `MaterialsJson` y `CriteriaJson` (de EcoModulationRules) se parsean en la capa Application, no en SQL.
5. **Consistencia visual**: usar ApexCharts, seguir los patrones de `MainLayout.razor` (modo oscuro/claro), responsive mobile-first.
6. **Exportación XLSX**: seguir el patrón de `ExportKpisToExcelQuery.cs` / `ExportMobilityDataToExcelQuery.cs` con ClosedXML. Nunca persistir ficheros en servidor.
