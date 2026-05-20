# 📚 Prompt para GitHub Copilot — Documentación Completa de GreenTransit

> **Objetivo**: Generar un documento Markdown exhaustivo que sirva como **documentación técnico-funcional** de toda la aplicación GreenTransit. Debe cubrir:
> 1. **Todas las entidades** del modelo de datos, su propósito, campos clave y cómo se integran entre sí.
> 2. **Todos los módulos funcionales**, explicando para qué sirve cada uno y cómo se relacionan.
> 3. **Todos los dashboards**, detallando cada gráfico/widget, qué información muestra, de dónde la obtiene y cómo se calculan los valores.
> 4. **Todos los valores calculados**, con sus fórmulas explícitas y las tablas/campos que intervienen.
>
> El formato de salida es un **único fichero `.md`** bien estructurado, navegable con tabla de contenidos.

---

## 📎 Archivos de contexto obligatorios

Adjunta estos archivos al inicio de la sesión de Copilot:
1. `README.md`
2. `Crear_BD_v4_1.sql`
3. `COPILOT_CONTEXT.md`
4. `Mapa_Funcionalidades.md`

---

## 🎯 Prompt principal

```
Necesito que generes un documento Markdown completo titulado
"Documentación Técnico-Funcional — GreenTransit" que sirva como referencia
exhaustiva de toda la aplicación. El documento debe ser autocontenido: cualquier
persona (desarrollador, consultor, auditor o stakeholder técnico) debe poder
entender qué hace la aplicación, cómo se estructura su modelo de datos,
qué calcula cada módulo y de dónde saca la información cada dashboard.

El fichero de salida debe llamarse: Documentacion_Tecnico_Funcional_GreenTransit.md

═══════════════════════════════════════════════════════════════════
ESTRUCTURA DEL DOCUMENTO
═══════════════════════════════════════════════════════════════════

Genera el documento con EXACTAMENTE esta estructura de secciones.
Dentro de cada sección, incluye todo el detalle que se pide.
No omitas ninguna sección ni subsección.

---

## SECCIÓN 1 — VISIÓN GENERAL DE LA APLICACIÓN

Describe:
- Qué es GreenTransit (plataforma multi-tenant para gestión integral de residuos
  industriales en España).
- Problema que resuelve (trazabilidad end-to-end, cumplimiento normativo
  Ley 22/2011, RAP, Objetivo 55 UE).
- Usuarios objetivo: lista de los 9 perfiles del sistema
  (ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT,
  COORDINATOR, DISPATCH_OFFICE) con una descripción de 1-2 líneas de cada uno.
- Stack tecnológico: .NET 10, Blazor Web App, EF Core, SQL Server Azure,
  MediatR, FluentValidation, Serilog, xUnit, ApexCharts, ClosedXML.
- Patrones arquitectónicos: Clean Architecture, CQRS, multi-tenant (OwnerId),
  autorización dinámica (PageDefinitions/PagePermissions).

---

## SECCIÓN 2 — CATÁLOGO COMPLETO DE ENTIDADES DEL MODELO DE DATOS

Para CADA tabla del modelo v4.1 (38+ tablas), genera una subsección con:

### 2.X — NombreTabla

- **Propósito**: descripción funcional de para qué sirve esta tabla (2-3 frases).
- **Dominio funcional**: a qué dominio pertenece (Contratos, Logística, Entradas,
  Maestros, Sostenibilidad, Producto/Ecodiseño, Seguridad, Geografía, Diccionarios).
- **Campos clave**: tabla con las columnas más importantes
  (nombre, tipo, nullable, FK si aplica, descripción).
  NO listes todos los campos; solo los que son funcionalmente relevantes
  (PKs, FKs, discriminadores, campos de negocio, campos calculados).
- **Discriminadores** (si los tiene): lista de valores válidos y qué significa cada uno.
  Ejemplo: Entities.EntityRole → Producer, Carrier, SCRAP, Plant, CAC, etc.
- **Relaciones**: lista de relaciones con otras tablas en formato:
  "NombreTabla (1)→(N) OtraTabla vía CampoFK" o
  "NombreTabla (N)→(1) OtraTabla vía CampoFK".
- **Consumidores principales**: qué módulos o dashboards usan esta tabla.
- **Reglas de negocio**: cualquier regla especial
  (ej: "OwnerId obligatorio", "nunca se elimina físicamente",
  "ResidueType discrimina entre Waste/Product/ProductSpec").

Agrupa las tablas por dominio funcional en este orden:
1. Maestros: Entities, LERCodes, Residues, TreatmentOperations
2. Contratos y liquidaciones: Agreements, AgreementDocuments, Settlements, SettlementLines
3. Operación logística: ServiceOrders, WasteMoves, WasteMoveResidues
4. Entradas y tratamiento: EntryPlants, EntryPlantResidues, TreatmentPlants,
   TreatmentPlantResidues, EntryCACs, EntryCACResidues
5. Zonas DUM: DUMZones, DUMRestrictionRules
6. Sostenibilidad y huella: EmissionFactorSets, EmissionFactors, PlantEnergies
7. Producto y ecodiseño: ProductDeclaration, Products, ProductSpecs,
   EcoModulationRuleSets, EcoModulationRules
8. Cuotas de mercado: MarketShares
9. Incidencias: Incidents
10. Seguridad: Users, Profiles, PageDefinitions, PagePermissions
11. Geografía: Country, TerritoryState, Province, Municipality,
    MunicipalityPopulation, MunicipalityZipCode
12. Diccionarios: dicProductDeclarationCategory, dicProductDeclarationPeriods,
    dicProductDeclarationProducts, dicProductDeclarationSource,
    dicProductDeclarationType, dicProductDeclarationUse, DocStates

---

## SECCIÓN 3 — MAPA DE RELACIONES ENTRE ENTIDADES

Genera una subsección por cada dominio funcional mostrando las cadenas de relaciones
en formato texto (no necesitamos diagrama, solo la cadena legible):

### 3.1 — Cadena de Contratos
Agreements (1)──(N) AgreementDocuments
Agreements (1)──(N) Settlements (1)──(N) SettlementLines → LERCodes
Agreements → Entities [IdScrap, IdPublicEntity, IdCoordinator]

### 3.2 — Cadena Logística
ServiceOrders → Entities [IdCarrier, IdPlannedPlant, IdIssuedBy, IdPickupPoint]
ServiceOrders → LERCodes
ServiceOrders (1)──(N) WasteMoves (1)──(N) WasteMoveResidues → Residues
WasteMoveResidues → TreatmentOperations, Entities [IdCarrier], EmissionFactorSets

### 3.3 — Cadena de Entradas y Tratamiento
ServiceOrders (1)──(N) EntryPlants (1)──(N) EntryPlantResidues → Residues
ServiceOrders (1)──(N) TreatmentPlants (1)──(N) TreatmentPlantResidues → Residues
TreatmentPlants → TreatmentOperations, Incidents

### 3.4 — Cadena de Maestros
Residues → LERCodes, Entities [IdProducer]

### 3.5 — Cadena de Producto y Ecodiseño
ProductDeclaration (1)──(N) Products → Residues
ProductSpecs → Residues, Entities [IdProducer]
EcoModulationRuleSets (1)──(N) EcoModulationRules

### 3.6 — Cadena de Sostenibilidad
EmissionFactorSets (1)──(N) EmissionFactors
PlantEnergies (independiente, vinculado lógicamente por PlantCenterCode)

### 3.7 — Cadena de Geografía
Country (1)──(N) TerritoryState (1)──(N) Province (1)──(N) Municipality

### 3.8 — Cadena de Seguridad
Profiles (1)──(N) Users → Country, TerritoryState, Municipality

Y después genera una tabla de dependencias entre módulos:

| Módulo origen | Depende de | Naturaleza |
|---|---|---|
| Flujo Operativo | Catálogos Maestros | Referencia de entidades, residuos, LER, tratamientos |
| Flujo Operativo | Contratación | Validación de acuerdo vigente al planificar |
| Liquidaciones | Flujo Operativo + Acuerdos | Pesos de EntryPlants + tarifas del acuerdo |
| KPIs Regulatorios | Flujo Operativo + MarketShares | Fracciones de tratamiento + objetivos |
| Huella de Carbono | Flujo Operativo + EmissionFactors | Distancias + factores kgCO₂e/km |
| Movilidad Urbana | Flujo Operativo + DUMZones | Horarios de recogida + restricciones DUM |
| Cumplimiento Normativo | Flujo Op. + Acuerdos + MarketShares | Tasas + cuotas + convenios |
| Declaraciones | Catálogo de Residuos | Productos del catálogo (ResidueType=Product) |
| Todos | Seguridad | OwnerId, perfiles, permisos por pantalla |

---

## SECCIÓN 4 — DESCRIPCIÓN FUNCIONAL DE MÓDULOS

Para CADA módulo funcional, genera una subsección con:

### 4.X — Nombre del Módulo

- **Propósito**: qué resuelve este módulo (3-5 frases).
- **Perfiles que lo usan**: lista de perfiles con nivel de acceso (lectura/escritura).
- **Entidades principales**: tablas del modelo que consume o modifica.
- **Flujo funcional**: paso a paso de cómo opera el módulo desde la perspectiva
  del usuario (sin entrar en código, pero mencionando entidades y estados).
- **Valores calculados**: si el módulo calcula algún valor derivado, documentar
  la fórmula exacta con los campos que intervienen.
- **Integraciones con otros módulos**: cómo se conecta con el resto de la aplicación
  (qué datos consume, qué datos produce para otros módulos).

Los módulos a documentar son:

### 4.1 — Catálogos Maestros
Cubre: Entities, LERCodes, Residues, TreatmentOperations.
Explicar el rol de cada catálogo como "fuente de verdad" que alimenta al resto.
Explicar los discriminadores EntityRole y ResidueType.

### 4.2 — Contratación y Economía
Cubre: Agreements, AgreementDocuments, Settlements, SettlementLines, MarketShares.
Explicar el flujo: Acuerdo Marco → Liquidación por periodo → Líneas de liquidación.
Fórmulas:
- ImporteBaseLiquidación = función de EntryPlants.NetWeight + TariffRulesJson del Agreement.
- AdjustmentsAmount = ajustes por eco-modulación (EcoModulationRules).
- TotalAmount = BaseAmount + AdjustmentsAmount + TaxAmount.
- % Cumplimiento MarketShares = SUM(EntryPlantResidues real) / MarketShares.Weight × 100.

### 4.3 — Flujo Operativo de Residuos (Core Logistics)
Cubre: ServiceOrders, WasteMoves, WasteMoveResidues.
Documentar la máquina de estados completa del traslado:
SOLICITADO → PLANIFICADO → RECOGIDO → EN_CAC (opcional) → EN_PLANTA → CLASIFICADO.
Para cada transición: qué campos se actualizan, qué validaciones se aplican.
Explicar la vista 360° del traslado.

### 4.4 — Entradas y Tratamiento
Cubre: EntryPlants, EntryPlantResidues, TreatmentPlants, TreatmentPlantResidues,
       EntryCACs, EntryCACResidues.
Documentar:
- Pesaje en planta: NetWeight = GrossWeight - TareWeight.
- Balance de masas: WeightReused + WeightValued + WeightRemove ≈ peso total entrada.
- Tasa de reciclaje (por tratamiento): WeightValued(IsRecycling) / WeightTotal.
- % Impropios: ImproperWeight / peso total entrada × 100.

### 4.5 — Sostenibilidad e Incidencias
Cubre: EmissionFactorSets, EmissionFactors, PlantEnergies, Incidents, DUMZones,
       DUMRestrictionRules.
Documentar:
- Cálculo de emisiones Scope 1 (transporte):
  kgCO₂e = TransportDistance × EmissionFactor.Value
  (donde EmissionFactor se selecciona por VehicleType + FuelType + EuroClass).
- Cálculo de emisiones Scope 2 (energía plantas):
  kgCO₂e = PlantEnergies.KwhTotal × factorConversion
  (factor configurable en appsettings.json, por defecto mix eléctrico español REE).
- Validación DUM: evaluación de ConditionsJson de DUMRestrictionRules vs horario
  planificado del traslado.
- Flujo de incidencias: apertura → asignación → resolución → cierre.

### 4.6 — Reporting y Trazabilidad
Cubre: Trazabilidad end-to-end, KPIs regulatorios, gestión documental.
Documentar:
- Trazabilidad: búsqueda por WasteMoveReference → timeline completa del traslado.
- KPIs regulatorios y sus fórmulas:
  · Tasa de reciclaje = Σ WeightValued (IsRecycling=1) / Σ WeightTotal.
  · Tasa de reutilización = Σ WeightReused (IsPreparationForReuse=1) / Σ WeightTotal.
  · Intensidad CO₂ = Σ TransportCarbonEmissions / (Σ Weight / 1000).
  · % Cumplimiento MarketShares = real / MarketShares.Weight × 100.
- Exportación XLSX con ClosedXML: patrón de 3 hojas (Resumen, Por Categoría,
  Histórico Trimestral).

### 4.7 — Declaraciones de Producción
Cubre: ProductDeclaration, Products, ProductSpecs, EcoModulationRuleSets,
       EcoModulationRules.
Documentar:
- Flujo: Borrador → Emitido → Validado/Rechazado.
- Importación masiva CSV/XLSX.
- Eco-modulación: cómo EcoModulationRules ajustan el importe de liquidaciones.

### 4.8 — Gestión de Usuarios y Seguridad
Cubre: Users, Profiles, PageDefinitions, PagePermissions.
Documentar:
- OpenID Connect → mapeo de claims → CurrentUserService.
- Sistema dinámico de permisos: PageDefinitions registra pantallas,
  PagePermissions asigna perfiles con nivel (Read/Write/Both).
- Regla fundamental: ADMIN y DISPATCH_OFFICE ven todos los datos del tenant;
  el resto filtra por LinkedEntityId.

---

## SECCIÓN 5 — FLUJO FUNCIONAL GLOBAL

Genera un diagrama textual del flujo end-to-end completo:

Orden de Servicio (ServiceOrder)
  ↓ [SCRAP/PUBLIC_ENT crea]
Traslado (WasteMove) — estado SOLICITADO
  ↓ [DISPATCH_OFFICE planifica → asigna transportista]
Estado PLANIFICADO
  ↓ [CARRIER ejecuta recogida → registra peso, distancia, emisiones]
Estado RECOGIDO
  ↓ (opcional) [CAC_OP registra entrada en centro de acopio]
Estado EN_CAC
  ↓ [PLANT_OP registra entrada y pesaje en planta]
Estado EN_PLANTA
  ↓ [PLANT_OP realiza clasificación y tratamiento → balance de masas]
Estado CLASIFICADO
  ↓ [Sistema genera datos para liquidación y reporting]
Liquidación (Settlement) → KPIs → Dashboards

Explicar:
- Qué entidades se crean/actualizan en cada paso.
- Qué campos cambian de valor.
- Qué cálculos se disparan automáticamente (emisiones, balance de masas).

---

## SECCIÓN 6 — DOCUMENTACIÓN DE DASHBOARDS

Esta es la sección MÁS IMPORTANTE del documento. Para CADA dashboard,
genera una subsección completa siguiendo esta plantilla:

### 6.X.Y — Nombre del Dashboard (ruta)

- **Ruta**: URL del dashboard (ej: `/logistics/optimization`).
- **Propósito**: qué resuelve o qué pregunta responde este dashboard (2-3 frases).
- **Perfiles con acceso**: lista de perfiles y cómo se filtra para cada uno.
- **Filtros disponibles**: lista de filtros que el usuario puede aplicar
  (Year, Month, IdScrap, ProvinceCode, etc.).

#### Widgets / Gráficos

Para CADA widget del dashboard, documenta:

| # | Widget | Tipo de gráfico | Fuente de datos (tablas + campos) | Cálculo / Fórmula | Descripción funcional |
|---|---|---|---|---|---|

Donde:
- **Tipo de gráfico**: card KPI, bar chart, line chart, donut, heatmap, tabla, treemap,
  progress bar, mapa interactivo, gauge, timeline, etc.
- **Fuente de datos**: tablas SQL exactas y campos que se consultan.
- **Cálculo / Fórmula**: la fórmula exacta. Ejemplos:
  · "AVG(WasteMoveResidues.TransportInfo_TransportDistance)"
  · "SUM(TransportCarbonEmissions) / SUM(Weight) * 1000"
  · "COUNT(WasteMoves WHERE ServiceStatus = 'RECOGIDO')"
  · "Σ WeightValued(IsRecycling=1) / Σ WeightTotal × 100"
- **Descripción funcional**: qué le dice este widget al usuario, qué decisión informa.

Los dashboards a documentar son (en este orden):

### 6.1 — Dashboards Logísticos
6.1.1 — Panel de Optimización Logística SCRAP (/logistics/optimization)
  Perfiles: SCRAP, COORDINATOR, ADMIN.
  Widgets: KPIs de ruta, volumen por zona, mapa interactivo, cumplimiento DUM,
  heatmap llegadas a planta, incidencias abiertas, utilización vehículos.

6.1.2 — Panel de Monitorización Pública (/logistics/public-monitoring)
  Perfiles: PUBLIC_ENT, ADMIN.
  Widgets: servicios por SCRAP, histórico mensual, liquidaciones, emisiones CO₂e,
  objetivos municipales.

6.1.3 — Panel Operativo (/logistics/operations)
  Perfiles: DISPATCH_OFFICE, CAC_OP, PLANT_OP, ADMIN.
  Widgets adaptados por perfil: SO pendientes, embudo traslados, planificación semanal,
  entradas CAC/planta, balance tratamiento, impropios, incidencias.

### 6.2 — Dashboards de Movilidad Urbana (UC3)
6.2.1 — Panel Coordinador (/mobility/coordinator-analysis)
  Perfiles: COORDINATOR, ADMIN.
  Widgets: mapa calor recogidas por hora, índice de conflicto, cumplimiento DUM,
  distribución por tipo vehículo, recomendaciones.
  Fórmulas del índice de conflicto:
  40% × % recogidas en hora pico + 30% × % fuera DUM + 20% × incidencias + 10% × volumen relativo.
  Hora pico configurable: por defecto 07:30-09:30 y 17:30-19:30.

6.2.2 — Panel Monitorización Municipal (/mobility/municipal-monitoring)
  Perfiles: PUBLIC_ENT, ADMIN.
  Widgets: calendario recogidas planificadas, impacto por zona, ranking SCRAP por conflicto.

6.2.3 — Panel Datos Oficina Asignación (/mobility/dispatch-data)
  Perfiles: DISPATCH_OFFICE, ADMIN.
  Widgets: dataset exportable, resumen por SCRAP, planificación semanal con conflicto.

### 6.3 — Dashboards de Mapas de Calor
6.3.1 — Mapa de Densidad de Residuos (HM-A) (/reporting/heat-maps/waste-density)
  Perfiles: SCRAP, COORDINATOR, PUBLIC_ENT, DISPATCH_OFFICE, ADMIN.
  Widgets: mapa calor georreferenciado, tabla frecuencia por punto,
  donut tipología residuos (LER), evolución temporal, alertas acumulación.
  Fórmula densidad: SUM(WasteMoveResidues.Weight) por área geográfica
  sobre coordenadas de Entities (Latitude, Longitude).

6.3.2 — Patrones Estacionales (HM-B) (/reporting/heat-maps/seasonal-patterns)
  Perfiles: SCRAP, COORDINATOR, DISPATCH_OFFICE, ADMIN.
  Widgets: heatmap mensual (mes × año), comparativa interanual, predicción estacional.

6.3.3 — Análisis por Tipología (HM-C) (/reporting/heat-maps/waste-typology)
  Perfiles: SCRAP, COORDINATOR, DISPATCH_OFFICE, ADMIN.
  Widgets: treemap por código LER, evolución por categoría, ranking zonas por tipo.

### 6.4 — Dashboards de Huella de Carbono
6.4.1 — HC-A: Panel SCRAP (/reporting/carbon-footprint/scrap-overview)
  Perfiles: SCRAP, COORDINATOR, DISPATCH_OFFICE, ADMIN.
  Widgets: emisiones totales Scope 1 (transporte), intensidad CO₂/tonelada,
  desglose por transportista, por tipo vehículo, por combustible,
  evolución mensual, ranking rutas por emisiones, mapa de emisiones.
  Fórmulas:
  - Emisiones Scope 1 = SUM(WasteMoveResidues.TransportInfo_TransportCarbonEmissions).
  - Intensidad = Σ TransportCarbonEmissions / (Σ Weight / 1000) [kgCO₂e/tonelada].
  - Promedio por recogida = AVG(TransportInfo_TransportCarbonEmissions).
  - Variación % = (actual - anterior) / anterior × 100.

6.4.2 — HC-B: Panel Transportista (/reporting/carbon-footprint/carrier-analysis)
  Perfiles: CARRIER, SCRAP, DISPATCH_OFFICE, ADMIN.
  Widgets: emisiones por vehículo, eficiencia por ruta, factor de carga,
  comparativa entre transportistas, evolución mensual.
  Fórmulas:
  - Eficiencia = kgCO₂e / (km × tonelada).
  - Factor de carga = peso transportado / capacidad máxima vehículo.

6.4.3 — HC-C: Panel Energía de Planta (/reporting/carbon-footprint/plant-energy)
  Perfiles: PLANT_OP, DISPATCH_OFFICE, ADMIN.
  Widgets: consumo eléctrico mensual, emisiones Scope 2, desglose por fuente,
  intensidad energética por tonelada tratada, evolución interanual.
  Fórmulas:
  - Emisiones Scope 2 = PlantEnergies.KwhTotal × factorConversionKwhCO2e
    (configurable en appsettings.json, valor por defecto: mix eléctrico español REE).
  - Intensidad energética = KwhTotal / SUM(EntryPlants.NetWeight / 1000)
    [kWh/tonelada].

6.4.4 — HC-D: Panel Productor (/reporting/carbon-footprint/producer-view)
  Perfiles: PRODUCER, ADMIN.
  Widgets: emisiones generadas por transporte de sus residuos, intensidad por tonelada,
  desglose por tipo residuo, evolución mensual.

6.4.5 — HC-E: Panel Entidad Pública (/reporting/carbon-footprint/public-view)
  Perfiles: PUBLIC_ENT, DISPATCH_OFFICE, ADMIN.
  Widgets: emisiones por SCRAP en su municipio, desglose geográfico, evolución temporal.

### 6.5 — Dashboards de Análisis y Cumplimiento Normativo (RAP)
6.5.1 — CN-A: Panel SCRAP (/reporting/regulatory-compliance/scrap-overview)
  Perfiles: SCRAP, ADMIN.
  Widgets: tasa de reciclaje actual vs objetivo, tasa de valorización,
  cumplimiento de cuotas (MarketShares), liquidaciones del periodo,
  alertas de incumplimiento, evolución trimestral.
  Fórmulas:
  - Tasa reciclaje = Σ TreatmentPlantResidues.WeightValued (donde
    TreatmentOperations.IsRecycling = 1) / Σ peso total entrada × 100.
  - Tasa reutilización = Σ WeightReused (IsPreparationForReuse=1) / Σ peso total × 100.
  - Tasa valorización = Σ (WeightValued + WeightReused) / Σ peso total × 100.
  - % Cumplimiento cuota = SUM(EntryPlantResidues real) / MarketShares.Weight × 100.
  - Desviación = % real - % objetivo.

6.5.2 — CN-B: Auditoría de Cuotas (/reporting/regulatory-compliance/market-share-audit)
  Perfiles: COORDINATOR, DISPATCH_OFFICE, ADMIN.
  Widgets: proporcionalidad real vs objetivo por SCRAP, ranking SCRAPs,
  desglose por categoría y CCAA, alertas de riesgo.
  Fórmula proporcionalidad = (cuota real SCRAP_i / cuota total real)
  vs (cuota objetivo SCRAP_i / cuota total objetivo).

6.5.3 — CN-C: Monitorización Convenios (/reporting/regulatory-compliance/agreement-monitoring)
  Perfiles: COORDINATOR, DISPATCH_OFFICE, ADMIN.
  Widgets: estado convenios, timeline vigencia, servicios por convenio,
  liquidaciones por convenio, alertas de vencimiento.
  Alerta vencimiento: días hasta EffectiveTo < umbral configurable
  (warning = 90 días, critical = 30 días).

6.5.4 — CN-D: Vista Entidad Pública (/reporting/regulatory-compliance/public-view)
  Perfiles: PUBLIC_ENT, ADMIN.
  Widgets: cumplimiento de SCRAPs en su municipio, liquidaciones recibidas,
  incidencias de cumplimiento, comparativa con medias regionales.

6.5.5 — CN-E: Datos Oficina Asignación (/reporting/regulatory-compliance/dispatch-data)
  Perfiles: DISPATCH_OFFICE, ADMIN.
  Widgets: dashboard ejecutivo (cards KPI globales), ranking SCRAPs por cumplimiento,
  tabla exportable para auditoría (XLSX), evolución interanual, mapa calor cumplimiento.
  Fórmulas de las cards ejecutivas:
  - Tasa reciclaje global tenant = Σ WeightValued(IsRecycling) / Σ WeightTotal.
  - Nº SCRAPs activos = COUNT(DISTINCT IdScrap en WasteMoves del periodo).
  - Nº convenios activos = COUNT(Agreements WHERE Status='Active').
  - Importe total liquidado = SUM(Settlements.TotalAmount WHERE Status='Approved').

### 6.6 — Dashboard de Declaraciones de Producción
6.6.1 — KPIs de Declaraciones (/product-declarations/dashboard)
  Perfiles: ADMIN, PRODUCER, SCRAP.
  Widgets: declaraciones por estado (donut), volumen por periodo (bar chart),
  top 10 productos declarados, productores sin declaración, importe total.
  Fórmulas:
  - Volumen por periodo = Σ Products.Quantity GROUP BY Year + Period.
  - Top 10 = ranking por Σ Products.Quantity → resolver nombre vía Residues.Name.
  - Productores sin declaración = Entities WHERE EntityRole='Producer'
    AND NOT EXISTS (ProductDeclaration en el periodo actual).
  - Importe total = Σ ProductDeclaration.Amount del periodo filtrado.

### 6.7 — Dashboard Principal (Home)
6.7.1 — Dashboard Principal (/)
  Perfiles: todos (adaptado por perfil).
  Widgets: cards KPI adaptadas al rol, accesos directos, notificaciones.

---

## SECCIÓN 7 — CATÁLOGO DE VALORES CALCULADOS Y FÓRMULAS

Genera una tabla resumen consolidada de TODOS los valores calculados
en la aplicación, con formato:

| # | Valor calculado | Fórmula | Tablas/Campos que intervienen | Dónde se usa (dashboard/módulo) |
|---|---|---|---|---|

Incluye al menos estos (y todos los demás que encuentres en el Mapa de Funcionalidades):

1. Tasa de reciclaje
2. Tasa de reutilización
3. Tasa de valorización
4. % Cumplimiento cuota de mercado (MarketShares)
5. Intensidad CO₂ (kgCO₂e/tonelada)
6. Emisiones Scope 1 (transporte)
7. Emisiones Scope 2 (energía planta)
8. Promedio km por recogida
9. Promedio kgCO₂e por recogida
10. kgCO₂e por tonelada transportada
11. % Variación periodo anterior
12. Índice de conflicto de movilidad
13. % Cumplimiento DUM
14. Hora pico (definición y cálculo)
15. Balance de masas (WeightReused + WeightValued + WeightRemove)
16. % Impropios
17. Intensidad energética (kWh/tonelada tratada)
18. Factor de carga vehicular
19. Proporcionalidad de cuotas entre SCRAPs
20. NetWeight (GrossWeight - TareWeight)
21. ImporteBase liquidación
22. AdjustmentsAmount (eco-modulación)
23. TotalAmount liquidación
24. Desviación cumplimiento (% real - % objetivo)

---

## SECCIÓN 8 — REGLAS TRANSVERSALES DE FILTRADO DE DATOS

Documenta las reglas de filtrado que aplican a TODOS los módulos y dashboards:

### 8.1 — Multi-tenancy
- Toda query operativa filtra por OwnerId = @currentUserOwnerId.
- Catálogos compartidos (LERCodes, TreatmentOperations, tablas geográficas,
  Profiles) NO filtran por OwnerId.

### 8.2 — Filtrado por perfil
Para cada perfil, documenta el patrón general de filtrado:
- SCRAP: WasteMoves WHERE (IdScrap = LinkedEntityId OR IdScrap2 = LinkedEntityId).
- COORDINATOR: scope derivado de Agreements WHERE IdCoordinator = LinkedEntityId.
- PUBLIC_ENT: MunicipalityCode de la entidad vinculada,
  O ServiceOrders.IdIssuedBy = LinkedEntityId.
- PLANT_OP: solo datos de su planta (EntryPlants/TreatmentPlants de su entidad).
- CAC_OP: solo datos de su CAC (EntryCACs de su entidad).
- CARRIER: WasteMoveResidues WHERE IdCarrier = LinkedEntityId.
- PRODUCER: ServiceOrders WHERE IdIssuedBy = LinkedEntityId.
- DISPATCH_OFFICE: todo el tenant (solo OwnerId).
- ADMIN: todo el tenant (solo OwnerId).

### 8.3 — Regla geográfica
ProvinceCode → siempre mostrar como Province.Name (JOIN).
MunicipalityCode → siempre mostrar como Municipality.Name (JOIN).
Nunca mostrar códigos, siempre nombres. Aplica a tablas, filtros, selectores y exports XLSX.

### 8.4 — Autorización dinámica
PageDefinitions registra cada pantalla con su ruta y módulo.
PagePermissions asigna perfiles a pantallas con nivel Read/Write/Both.
Se configura desde /security/page-permissions. Nunca hardcodeado en código.

---

## SECCIÓN 9 — CONFIGURACIÓN DE UMBRALES Y PARÁMETROS

Documenta todos los valores configurables en appsettings.json que afectan
a cálculos y dashboards:

- CarbonFootprint.Scope2ConversionFactor (kWh → kgCO₂e, mix eléctrico español).
- Mobility.PeakHourRanges (franjas hora pico: 07:30-09:30, 17:30-19:30).
- Mobility.ConflictIndexWeights (40% hora pico, 30% DUM, 20% incidencias, 10% volumen).
- RegulatoryCompliance.Alerts.MarketShareRiskThresholdPercent (80%).
- RegulatoryCompliance.Alerts.AgreementExpiryWarningDays (90).
- RegulatoryCompliance.Alerts.AgreementExpiryCriticalDays (30).
- RegulatoryCompliance.Alerts.MinServicesThresholdPercent (70%).
- RegulatoryCompliance.Defaults.DefaultMinRecyclingPercent (55%).
- RegulatoryCompliance.Defaults.DefaultMinReusePercent (5%).
- RegulatoryCompliance.Defaults.DefaultMinValorizationPercent (65%).
- HeatMaps.AccumulationAlertThresholds (umbrales de alertas de acumulación).

---

## REGLAS DE FORMATO DEL DOCUMENTO

1. Usar encabezados Markdown jerárquicos (# ## ### ####) para toda la estructura.
2. Incluir una tabla de contenidos (TOC) al principio del documento con enlaces
   a cada sección y subsección.
3. Usar tablas Markdown para campos de entidades, widgets de dashboards y fórmulas.
4. Usar bloques de código para fórmulas SQL cuando sea necesario.
5. Usar emojis como prefijos de sección para facilitar la navegación visual:
   📋 para catálogos, 🔗 para relaciones, 📊 para dashboards, 🧮 para fórmulas,
   🔒 para seguridad, ⚙️ para configuración.
6. El documento debe ser completo y autocontenido. No debe hacer referencia a
   "ver archivo X" ni "consultar el SQL". Todo debe estar en el propio .md.
7. Longitud esperada: 2.000–4.000 líneas. No recortar. Si Copilot se queda corto,
   indicarle: "Continúa la documentación desde la sección X.Y".

═══════════════════════════════════════════════════════════════════
FIN DEL PROMPT
═══════════════════════════════════════════════════════════════════
```

