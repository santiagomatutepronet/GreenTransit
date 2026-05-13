# 🌱 Prompt para Copilot — Refactorización de Seed Data de Demo (Sandbox)

> **Objetivo**: Modificar la funcionalidad de carga de datos sandbox/demo del `DbInitializer` para que **todos los dashboards y pantallas de reporting se vean completados con datos coherentes, variados y visualmente representativos** en una demostración de la aplicación.

---

## 📎 Archivos de contexto a adjuntar

Adjunta estos archivos al inicio de la sesión de Copilot:
1. `README.md`
2. `Crear_BD_v4_1.sql`
3. `COPILOT_CONTEXT.md`
4. `Mapa_Funcionalidades.md`

---

## 🎯 Prompt

```
Necesito que refactorices y amplíes la funcionalidad de carga de datos sandbox/demo
(DbInitializer o servicio equivalente de seed) para que TODOS los dashboards y
pantallas de reporting de la aplicación GreenTransit se vean completados con datos
coherentes, variados y visualmente representativos para una demo.

El seed de datos de demo debe ejecutarse condicionalmente (solo en entorno
Development/Sandbox) y ser idempotente (si ya existen datos de demo, no duplicar).

A continuación detallo EXACTAMENTE qué datos necesito y con qué características
para que cada dashboard funcione correctamente.

═══════════════════════════════════════════════════════════
1. ENTIDADES (Entities) — Ecosistema completo de actores
═══════════════════════════════════════════════════════════

Crear al menos las siguientes entidades con datos realistas españoles
(nombres, NIFs, direcciones, coordenadas GPS reales de ciudades españolas):

- 20 Productores (EntityRole = "Producer") en distintas provincias
  → Con Latitude/Longitude reales (ej: Madrid, Barcelona, Zaragoza)
- 8 Transportistas (EntityRole = "Carrier") con InscriptionNumber
  → Con VehicleType variado
- 8 SCRAPs (EntityRole = "SCRAP")
- 6 Plantas de tratamiento (EntityRole = "Plant") con Latitude/Longitude
- 6 Centro de Acopio CAC (EntityRole = "CAC") con Latitude/Longitude
- 10 Entidades Públicas / Ayuntamientos (EntityRole = "PublicEntity")
  → Con MunicipalityCode real
- 1 Coordinador (EntityRole = "Coordinator")
- 1 Oficina de asignación (EntityRole = "Dispatch_office")
- 3 Operador de transferencia (EntityRole = "OperatorTransfer")

Todas con OwnerId compartido (mismo tenant de demo), IsActive = true,
ProvinceCode y MunicipalityCode coherentes con la localización.

═══════════════════════════════════════════════════════════
2. USUARIOS (Users) — Uno por cada perfil del sistema
═══════════════════════════════════════════════════════════

Crear un usuario vinculado a cada entidad con el perfil correspondiente
según el mapeo EntityRole → Profiles.Reference, dejar el usuario ADMIN sin tocar ni al crear ni al limpiar datos

Todos con IsActive = true y OwnerId del tenant demo.

═══════════════════════════════════════════════════════════
3. CATÁLOGOS (ya deben existir, pero verificar completitud)
═══════════════════════════════════════════════════════════

- LERCodes: al menos 20-30 códigos variados, incluyendo:
  → Al menos 10 con IsDangerous = true
  → Al menos 10 con IsRAEE = true
  → De distintos capítulos/subcapítulos
- TreatmentOperations: todas las R1-R13 y D1-D15 con los flags:
  → IsRecycling, IsEnergyRecovery, IsPreparationForReuse correctos
- EmissionFactorSets: 3 set activo (Status = "Active", ValidFrom = hace 1 año)
  con EmissionFactors para las combinaciones:
  → VehicleType: "Furgoneta", "Camión 3.5t", "Camión 12t"
  → FuelType: "Diesel", "GNC", "Eléctrico"
  → EuroClass: "Euro5", "Euro6"
  → Values realistas (ej: Diesel Euro5 = 0.27 kgCO2e/km, Eléctrico = 0.05)
- Geografía: todas las CCAA, provincias y municipios de España - importante no crearlas en el sandbox ni eliminarlas al limpiarlas
- Diccionarios de declaración: dicProductDeclarationCategory,
  dicProductDeclarationPeriods, dicProductDeclarationProducts,
  dicProductDeclarationSource, dicProductDeclarationType,
  dicProductDeclarationUse — importante no crearlas en el sandbox ni eliminarlas al limpiarlas 
- DocStates: Borrador, Emitido, Validado, Rechazado

═══════════════════════════════════════════════════════════
4. RESIDUOS (Residues) — Catálogo variado
═══════════════════════════════════════════════════════════

Crear al menos 12 residuos distribuidos:

- 15 con ResidueType = "Waste" (residuos operativos):
  → Mezcla de peligrosos y no peligrosos
  → Al menos 5 RAEE (ej: pantallas, frigoríficos, pequeños aparatos)
  → Con ProductCategory variadas
  → Con WeightPerUnitKg realistas
- 5 con ResidueType = "Product" (productos de productor):
  → Vinculados a distintos productores (IdProducer)
  → Con ProductCategory y ProductUse variados
- 5 con ResidueType = "ProductSpec" (fichas ecodiseño):
  → Con ReparabilityIndex, RecycledContentPercent informados
  → Con CompositionJson y MaterialsJson como JSON de ejemplo

═══════════════════════════════════════════════════════════
5. ACUERDOS (Agreements) — Base para liquidaciones y filtros
═══════════════════════════════════════════════════════════

Crear al menos 10 acuerdos:

- Acuerdo 1: SCRAP-1 ↔ Ayuntamiento-1 ↔ Coordinador
  → Status = "Active", vigencia: hace 6 meses → dentro de 6 meses
  → WasteStream = "RAEE", AutonomousCommunity coherente
  → TariffRulesJson con ejemplo realista de tarifa por kg
- Acuerdo 2: SCRAP-1 ↔ Ayuntamiento-2
  → Status = "Active", WasteStream = "RAEE"
- Acuerdo 3: SCRAP-2 ↔ Ayuntamiento-1
  → Status = "Active", WasteStream distinto (ej: "ENV" envases)

  Y asi sucesivamente

Cada uno con al menos 1 AgreementDocument de tipo "Contrato"
con DocumentHash generado (SHA-256 de un string fijo).

═══════════════════════════════════════════════════════════
6. CUOTAS DE MERCADO (MarketShares) — Para widget de cumplimiento
═══════════════════════════════════════════════════════════

Crear al menos 8 registros de MarketShares para el año actual:

- SCRAP-1, categoría "Grandes aparatos", CCAA-1, Weight = 50000 kg
- SCRAP-1, categoría "Pantallas", CCAA-1, Weight = 20000 kg
- SCRAP-2, categoría "Grandes aparatos", CCAA-1, Weight = 30000 kg
- SCRAP-1, categoría "Pequeños aparatos", CCAA-2, Weight = 15000 kg

Y asi sucesivamente

Con FlowType = "RAEE" y Period coherente (trimestral o anual).

═══════════════════════════════════════════════════════════
7. ÓRDENES DE SERVICIO (ServiceOrders) — VARIEDAD DE ESTADOS
═══════════════════════════════════════════════════════════

Crear al menos 100 órdenes de servicio distribuidas así:

ESTADOS:
- 10 en estado "Pending" (sin traslado vinculado)
  → PlannedPickupStart en los PRÓXIMOS 7 días (importante para el widget
    de planificación semanal del Dashboard 3 operativo)
  → Horas variadas: algunas entre 07:30-09:30, otras entre 10:00-16:00,
    otras entre 17:30-19:30 (para el semáforo de hora punta de movilidad)
- 20 en estado "Scheduled"
  → PlannedPickupStart en los próximos 3-10 días
- 20 en estado "InProgress" (con traslado vinculado)
- 50 en estado "Completed"
- 2 en estado "Cancelled"

VARIEDAD TEMPORAL:
- Las completadas deben distribuirse a lo largo de los ÚLTIMOS 12 MESES
  (no todas el mismo día) para que las series temporales mensuales tengan datos.
- Usar distintas horas del día (mañana, mediodía, tarde, noche)
  para que los heatmaps 7×24 tengan dispersión.
- Algunas los lunes, otras martes, miércoles, etc.

VARIEDAD DE DATOS:
- IdIssuedBy: distribuir entre los 3 productores y las 2 entidades públicas
- IdPickupPoint: variar (distintas entidades con coordenadas)
- IdCarrier: alternar entre los 2 transportistas
- IdLERCode: mezclar códigos RAEE y no RAEE
- WasteStream: mayoritariamente "RAEE" pero algunas con otros flujos
- EstimatedWeight: valores variados (50-5000 kg)
- IdPlannedPlant: distribuir entre las 2 plantas

Cada SO con al menos 1-2 ServiceOrderResidues con datos coherentes.

═══════════════════════════════════════════════════════════
8. TRASLADOS (WasteMoves + WasteMoveResidues) — MÁQUINA DE ESTADOS
═══════════════════════════════════════════════════════════

Crear al menos 100 traslados con distribución por estado
(campo ServiceStatus):

- 10 en "SOLICITADO"
- 20 en "PLANIFICADO"
- 20 en "RECOGIDO"
- 10 en "EN CAC"
- 10 en "EN PLANTA"
- 50 en "CLASIFICADO" (ciclo completo)

IMPORTANTE — DATOS DE INSTANCIA EN WasteMoveResidues:
- Weight: valores variados (30-4000 kg) — NO todos iguales
- TransportInfo_TransportDistance: entre 15 y 350 km (variado)
- TransportInfo_TransportDuration: coherente con distancia (ej: 30-300 min)
- TransportInfo_TransportCarbonEmissions: calculado como
  Distance × EmissionFactor.Value (usar los factores del seed)
- EmissionFactorSetId y EmissionFactorVersion: vinculados al set activo
- VehicleType y FuelType: variados ("Furgoneta", "Camión 3.5t", etc.)
- EuroClass: variado ("Euro5", "Euro6")
- IdCarrier: distribuir entre los 2 transportistas
- IdTreatmentOperationDestiny: distintas operaciones R/D
- NTNumber y DINumber: generar para los que están en RECOGIDO o posterior
  (formato realista: NT-2025-XXXX, DI-2025-XXXX)
- DIPhase: valores "E1", "E2", "E3" variados

VINCULACIÓN:
- Cada WasteMove debe tener ServiceOrderId vinculado a una SO
- IdSource: uno de los productores/CAC
- IdDestination: una de las plantas
- IdScrap: uno de los SCRAPs (alternar)
- Algunos con IdScrap2 para doble SCRAP

TEMPORALIDAD:
- RequestDate y fechas planificadas/reales distribuidas a lo largo
  de los últimos 12 meses para que las series mensuales funcionen
- ActualPickupStart/End: con horas variadas del día
  → Al menos un 30% en franjas de hora punta (07:30-09:30 y 17:30-19:30)
    para que el % de hora punta sea visible y realista
  → El 70% restante fuera de hora punta

═══════════════════════════════════════════════════════════
9. ENTRADAS EN CAC (EntryCACs + EntryCACResidues)
═══════════════════════════════════════════════════════════

Crear al menos 10 entradas en CAC para los traslados en estado "EN CAC"
y superiores que pasaron por CAC:

- CACEntryDate: variada, coherente con la fecha del traslado
- TypeContainer: variado ("Bigbag", "Contenedor", "Palé")
- EntryCACResidues con Weight coherente con WasteMoveResidues

═══════════════════════════════════════════════════════════
10. ENTRADAS EN PLANTA (EntryPlants + EntryPlantResidues)
═══════════════════════════════════════════════════════════

Crear entradas en planta para TODOS los traslados en estado
"EN PLANTA" o "CLASIFICADO" (al menos 20):

- PlantEntryDate: distribuidas a lo largo de los últimos 12 meses
  → Distintos días de la semana y horas del día
    (CRÍTICO para el heatmap de llegadas 7×24 del Dashboard 1)
  → Algunas entre 06:00-08:00, otras 10:00-14:00, otras 15:00-18:00
- GrossWeight, TareWeight, NetWeight: coherentes (NetWeight = Gross - Tara)
  → Variación respecto a WasteMoveResidues.Weight (±5-15%) para realismo
- TicketScale: generar códigos únicos (ej: "BS-2025-0001")
- ServiceOrderId: vinculado a la SO correspondiente

EntryPlantResidues con Weight coherente.

═══════════════════════════════════════════════════════════
11. TRATAMIENTO EN PLANTA (TreatmentPlants + TreatmentPlantResidues)
═══════════════════════════════════════════════════════════

Crear tratamientos para TODOS los traslados en estado "CLASIFICADO" :

- PlantTreatmentDate: posterior a PlantEntryDate (1-3 días después)
- IdTreatmentOperation: variar entre R3, R4, R5, R12, D10
  (al menos 1 de reciclaje, 1 de valorización energética, 1 de eliminación)

TreatmentPlantResidues — BALANCE DE MASAS coherente:
- WeightTotal: coherente con EntryPlantResidues
- WeightReused: entre 10-40% del total
- WeightValued: entre 30-60% del total
- WeightRemove (rechazo): entre 5-20% del total
- Que WeightReused + WeightValued + WeightRemove ≈ WeightTotal (±1%)
- IdResidueReused, IdResidueValued, IdResidueRemove: apuntar a
  residuos del catálogo (pueden ser distintos al de entrada)
- ImproperWeight: pequeño en la mayoría (0-50 kg), más alto en 1-2 casos

═══════════════════════════════════════════════════════════
12. LIQUIDACIONES (Settlements + SettlementLines)
═══════════════════════════════════════════════════════════

Crear al menos 10 liquidaciones para alimentar el Dashboard 2 (Público):

- Settlement 1: SCRAP-1, Ayuntamiento-1, Acuerdo-1, año actual, mes -3
  → Status = "Approved", TotalAmount realista (ej: 12500 EUR)
- Settlement 2: SCRAP-1, Ayuntamiento-1, Acuerdo-1, año actual, mes -2
  → Status = "Approved"
- Settlement 3: SCRAP-1, Ayuntamiento-2, Acuerdo-2, año actual, mes -1
  → Status = "Pending"
- Settlement 4: SCRAP-2, Ayuntamiento-1, Acuerdo-3, año actual, mes -2
  → Status = "Approved"

  Y asi sucesivamente

SettlementLines (2-3 por liquidación):
- ProductCategory y IdLERCode variados
- WeightKg coherente con EntryPlants del periodo
- PricePerKg y Amount calculados

═══════════════════════════════════════════════════════════
13. INCIDENCIAS (Incidents) — Para widgets de semáforo
═══════════════════════════════════════════════════════════

Crear al menos 25 incidencias:

- 10 ABIERTAS (ClosedAt = NULL):
  → 2 con Severity = "Critical"
  → 2 con Severity = "High"
  → 6 con Severity = "Medium"
  → Type variados: "DescuadrePeso", "Retraso", "AveriaVehiculo",
    "ResiduoNoConforme"
  → OpenedAt: en los últimos 30 días
  → Vinculadas a traslados de distintos SCRAPs/municipios

- 4 CERRADAS:
  → Severity variada
  → OpenedAt hace 1-3 meses, ClosedAt 1-7 días después
  → Con ResolutionJson de ejemplo

═══════════════════════════════════════════════════════════
14. ZONAS DUM (DUMZones + DUMRestrictionRules)
═══════════════════════════════════════════════════════════

Crear al menos 10 zonas DUM:

- Zona 1: centro de Madrid, Status = "Active"
  → GeometryJson: polígono GeoJSON simplificado del centro
  → Con DUMRestrictionRule:
    ActionType = "Restrict", ConditionsJson con horarios
    (permitido 06:00-08:00 y 20:00-23:00), ValidFrom/To vigentes
- Zona 2: centro de Barcelona, Status = "Active"
  → Con DUMRestrictionRule ActionType = "Notify"

Y asi sucesivamente

═══════════════════════════════════════════════════════════
15. ENERGÍA DE PLANTAS (PlantEnergies)
═══════════════════════════════════════════════════════════

Crear registros mensuales de los últimos 6 meses para cada planta:

- KwhTotal: valores realistas (15000-45000 kWh/mes) con variación estacional
- Source: "Red eléctrica"
- PlantName y PlantCenterCode coherentes con las entidades Plant

═══════════════════════════════════════════════════════════
16. DECLARACIONES DE PRODUCCIÓN (ProductDeclaration + Products)
═══════════════════════════════════════════════════════════

Crear al menos 30 declaraciones:

- x en estado "BORRADOR" (del productor 1, trimestres recientes)
- x en estado "EMITIDO" (del productor 2)
- x en estado "VALIDADO" (del productor 1, trimestre anterior)
- x en estado "RECHAZADO" (del productor 3)

y asi sucesivamente

Cada una con 2-4 líneas de Products:
- IdResidue apuntando a residuos de tipo Product del catálogo
- Quantity variada (100-10000 unidades)
- Price realista
- Source y MeasureUnit informados

═══════════════════════════════════════════════════════════
17. ECO-MODULACIÓN (EcoModulationRuleSets + EcoModulationRules)
═══════════════════════════════════════════════════════════

Crear 2 RuleSet activo con 2-3 reglas de ejemplo:
- CriteriaJson con condiciones de ejemplo
- ImpactPercent variado (-10%, +5%, -15%)

═══════════════════════════════════════════════════════════
18. REGULATORY TARGETS (RegulatoryTargets)
═══════════════════════════════════════════════════════════

Crear al menos 3 registros para el año actual:
- Categoría "Grandes aparatos": MinRecyclingPercent = 55, MinReusePercent = 5
- Categoría "Pantallas": MinRecyclingPercent = 65, MinReusePercent = 5
- Categoría "Pequeños aparatos": MinRecyclingPercent = 55, MinReusePercent = 5


═══════════════════════════════════════════════════════════
         VERIFICACIÓN DE COBERTURA POR DASHBOARD
═══════════════════════════════════════════════════════════

Tras generar los datos, verificar que cada dashboard tiene datos
para TODOS sus widgets:

▸ Dashboard HOME (§0.1):
  ✓ Embudo de traslados por estado → WasteMoves con los 6 estados
  ✓ Kg recogidos vs tratados → EntryPlants + TreatmentPlantResidues
  ✓ Tasa reciclaje/valorización → TreatmentPlantResidues con fracciones
  ✓ Huella CO₂ acumulada → WasteMoveResidues.TransportCarbonEmissions
  ✓ Incidencias abiertas → Incidents con ClosedAt = NULL
  ✓ Cumplimiento objetivos → MarketShares vs EntryPlants reales
  ✓ Próximas recogidas → ServiceOrders Pending/Scheduled próximos 7 días
  ✓ Mapa interactivo → Entities con Latitude/Longitude + DUMZones

▸ Dashboard 1 — Optimización Logística SCRAP (/logistics/optimization):
  ✓ Mapa con puntos de recogida y plantas → Entities con coordenadas
  ✓ Volumen RAEE por zona → WasteMoveResidues con Weight por provincia
  ✓ Eficiencia de rutas → TransportDistance, TransportCarbonEmissions
  ✓ Utilización vehículos → WasteMoveResidues con VehicleType variado
  ✓ Cumplimiento DUM → WasteMoves con fechas vs DUMRestrictionRules
  ✓ Heatmap llegadas planta → EntryPlants.PlantEntryDate variadas
  ✓ Incidencias logísticas → Incidents abiertas tipo logístico

▸ Dashboard 2 — Monitorización Pública (/logistics/public-monitoring):
  ✓ Servicios por SCRAP → WasteMoves vinculados a Agreements con PublicEntity
  ✓ Histórico mensual → WasteMoveResidues distribuidos en 12 meses
  ✓ Liquidaciones → Settlements vinculadas a acuerdos
  ✓ Emisiones CO₂ comparativa → WasteMoveResidues de 2 periodos
  ✓ Objetivos municipales → MarketShares vs EntryPlantResidues

▸ Dashboard 3 — Panel Operativo (/logistics/operations):
  ✓ W1 SO pendientes de planificar → ServiceOrders en "Pending"
  ✓ W2 Embudo de traslados → WasteMoves en todos los estados
  ✓ W3 Planificación semanal → ServiceOrders próximos 7 días
  ✓ W4 Incidencias abiertas → Incidents activas
  ✓ W5 Entradas CAC hoy → EntryCACs con fecha reciente
  ✓ W6 Stock por residuo en CAC → EntryCACResidues
  ✓ W7 Tickets pendientes → EntryCACs pendientes
  ✓ W8 Entradas planta hoy → EntryPlants con fecha reciente
  ✓ W9 Balance tratamiento → TreatmentPlantResidues
  ✓ W10 Impropios → TreatmentPlants.ImproperWeight
  ✓ W11 Incidencias planta → Incidents vinculadas

▸ KPIs Regulatorios (/kpis):
  ✓ Tasa reciclaje → TreatmentPlantResidues + TreatmentOperations
  ✓ Tasa reutilización → WeightReused + IsPreparationForReuse
  ✓ % cumplimiento MarketShares → MarketShares vs pesos reales
  ✓ Intensidad CO₂/tonelada → TransportCarbonEmissions / Weight
  ✓ Desglose por categoría → Residues.ProductCategory variado
  ✓ Histórico 4 trimestres → datos distribuidos en el año

▸ Trazabilidad (/traceability):
  ✓ Búsqueda por DI/NT/Ticket/Referencia → campos informados

▸ Documentos (/documents):
  ✓ AgreementDocuments con DocumentHash
  ✓ WasteMoves con DocumentId
  ✓ Settlements con EvidenceRefsJson

▸ UC3-A Movilidad Coordinador (/mobility/coordinator-analysis):
  ✓ Mapa densidad recogidas → WasteMoves con coordenadas de origen
  ✓ Heatmap temporal 7×24 → ActualPickupStart con horas variadas
  ✓ Índice de conflicto por municipio → recogidas en hora punta + DUM
  ✓ Comparativa pre/post → datos de 2 meses distintos
  ✓ Recomendaciones → % hora punta > 30% en al menos 1 municipio

▸ UC3-B Movilidad Ayuntamiento (/mobility/municipal-monitoring):
  ✓ KPIs de impacto → WasteMoves en su municipio
  ✓ Calendario recogidas → ServiceOrders Pending/Scheduled
  ✓ Histórico recogidas vs incidencias → datos mensuales
  ✓ Cumplimiento por SCRAP → WasteMoves por SCRAP en municipio

▸ UC3-C Movilidad Oficina (/mobility/dispatch-data):
  ✓ Dataset exportable → WasteMoves + WasteMoveResidues completos
  ✓ Resumen por SCRAP → agrupación WasteMoves
  ✓ Planificación semanal → ServiceOrders próximos 7 días
  ✓ Serie mensual → datos de 12 meses

▸ Declaraciones de Producción (/product-declarations/dashboard):
  ✓ Declaraciones por estado → ProductDeclaration en 4 estados
  ✓ Volumen por periodo → Products con Quantity
  ✓ Top 10 productos → Products variados
  ✓ Productores sin declaración → al menos 1 productor sin declaración
  ✓ Importe total → ProductDeclaration.Amount

═══════════════════════════════════════════════════════════
         REGLAS TÉCNICAS DE IMPLEMENTACIÓN
═══════════════════════════════════════════════════════════

1. El seed de demo debe estar en un método separado del seed de
   catálogos (SeedDemoDataAsync o similar), ejecutado solo si
   IsDevelopment() o si existe una variable de configuración
   "SeedDemoData": true.

2. Usar el OwnerId de demo (el mismo GUID que ya se use para el
   tenant de desarrollo, o crear uno fijo de demo).

3. Ser IDEMPOTENTE: antes de insertar, comprobar si ya existen
   registros de demo (por ejemplo, verificando si existe la
   primera entidad de demo por su NationalId fijo).

4. Todas las fechas deben calcularse RELATIVAMENTE a DateTime.UtcNow:
   - "hace 3 meses" = DateTime.UtcNow.AddMonths(-3)
   - "próximos 7 días" = DateTime.UtcNow.AddDays(random 1-7)
   Esto garantiza que los dashboards siempre muestran datos recientes
   sin importar cuándo se ejecute el seed.

5. Las fechas deben incluir HORAS variadas, no solo 00:00:00.
   Usar horas como 07:45, 08:30, 10:15, 13:00, 15:30, 17:45, 20:00.

6. Los GUIDs de las entidades de demo pueden ser fijos (hardcoded)
   para facilitar las referencias cruzadas entre entidades.

7. Generar Hash (SHA-256) donde corresponda (Agreements, WasteMoves,
   Settlements, Incidents) usando un helper estándar.

8. Los campos Version deben ser 1 para registros nuevos.

9. CreatedAt/UpdatedAt y DateCreateSys/DateModifiedSys deben ser
   coherentes con la cronología del dato.

10. Las coordenadas GPS deben ser REALES de ciudades españolas para
    que el mapa interactivo muestre puntos reconocibles.
```

---

## 📝 Notas para la sesión de Copilot

- Este prompt es largo. Si Copilot se queda corto, puedes dividirlo en fases:
  - **Fase 1**: Entidades + Usuarios + Catálogos (secciones 1-4)
  - **Fase 2**: Acuerdos + MarketShares + ServiceOrders (secciones 5-7)
  - **Fase 3**: WasteMoves + Entradas + Tratamientos (secciones 8-11)
  - **Fase 4**: Liquidaciones + Incidencias + resto (secciones 12-18)
- Tras cada fase, pídele que actualice `COPILOT_CONTEXT.md` con el estado.
- Después de generar todo, pídele: *"Ahora revisa que cada widget de la sección VERIFICACIÓN DE COBERTURA POR DASHBOARD tiene datos suficientes. Si alguno no lo tiene, genera los registros faltantes."*