---

## 📝 Notas para la sesión de Copilot

- **Este prompt es largo.** Si Copilot se queda corto o trunca la salida, divídelo en fases:
  - **Fase 1**: Secciones 1–3 (Visión general + Entidades + Relaciones)
  - **Fase 2**: Sección 4 (Módulos funcionales)
  - **Fase 3**: Sección 5 + Sección 6.1–6.3 (Flujo global + Dashboards Logística, Movilidad y Mapas de Calor)
  - **Fase 4**: Sección 6.4–6.7 (Dashboards Huella de Carbono, Cumplimiento, Declaraciones, Home)
  - **Fase 5**: Secciones 7–9 (Fórmulas, Filtrado, Configuración)

- Tras cada fase, pídele: *"Continúa la documentación desde la sección X.Y donde te quedaste. No repitas lo que ya has generado."*

- Si Copilot genera contenido demasiado genérico o inventa datos, recuérdale:
  *"Basa toda la documentación EXCLUSIVAMENTE en los archivos adjuntos (Mapa_Funcionalidades.md, Crear_BD_v4_1.sql, COPILOT_CONTEXT.md, README.md). No inventes módulos, tablas o campos que no existan en estos archivos."*

- Al finalizar todas las fases, solicita una revisión de completitud:
  *"Revisa el documento completo y verifica que: (1) Cada tabla del SQL tiene su sección en la Sección 2, (2) Cada dashboard del Mapa de Funcionalidades tiene su sección en la Sección 6 con todos sus widgets documentados, (3) Cada fórmula mencionada en las secciones 4 y 6 aparece en la tabla consolidada de la Sección 7. Si falta algo, complétalo."*
