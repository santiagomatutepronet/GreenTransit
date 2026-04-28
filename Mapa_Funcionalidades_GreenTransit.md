# 🗺️ Mapa de Funcionalidades — Sistema de Trazabilidad **GreenTransit**

> Plataforma web **multi-rol**, **multi-tenant** (`OwnerId`) y preparada para **data spaces (EDC)** que cubre el ciclo completo del residuo: planificación → ejecución → pesaje → tratamiento → justificación económica → reporting regulatorio.
>
> Basado en el modelo de datos técnico **v4.1** (SQL Server Azure). Cada funcionalidad detalla **entidades implicadas**, **campos clave** y **transiciones de estado** del traslado.

---

## 📌 Convenciones generales del sistema

Todas las funcionalidades deben respetar las convenciones del modelo v4.1:

- **PKs**: `uniqueidentifier` (GUID) en tablas operativas y económicas; `int IDENTITY` en catálogos, geografía y seguridad.
- **Multi-tenant**: todo registro operativo filtra por `OwnerId`. Un usuario solo ve los registros de su tenant.
- **Auditoría**: `CreatedAt` / `UpdatedAt` (UTC) en tablas nuevas; `DateCreateSys` / `DateModifiedSys` en tablas legacy (`WasteMoves`, `EntryPlants`, `EntryCACs`, `TreatmentPlants`).
- **Versionado e integridad**: `Version` + `Hash` en tablas clave (Agreements, Settlements, ServiceOrders, WasteMoves, Incidents, Residues...).
- **Discriminadores**:
  - `Entities.EntityRole` → `Producer | OperatorTransfer | SCRAP | PublicEntity | Carrier | CAC | Plant | Coordinator | Other`. ⚠️ Los roles `Source` y `Destination` han sido eliminados: cualquier tipo de entidad puede actuar como origen o destino de un traslado según el contexto; el sistema filtra los selectores por `EntityRole` permitido en cada campo.
  - `Residues.ResidueType` → `Waste | Product | ProductSpec`.
  - `TreatmentOperations.Code` → `R1–R13` (valorización) / `D1–D15` (eliminación) — Directiva 2008/98/CE, Ley 7/2022.

### 🔄 Estados del traslado (máquina de estados)

> Estado lógico NO presente como columna en el modelo. Se implementa en `WasteMoves.ServiceStatus` (existe como `nvarchar(32)`) y/o en un campo controlado a nivel aplicación. Las transiciones se registran en un log de auditoría interno.

```
[SOLICITADO] → [PLANIFICADO] → [RECOGIDO] → [EN CAC]* → [EN PLANTA] → [CLASIFICADO]
                                         ↘───────────────→ [EN PLANTA]
(* EN CAC es opcional; solo si el destino intermedio es un Centro de Acopio)
```

Reglas de transición (claves):

| Desde → Hasta | Dispara | Requiere |
|---|---|---|
| `SOLICITADO → PLANIFICADO` | Asignación transportista/vehículo | `WasteMoves.IdSource`, `IdDestination`, `WasteMoveResidues.IdCarrier`, `TransportInfo_vehicleRegistration`, `PlannedPickupStart/End` |
| `PLANIFICADO → RECOGIDO` | Confirmación de carga | `ActualPickupStart`, `GatheredDate`, `DocumentId`/`DocumentHash` |
| `RECOGIDO → EN CAC` | Entrada en CAC intermedio | Registro `EntryCACs` + `EntryCACResidues` |
| `RECOGIDO | EN CAC → EN PLANTA` | Pesaje en planta | Registro `EntryPlants` con `GrossWeight` / `TareWeight` / `NetWeight` + `TicketScale` |
| `EN PLANTA → CLASIFICADO` | Tratamiento aplicado | Registro `TreatmentPlants.IdTreatmentOperation` + `TreatmentPlantResidues` (balance reutilizado/valorizado/rechazo) |
| Cualquiera → `BLOQUEADO` | Incidencia `Severity = High/Critical` | Registro `Incidents` abierto |

---

## 0. 🏠 Página de Inicio y Navegación General

### 0.1. Dashboard operativo (Home)

- **Lógica**: landing tras login. Vista 360º del estado del ecosistema filtrada por `OwnerId` y `Users.IdProfile` (cada perfil ve sus KPIs).
- **KPIs y widgets** (Chart.js / ApexCharts):
  - **Embudo de traslados** por estado: nº de `WasteMoves` en `SOLICITADO / PLANIFICADO / RECOGIDO / EN CAC / EN PLANTA / CLASIFICADO`.
  - **Kg recogidos vs tratados** (mes/trimestre/año): suma de `EntryPlantResidues.Weight` (o `EntryPlants.NetWeight`) vs `TreatmentPlantResidues.WeightTotal`.
  - **Tasa de reciclaje / valorización / rechazo**: ratio `WeightReused + WeightValued` / `WeightTotal` cruzado con `TreatmentOperations.IsRecycling / IsEnergyRecovery / IsPreparationForReuse`.
  - **Huella de CO₂ acumulada**: suma de `WasteMoveResidues.TransportInfo_TransportCarbonEmissions`.
  - **Incidencias abiertas**: `Incidents` donde `ClosedAt IS NULL`, segmentado por `Severity`.
  - **Cumplimiento de objetivos**: real (de `WasteMoves`/`EntryPlants`) vs `MarketShares.Weight` del periodo.
  - **Próximas recogidas planificadas**: `ServiceOrders.PlannedPickupStart` en los próximos 7 días.
  - **Mapa interactivo**: entidades (`Entities.Latitude/Longitude`) + puntos de recogida + zonas DUM (`DUMZones.GeometryJson`).
- **Entidades**: vistas agregadas sobre `WasteMoves`, `WasteMoveResidues`, `EntryPlants`, `TreatmentPlants`, `TreatmentPlantResidues`, `Incidents`, `MarketShares`, `Entities`.
- **Roles**: todos; el dashboard se adapta al perfil.

### 0.2. Navegación y UX global

- **Layout**: sidebar colapsable + topbar con selector de `OwnerId` (para admins multi-tenant), buscador global y notificaciones.
- **Buscador global**: busca por `ServiceOrderNumber`, `WasteMoveReference`, `TicketScale`, `DINumber`, `NTNumber`, `AgreementNumber`, `Entities.Name` / `NationalId` / `CenterCode`.
- **Stepper de traslado**: componente visual que muestra en qué estado está cada `WasteMove` y cuáles son los siguientes pasos permitidos.
- **Notificaciones en tiempo real**: cambios de estado, incidencias críticas, liquidaciones a validar.
- **Modo oscuro / claro** y diseño responsive mobile-first (clave para operadores de campo en planta/CAC/transporte).

---

## 1. 📚 Módulo de Configuración y Maestros (Back-Office)

### 1.1. Gestión de Entidades (Ecosistema)

- **Lógica**: CRUD centralizado de **todos** los actores del ecosistema. El campo `EntityRole` es el filtro crítico que determina dónde puede aparecer la entidad en cada selector (p. ej. solo entidades con `EntityRole ∈ {Producer, CAC, PublicEntity, OperatorTransfer}` pueden ser origen de un traslado). Una misma entidad puede tener varios registros si desempeña distintos roles. **Al crear una entidad, el sistema genera automáticamente un usuario vinculado** con el perfil correspondiente a su `EntityRole`, permitiendo que la nueva entidad acceda al sistema desde el primer momento.
- **Entidades**: `Entities`, `Users`, `Profiles`.
- **Campos clave**:
  - Identificación: `Id`, `Name`, `NationalId` (NIF/CIF/VAT), `CenterCode` (NIMA), `EntityRole`.
  - Clasificación: `TypeThirdParty`, `InscriptionType`, `InscriptionNumber`, `EntityType`, `EconomicActivity`.
  - Localización: `CountryCode`, `StateCode`, `ProvinceCode`, `MunicipalityCode`, `ZipCode`, `Address`, `Latitude`, `Longitude` (para geovallado DUM y cálculo de rutas).
  - Contacto: `PhoneNumber`, `Email`, `ContactPerson`.
  - Control: `IsActive`, `SourceSystem`, `CreatedAt`, `UpdatedAt`, `IdUser`.
- **Validaciones**:
  - `NationalId` único por `OwnerId` + `EntityRole`.
  - Si `EntityRole = Plant | CAC`, `Latitude`/`Longitude` obligatorios.
  - Si `EntityRole = Carrier`, obligatorio `InscriptionNumber`.
  - Para la creación automática de usuario: `Email` obligatorio (se usará como `Users.Login`).
- **🔗 Provisión automática de usuario al crear entidad**:
  - Al confirmar el alta de una `Entity`, el sistema ejecuta una **transacción conjunta** que crea también un registro en `Users` con los siguientes datos derivados:

  | Campo `Users` | Origen |
  |---|---|
  | `Login` | `Entities.Email` (o `NationalId` si no hay email) |
  | `Email` | `Entities.Email` |
  | `IdProfile` | Mapeado automático desde `EntityRole` (ver tabla abajo) |
  | `OwnerId` | Heredado de la `Entity` (mismo tenant) |
  | `NationalId` (FK Country) | Derivado de `Entities.CountryCode` → `Country.id` |
  | `GeographicalId` | Derivado de `Entities.StateCode` → `TerritoryState.id` |
  | `MunicipalityId` | Derivado de `Entities.MunicipalityCode` → `Municipality.Id` |

  - **Mapeo `EntityRole` → `Profiles.Reference`**:

  | EntityRole | Perfil asignado (`Profiles.Reference`) | Justificación |
  |---|---|---|
  | `SCRAP` | `SCRAP` | Gestión de acuerdos, liquidaciones y reporting propio |
  | `Producer` | `PRODUCER` | Alta de SOs, declaraciones de producto, lectura operativa |
  | `Carrier` | `CARRIER` | Recogidas, app móvil, confirmación de carga |
  | `Plant` | `PLANT_OP` | Entradas en planta, pesaje, tratamiento |
  | `CAC` | `CAC_OP` | Entradas en CAC, registro de acopio |
  | `PublicEntity` | `PUBLIC_ENT` | Revisión de acuerdos, liquidaciones, reporting municipal |
  | `Coordinator` | `COORDINATOR` | Lectura transversal del ámbito de los acuerdos |
  | `OperatorTransfer` | `CARRIER` | Mismo perfil que transportista (gestiona movimientos) |
  | `Other` | *(no se crea usuario automáticamente)* | Entidad auxiliar sin sesión propia. El admin puede crear usuario manualmente si procede |

  - **Comportamiento del flujo**:
    1. El formulario de alta de entidad muestra una sección colapsable "Acceso al sistema" que se activa automáticamente cuando el `EntityRole` tiene perfil mapeado.
    2. El usuario puede personalizar el `Login` sugerido y añadir una contraseña temporal (o activar envío de enlace de activación por email).
    3. Si la `Entity` ya tiene un `Users` vinculado (p. ej. tras edición de rol), el sistema avisa y ofrece: (a) actualizar el perfil existente, (b) crear un segundo usuario, (c) no hacer nada.
    4. Si se **desactiva** una `Entity` (`IsActive = 0`), se ofrece desactivar también al usuario vinculado (bloqueo de acceso, no eliminación).
    5. En la **importación masiva CSV** de entidades, se generan los usuarios correspondientes en lote; el sistema genera contraseñas temporales y exporta un listado seguro para distribución.
- **Funciones**: listado con filtros (por rol, provincia, activo/inactivo), importación masiva CSV (con provisión de usuarios en lote), exportación, mapa de entidades, columna "Usuario vinculado" en el listado con enlace directo a la ficha del usuario.
- **Roles**: **Administrador** (CRUD), **SCRAP** (lectura + alta restringida a su ámbito), resto (lectura de entidades que les afectan).

### 1.2. Catálogo LER (Lista Europea de Residuos)

- **Lógica**: catálogo normativo inmutable (editable solo por Administrador). Activa validaciones legales posteriores (peligrosos → DI/NT obligatorios; RAEE → flujo RAEE específico).
- **Entidad**: `LERCodes`.
- **Campos clave**: `Code` (6 dígitos, único), `CodeExtended`, `Description`, `Chapter` + `ChapterDescription`, `SubChapter` + `SubChapterDescription`, `IsDangerous`, `IsRAEE`, `DefaultProductCategory`, `Notes`, `IsActive`.
- **Funciones**: búsqueda jerárquica (capítulo → subcapítulo → código), filtro peligrosos/RAEE, exportación oficial.
- **Roles**: **Administrador** (CRUD), resto (solo lectura).

### 1.3. Catálogo de Residuos y Productos

- **Lógica**: catálogo maestro unificado de residuos operativos, productos declarados y fichas técnicas (ecodiseño). El discriminador `ResidueType` determina el contexto y los campos aplicables.
- **Entidad**: `Residues` ↔ `LERCodes` ↔ `Entities` (Producer).
- **Campos clave**:
  - Discriminador y descripción: `ResidueType` (`Waste | Product | ProductSpec`), `Name`, `Description`, `Reference`.
  - Clasificación normativa: `IdLERCode` → `LERCodes`, `IsDangerous`, `IsRAEE`, `DangerousCode` (H-codes, UN, ADR), `ProductUse`, `ProductCategory`.
  - Medidas: `WeightPerUnitKg`, `DefaultMeasureUnit`.
  - Ecodiseño: `ReparabilityIndex`, `DisassemblyEase`, `ContainsHazardous`, `RecycledContentPercent`, `CompositionJson`, `PotentialLERCodesJson`, `MaterialsJson`.
  - Trazabilidad: `IdProducer` → `Entities` (EntityRole=Producer), `ProducerRef`.
  - Control: `IsActive`, `Version`, `Hash`, `SourceSystem`, `CreatedAt`, `UpdatedAt`, `IdUser`.
- **Validaciones**:
  - `IsDangerous = 1` ⇒ `DangerousCode` obligatorio.
  - `ResidueType = ProductSpec` ⇒ `IdProducer` obligatorio.
- **Roles**: **Administrador** (CRUD), **Productor** (CRUD sobre sus propios `Product` / `ProductSpec`).

### 1.4. Catálogo de Operaciones de Tratamiento (R/D)

- **Lógica**: catálogo normativo de operaciones según Directiva Marco 2008/98/CE. Sustituye los textos libres de v3. Los flags booleanos permiten computar KPIs automáticamente.
- **Entidad**: `TreatmentOperations`.
- **Campos clave**: `Code` (R1–R13 / D1–D15, único), `OperationType` (`Recovery | Disposal`), `Description`, `ShortDescription`, `IsRecycling`, `IsEnergyRecovery`, `IsPreparationForReuse`, `SortOrder`, `IsActive`.
- **Funciones**: selector agrupado por tipo (Recovery / Disposal) en los formularios de `TreatmentPlants` y `WasteMoveResidues`.
- **Roles**: **Administrador** (mantenimiento muy esporádico — solo cambios normativos).

### 1.5. Catálogos Geográficos

- **Lógica**: jerarquía geográfica oficial usada para validar y enriquecer direcciones de entidades y usuarios.
- **Entidades**: `Country` → `TerritoryState` → `Province` → `Municipality` → `MunicipalityPopulation` / `MunicipalityZipCode`.
- **Funciones**: selectores en cascada (país → CCAA → provincia → municipio → CP) en todos los formularios con dirección.
- **Roles**: **Administrador** (muy esporádico), resto (lectura).

### 1.6. Diccionarios y estados documentales

- **Lógica**: listas de valores de soporte. Sin FK explícitas — uso lógico por valor.
- **Entidades**: `dicProductDeclarationCategory`, `dicProductDeclarationPeriods`, `dicProductDeclarationProducts`, `dicProductDeclarationSource`, `dicProductDeclarationType`, `dicProductDeclarationUse`, `DocStates`.
- **Funciones**: alimenta combos en declaraciones de producto y control de estados documentales (`Borrador | Emitido | Firmado | Validado | Rechazado`).
- **Roles**: **Administrador**.

### 1.7. Factores de Emisión (maestro de sostenibilidad)

- **Lógica**: catálogo versionado de factores `kgCO₂e/km` por combinación vehículo/combustible/Euro. Imprescindible para el cálculo de huella.
- **Entidades**: `EmissionFactorSets` ↔ `EmissionFactors`.
- **Campos clave**:
  - `EmissionFactorSets`: `FactorSetName`, `Version`, `Status`, `ValidFrom`, `ValidTo`.
  - `EmissionFactors`: `FactorSetId` (FK), `VehicleType`, `FuelType`, `EuroClass`, `Unit` (p.ej. `kgCO2e/km`), `Value`.
- **Funciones**: subir nueva versión (mantener histórico), activar como set por defecto, previsualizar factores antes de aplicar.
- **Roles**: **Administrador**.

---

## 2. 💶 Módulo de Contratación y Economía

### 2.1. Formalización de Acuerdos (Agreements)

- **Lógica**: contrato marco entre SCRAP, entidad pública y (opcional) coordinador. Define vigencia, ámbito geográfico, modelo tarifario, mínimos y obligaciones mediante campos JSON flexibles. **Sin `Agreement.Status = Active` y vigencia vigente no se pueden liquidar servicios**.
- **Entidades**: `Agreements` ↔ `AgreementDocuments` ↔ `Entities`.
- **Campos clave de `Agreements`**:
  - Identificación y vigencia: `AgreementNumber` (único), `Status` (`Draft | Active | Expired | Cancelled`), `EffectiveFrom`, `EffectiveTo`.
  - Partes (FK → `Entities`): `IdScrap`, `IdPublicEntity`, `IdCoordinator`.
  - Ámbito: `WasteStream`, `SubStream`, `AutonomousCommunity`, `ProvinceCode`, `MunicipalityCode`, `CoveredMethodsJson`.
  - Economía: `TariffModelType`, `Currency` (default `EUR`), `TariffRulesJson`, `MinimumsJson`, `ObligationsJson`.
  - Auditoría: `Version`, `Hash`, `CreatedAt`, `UpdatedAt`, `IdUser`.
- **Campos clave de `AgreementDocuments`**: `AgreementId` (FK), `DocumentType` (Contrato/Anexo/Acta), `DocumentId` (ref. externa DMS/Blob), `DocumentHash` (SHA), `SignedAt`, `SignatureProvider`.
- **Funciones**: wizard de alta (partes → ámbito → tarifas → obligaciones → firma), versionado con nuevo `Hash`, alerta de próximo vencimiento (30/60/90 días antes de `EffectiveTo`), repositorio documental con hash de integridad.
- **Roles**: **Administrador**, **SCRAP** (alta/edición de los suyos), **Entidad Pública** (lectura + firma).

### 2.2. Liquidación Económica (Settlements)

- **Lógica**: cierre económico periódico. Agrupa los pesos netos de entradas validadas (`EntryPlants.NetWeight`) y aplica las reglas tarifarias del `Agreement`. Desglosa por LER/categoría en `SettlementLines`.
- **Entidades**: `Settlements` ↔ `SettlementLines` ↔ `Agreements`, `EntryPlants`, `LERCodes`.
- **Flujo**:
  1. Selección de `AgreementId` + `Year` + `Month`.
  2. Sistema recupera `EntryPlants` del periodo, filtra por el ámbito del acuerdo.
  3. Aplica `TariffRulesJson` y `MinimumsJson` → genera `SettlementLines` (una por `IdLERCode` × `ProductCategory`).
  4. Calcula cabecera: `BaseAmount`, `AdjustmentsAmount` (eco-modulación si existe), `TaxAmount`, `TotalAmount`.
  5. Estado pasa a `Pending` → validación → `Approved` / `Rejected`.
- **Campos clave de `Settlements`**: `SettlementNumber` (único), `Status`, `AgreementId`, `Year`, `Month`, `IdScrap`, `IdPublicEntity`, `Currency`, `BaseAmount`, `AdjustmentsAmount`, `TaxAmount`, `TotalAmount`, `EvidenceRefsJson`, `Validator`, `ValidationStatus`, `ValidatedAt`, `ValidationRef`, `Version`, `Hash`.
- **Campos clave de `SettlementLines`**: `SettlementId` (FK), `ProductCategory`, `IdLERCode`, `WeightKg`, `PricePerKg`, `Amount`, `EvidenceType`, `SourceIdsJson` (array de IDs de `EntryPlants`/`WasteMoves`/tickets).
- **Funciones**: previsualización antes de generar, re-cálculo, bloqueo tras `Approved`, exportación a formato SII/factura.
- **Roles**: **Administrador** (cálculo), **SCRAP** (validador), **Entidad Pública** (revisión).

### 2.3. Objetivos y Cuotas de Mercado (MarketShares)

- **Lógica**: gestión de objetivos de recogida/reciclaje por SCRAP, categoría, comunidad autónoma y periodo. Se contrasta con el rendimiento real agregado desde `WasteMoves` y `EntryPlants` para mostrar el % de cumplimiento en el dashboard y alertas.
- **Entidad**: `MarketShares`.
- **Campos clave**: `IdScrap` (FK `Entities`), `Category`, `AutonomousCommunity`, `Year`, `Weight` (objetivo en kg), `Period`, `EffectiveFrom`, `EffectiveTo`, `FlowType`.
- **Funciones**: comparativa real vs objetivo en dashboard, alertas cuando el % de cumplimiento a fecha cae bajo un umbral, exportación regulatoria.
- **Roles**: **Administrador**, **SCRAP** (lectura).

---

## 3. 🚛 Flujo Operativo de Residuos (Core Logistics)

> Cada paso documenta la **entidad que se crea/actualiza** y la **transición de estado** del traslado.

### 3.1. Paso 1 — Generar Orden de Servicio (`ServiceOrders`)

- **Lógica**: punto de partida operativo. Un Productor, Entidad Pública o Administrador "contrata" un servicio de recogida: qué residuo, dónde, cuándo, con qué prioridad, bajo qué acuerdo. La SO define la **promesa** de servicio.
- **Entidad**: `ServiceOrders` ↔ `Entities` ↔ `LERCodes`.
- **Estado del traslado**: N/A (aún no existe `WasteMove`).
- **Campos clave**:
  - Identificación: `ServiceOrderNumber` (único), `IssuedAt`, `Status`, `Priority` (default `Normal`).
  - Emisor: `IdIssuedBy` → `Entities` (+ `IssuedByName`/`NationalId`/`CenterCode` de respaldo).
  - Clasificación del residuo: `WasteStream`, `SubStream`, `ProductUse`, `ProductCategory`, `IdLERCode` → `LERCodes`.
  - **Punto de recogida (v4)**: `IdPickupPoint` → `Entities` (con `EntityRole ∈ {CAC, PublicEntity, Producer, OperatorTransfer}`). ⚠️ Sustituye a los campos `Point_*` de v1.
  - Ventanas planificadas: `PlannedPickupStart`, `PlannedPickupEnd`, `PlannedDeliveryStart`, `PlannedDeliveryEnd`.
  - Estimación: `EstimatedWeight`, `MeasureUnit`, `Units`, `ContainersJson`.
  - Asignaciones previstas: `IdCarrier` → `Entities` (Carrier), `IdPlannedPlant` → `Entities` (Plant).
  - Ejecución (se actualizan luego): `WasteMoveReference`, `TicketScalePlanned`, `ActualPickupStart/End`, `ActualDeliveryStart/End`, `TransportDistanceKm`, `TransportDurationMin`, `VehicleRegistration`, `VehicleType`, `FuelType`, `EuroClass`.
  - Auditoría: `Version`, `Hash`, `CreatedAt`, `UpdatedAt`, `IdUser`.
- **Validaciones**:
  - `IdPickupPoint` debe pertenecer a `OwnerId` o ámbito permitido.
  - Si `IdLERCode.IsDangerous = 1`, avisar al usuario de obligaciones NT/DI aguas abajo.
  - Validar cruce con `Agreements` vigente (ámbito geográfico + waste stream).
- **Funciones**: alta rápida, duplicación de orden recurrente, adjuntar `ContainersJson` (nº y tipo de contenedores), vinculación opcional a un `Agreement`.
- **Roles**: **Productor**, **Entidad Pública**, **Administrador**, **Gestor logístico**.

### 3.2. Paso 2 — Crear Solicitud de Traslado (`WasteMoves`) → Estado **SOLICITADO**

- **Lógica**: agrupación de una o varias `ServiceOrders` en un movimiento logístico real. Se fijan origen y destino (entidades del maestro) y los SCRAPs implicados.
- **Entidades**: `WasteMoves` ↔ `WasteMoveResidues` ↔ `ServiceOrders` ↔ `Entities` ↔ `Residues`.
- **Estado**: `SOLICITADO`.
- **Campos clave de `WasteMoves`**:
  - Vinculación: `ServiceOrderId` → `ServiceOrders`, `WasteMoveReference`, `Lot`, `RequestDate`.
  - Actores: `IdSource`, `IdDestination`, `IdScrap`, `IdScrap2` (si doble SCRAP), `IdOperatorTransfer` → todos FK a `Entities`.
  - Tiempos planificados: `PlannedPickupStart/End`, `PlannedDeliveryStart/End`.
  - Estado: `ServiceStatus` (aquí se materializa la máquina de estados).
  - Control: `OwnerId`, `DateCreateSys`, `DateModifiedSys`, `IdUser`, `SourceSystem`, `Version`.
- **Campos clave de `WasteMoveResidues`** (una línea por residuo):
  - Vinculación: `IdWasteMove` → `WasteMoves`, `IdResidue` → `Residues`.
  - Cantidades: `Weight`, `MeasureUnit`, `Units`, `unitPriceKg`, `DateDelivery`.
  - Destino de tratamiento previsto: `IdTreatmentOperationDestiny` → `TreatmentOperations`.
- **Validaciones**:
  - `IdSource` debe tener `EntityRole ∈ {Producer, CAC, PublicEntity, OperatorTransfer}`.
  - `IdDestination` debe tener `EntityRole ∈ {Plant, CAC, SCRAP}`.
  - Si `Residues.IsDangerous = 1` o `IsRAEE = 1`, obligar `IdTreatmentOperationDestiny`.
  - Heredar `IdScrap` y `IdLERCode` desde la `ServiceOrder` origen.
- **Funciones**: agrupación multi-SO, consolidación de cargas, preview del DI/NT a generar.
- **Roles**: **Gestor logístico**, **Administrador**.

### 3.3. Paso 3 — Planificación Logística → Estado **PLANIFICADO**

- **Lógica**: se asigna el "quién" (transportista + vehículo) y el "cuándo" definitivo. Se valida contra zonas DUM antes de confirmar.
- **Entidades**: `WasteMoves` (actualiza), `WasteMoveResidues` (asigna transportista por línea), `Entities` (Carrier), `DUMZones` + `DUMRestrictionRules` (validación).
- **Estado**: `SOLICITADO → PLANIFICADO`.
- **Campos que se actualizan**:
  - En `WasteMoves`: `PlannedPickupStart/End`, `PlannedDeliveryStart/End`, `IdOperatorTransfer`.
  - En `WasteMoveResidues`: `IdCarrier` → `Entities` (EntityRole=Carrier), `TransportInfo_vehicleRegistration`, `TransportInfo_vehicleRegistrationTrailer`, `VehicleType`, `FuelType`, `EuroClass`.
- **Validaciones automáticas**:
  - El `Entities` seleccionado como `Carrier` debe tener `InscriptionNumber`.
  - Cruce con `DUMZones`: si el `IdPickupPoint` cae dentro de un polígono con regla activa, lanzar aviso/bloqueo según `DUMRestrictionRules.ActionType`.
  - Chequeo de vigencia del `Agreement` en la fecha planificada.
- **Funciones**: tablero Kanban de planificación, sugerencia del factor de emisión aplicable, edición masiva.
- **Roles**: **Gestor logístico**, **Administrador**, **Transportista** (lectura y aceptación).

### 3.4. Paso 4 — Ejecución de Recogida → Estado **RECOGIDO**

- **Lógica**: el transportista confirma la carga en origen. Se generan y registran los documentos normativos (DI, NT). Tras este paso se dispara el cálculo de huella de CO₂.
- **Entidades**: `WasteMoves`, `WasteMoveResidues`.
- **Estado**: `PLANIFICADO → RECOGIDO`.
- **Campos que se actualizan**:
  - En `WasteMoves`: `ActualPickupStart`, `ActualPickupEnd`, `GatheredDate`, `DocumentId`, `DocumentHash`, `SignatureStatus`.
  - En `WasteMoveResidues` (clave v4): `NTNumber` (Notificación de Traslado), `DINumber` (Documento de Identificación), `DIPhase` (E1/E2/E3/E4/E5…).
- **Validaciones**:
  - Si `IsDangerous = 1`, `NTNumber` + `DINumber` + `DIPhase` son obligatorios.
  - Captura de evidencia (foto del contenedor, firma digital del responsable del origen).
- **Funciones**:
  - App móvil para transportistas: escaneo QR/código del contenedor, captura de firma, geolocalización.
  - Disparo automático del **cálculo de emisiones** (§4.1).
- **Roles**: **Transportista**, **Operador logístico** (supervisión).

### 3.5. Paso 5 — Recepción en Centro de Acopio (`EntryCACs`) → Estado **EN CAC** *(opcional)*

- **Lógica**: paso intermedio si el destino no es planta final sino un Centro de Acopio Ciudadano. Permite saber qué hay dentro del CAC.
- **Entidades**: `EntryCACs` ↔ `EntryCACResidues` ↔ `Residues`.
- **Estado**: `RECOGIDO → EN CAC`.
- **Campos clave de `EntryCACs`**:
  - Vinculación: `IdWasteMove` (relación lógica), `WasteMoveReference`.
  - Operación: `CACEntryDate`, `TypeContainer`, `PriceContainer`, `CollectionMethod`.
  - Auditoría: `OwnerId`, `DateCreateSys`, `DateModifiedSys`, `IdUser`.
- **Campos clave de `EntryCACResidues`**: `IdEntryCAC` (FK), `IdResidue` → `Residues`, `Weight`, `MeasureUnit`, `Units`, `PriceWeight`, `PriceUnit`.
- **Funciones**: registro rápido en terminal del CAC, etiquetado, preparación para consolidación y posterior traslado a planta.
- **Roles**: **Operador de CAC**.

### 3.6. Paso 6 — Entrada y Pesaje en Planta (`EntryPlants`) → Estado **EN PLANTA**

- **Lógica**: el destino pesa el residuo en báscula oficial. **El `NetWeight` es el valor oficial para la liquidación económica y el reporte regulatorio**. Puede venir directamente desde `RECOGIDO` o desde `EN CAC`.
- **Entidades**: `EntryPlants` ↔ `EntryPlantResidues` ↔ `ServiceOrders` ↔ `Residues`.
- **Estado**: `RECOGIDO | EN CAC → EN PLANTA`.
- **Campos clave de `EntryPlants`**:
  - Vinculación: `IdWasteMove`, `WasteMoveReference`, `ServiceOrderId` → `ServiceOrders`.
  - Ticket de báscula: `TicketScale`, `WeighbridgeId`, `PlantEntryDate`.
  - Pesos: `GrossWeight`, `TareWeight`, `NetWeight` (calculado Bruto − Tara).
  - Contenedor: `TypeContainer`, `PriceContainer`.
  - Auditoría: `OwnerId`, `DateCreateSys`, `DateModifiedSys`, `IdUser`.
- **Campos clave de `EntryPlantResidues`**: `IdEntryPlant` (FK), `IdResidue`, `Weight`, `MeasureUnit`, `Units`, `PriceWeight`, `PriceUnit`.
- **Validaciones**:
  - `NetWeight = GrossWeight - TareWeight` (calculado en backend, no editable).
  - Descuadre respecto a `WasteMoveResidues.Weight` ⇒ sugerir incidencia automática.
- **Funciones**: integración directa con báscula (si hay `WeighbridgeId`), captura de ticket, conciliación automática con `WasteMoveResidues`.
- **Roles**: **Operador de Planta**.

### 3.7. Paso 7 — Clasificación y Tratamiento Final (`TreatmentPlants`) → Estado **CLASIFICADO**

- **Lógica**: la planta procesa el residuo y lo desglosa en tres fracciones: **reutilizado**, **valorizado** y **rechazo**. Asigna la operación R/D oficial. Cierre del flujo operativo.
- **Entidades**: `TreatmentPlants` ↔ `TreatmentPlantResidues` ↔ `TreatmentOperations` ↔ `Residues` ↔ `Incidents`.
- **Estado**: `EN PLANTA → CLASIFICADO`.
- **Campos clave de `TreatmentPlants`**:
  - Vinculación: `IdWasteMove`, `WasteMoveReference`, `ServiceOrderId`, `TicketScale`.
  - Tratamiento: `PlantTreatmentDate`, `IdTreatmentOperation` → `TreatmentOperations` (R/D oficial).
  - Calidad: `ImproperWeight`, `QualityMetricsJson`.
  - Incidencia: `IncidentId` → `Incidents`.
  - Contenedor: `TypeContainer`, `PriceContainer`.
- **Campos clave de `TreatmentPlantResidues`** (balance obligatorio):
  - Entrada: `IdResidue` → `Residues`, `Category`, `WeightTotal`, `MeasureUnit`, `Units`, `PriceWeight`, `PriceUnit`.
  - **Fracción reutilizada**: `IdResidueReused` → `Residues`, `WeightReused`, `MeasureUnitReused`, `UnitsReused`.
  - **Fracción valorizada**: `IdResidueValued` → `Residues`, `WeightValued`, `MeasureUnitValued`, `UnitsValued`.
  - **Fracción rechazo**: `IdResidueRemove` → `Residues`, `WeightRemove`, `MeasureUnitRemove`, `UnitsRemove`.
- **Validación crítica (balance de masas)**:
  ```
  |WeightTotal − (WeightReused + WeightValued + WeightRemove) − ImproperWeight| < tolerancia (p. ej. 1%)
  ```
  Si falla ⇒ abrir `Incident` automática con `Severity = High`, bloqueando la transición a `CLASIFICADO`.
- **Funciones**: formulario de clasificación con validación en tiempo real, métricas de calidad (p.ej. % contaminantes), trazabilidad completa desde la SO origen hasta cada fracción.
- **Roles**: **Operador de Planta**.

### 3.8. Consulta 360º del traslado

- **Lógica**: vista consolidada de todo el ciclo de un traslado: SO origen → WasteMove → entradas CAC/Planta → tratamiento final → liquidación asociada → incidencias → huella.
- **Entidades** (join lógico): `ServiceOrders` + `WasteMoves` + `WasteMoveResidues` + `EntryCACs/Residues` + `EntryPlants/Residues` + `TreatmentPlants/Residues` + `SettlementLines` + `Incidents`.
- **Funciones**: timeline con todos los eventos, mapa con origen/ruta/destino, descarga de expediente completo en PDF (DI, NT, ticket, certificado de tratamiento).
- **Roles**: todos (con filtrado por visibilidad).

---

## 4. 🌱 Módulo de Sostenibilidad, Incidencias y Zonas DUM

### 4.1. Cálculo de Emisiones (Huella de Carbono)

- **Lógica**: al pasar a `RECOGIDO`, el sistema cruza la distancia del transporte con el factor de emisión aplicable (según `FuelType` + `VehicleType` + `EuroClass`). El resultado se persiste a nivel de línea de residuo.
- **Entidades**: `WasteMoveResidues`, `EmissionFactorSets`, `EmissionFactors`.
- **Cálculo**:
  ```
  TransportCarbonEmissions (kgCO₂e) = TransportInfo_TransportDistance (km)
                                     × EmissionFactors.Value (kgCO₂e/km)
                                     × factor de carga opcional
  ```
- **Campos que se actualizan en `WasteMoveResidues`**:
  - `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`.
  - `EmissionFactorSetId` → `EmissionFactorSets` (trazabilidad del cálculo).
  - `EmissionFactorVersion` (snapshot de la versión aplicada).
- **Funciones**: re-cálculo por lote si cambia el `EmissionFactorSet` vigente, informe comparativo de huella por SCRAP/transportista/trimestre.
- **Roles**: **Automático** (backend); **Administrador** puede forzar re-cálculo.

### 4.2. Consumo energético de plantas (`PlantEnergies`)

- **Lógica**: cada planta declara su consumo eléctrico por año/mes. Permite imputar huella de Scope 2 al tratamiento.
- **Entidad**: `PlantEnergies`.
- **Campos clave**: `PlantName`, `PlantCenterCode`, `Year`, `Month`, `KwhTotal`, `Source`, `GridMixRef`, `AllocationMethod`, `Notes`.
- **Funciones**: carga mensual/anual, integración en dashboard de sostenibilidad, cálculo de kgCO₂e por tonelada tratada.
- **Roles**: **Operador de Planta**, **Administrador**.

### 4.3. Gestión de Incidencias (`Incidents`)

- **Lógica**: cualquier usuario puede abrir una incidencia en cualquier estado del traslado. Si `Severity ∈ {High, Critical}`, el sistema **bloquea la transición al siguiente estado** hasta que se cierre.
- **Entidad**: `Incidents`.
- **Campos clave**: `Type`, `Severity` (`Low | Medium | High | Critical`), `OpenedAt`, `ClosedAt`, `ServiceOrderId`, `WasteMoveReference`, `TicketScale`, `ReportedByName/NationalId/CenterCode`, `Description`, `ResolutionJson`, `Version`, `Hash`.
- **Tipos típicos**: descuadre de peso, residuo no conforme, retraso, avería vehículo, contaminación de fracción, documento faltante.
- **Funciones**: apertura con foto+geolocalización, workflow de resolución, vinculación a `TreatmentPlants.IncidentId`, exportación histórica, bandeja de incidencias abiertas en dashboard.
- **Roles**: apertura → cualquier perfil; resolución/cierre → **Administrador** o perfil responsable según `Type`.

### 4.4. Control de Zonas DUM (Geofencing)

- **Lógica**: al planificar un traslado, se comprueba si las coordenadas del `IdPickupPoint` (`Entities.Latitude/Longitude`) caen dentro de un polígono DUM con regla activa. Según la acción → se avisa, se restringe o se bloquea.
- **Entidades**: `DUMZones` ↔ `DUMRestrictionRules`.
- **Campos clave**:
  - `DUMZones`: `ZoneCode` (único), `GeometryJson` (GeoJSON polígono/multipolígono), `Status`.
  - `DUMRestrictionRules`: `ZoneId` (FK), `RuleCode`, `ValidFrom`, `ValidTo`, `ConditionsJson` (p. ej. horario, tipo vehículo, Euro mínimo), `ActionType` (`Block | Restrict | Allow | Notify`), `ActionReason`.
- **Funciones**: editor visual de zonas sobre mapa (Leaflet/Mapbox), simulador "¿puedo entrar aquí con mi vehículo?", log de bloqueos y notificaciones.
- **Roles**: **Administrador** (alta/edición); resto (validación automática al planificar).

---

## 5. 📈 Módulo de Reporting, Trazabilidad y Data Space

### 5.1. Trazabilidad end-to-end del residuo

- **Lógica**: dado un `IdResidue` o un `LERCode`, recuperar cadena completa: productor → SO → WasteMove → CAC → planta → fracciones de tratamiento.
- **Funciones**: buscador por `DINumber`, `NTNumber`, `TicketScale`, `WasteMoveReference`; exportar expediente en PDF/XML.

### 5.2. KPIs y cumplimiento regulatorio

- **Lógica**: vistas agregadas para justificar cumplimiento de objetivos (Ley 7/2022, RD envases, RD RAEE…).
- **KPIs**:
  - Tasa de reciclaje = `Σ WeightValued (operaciones R con IsRecycling=1)` / `Σ WeightTotal`.
  - Tasa de preparación para reutilización = `Σ WeightReused (IsPreparationForReuse=1)` / `Σ WeightTotal`.
  - % cumplimiento MarketShares = real / `MarketShares.Weight`.
  - Intensidad CO₂ (kgCO₂e / tonelada movida).
- **Funciones**: cuadros de mando filtrables por SCRAP, CCAA, año, categoría; exportación en XLSX/CSV/PDF.
- **Roles**: **Administrador**, **SCRAP**, **Entidad Pública**.

### 5.3. Gestión documental centralizada

- **Lógica**: único punto de acceso a todos los documentos (contratos, anexos, DI, NT, tickets, certificados, actas de liquidación).
- **Entidades implicadas**: `AgreementDocuments`, campos `DocumentId/DocumentHash/SignatureStatus` en `WasteMoves`, `EvidenceRefsJson` en `Settlements`.
- **Funciones**: repositorio con búsqueda por hash, verificación de integridad (SHA), integración con SharePoint vía `UserSharePointCredentials`.
- **Roles**: todos con permisos acordes a visibilidad.

### 5.4. Interoperabilidad y Data Space (EDC)

- **Lógica**: la plataforma está preparada para participar en ecosistemas tipo IDSA/Gaia-X. Los usuarios tienen `PortalEDCProvider` y `PortalEDCConsumer` (URLs de conector EDC) que permiten publicar/consumir datasets regulados.
- **Entidades**: `Users.PortalEDCProvider`, `Users.PortalEDCConsumer`, `SourceSystem`, `Hash` (integridad entre sistemas).
- **Funciones**: publicación de datasets agregados (sin PII), catálogo de recursos disponibles, contratos de uso de datos.
- **Roles**: **Administrador**, **SCRAP**, **Entidad Pública**.

---

## 6. 👥 Gestión de Usuarios, Perfiles y Seguridad

### 6.1. Perfiles y control de acceso (`Profiles`)

- **Lógica**: cada usuario pertenece a un `Profile` que determina sus permisos funcionales y de visibilidad.
- **Entidad**: `Profiles`.
- **Campos clave**: `ID`, `Reference` (código único), `Description`.
- **Perfiles estándar sugeridos** (alimentan `Profiles`):

| Reference | Descripción | Acceso típico |
|---|---|---|
| `ADMIN` | Administrador del sistema | Todo (multi-tenant si aplica) |
| `SCRAP` | Sistema colectivo de responsabilidad ampliada | Agreements, Settlements, MarketShares propios + lectura operativa |
| `PRODUCER` | Productor / Generador | Sus `ServiceOrders`, sus `Residues` (Product/ProductSpec) |
| `CARRIER` | Transportista | `WasteMoves` / `WasteMoveResidues` donde figura como `IdCarrier`; app móvil |
| `PLANT_OP` | Operador de Planta | `EntryPlants`, `TreatmentPlants`, `PlantEnergies` de su entidad |
| `CAC_OP` | Operador de CAC | `EntryCACs` de su entidad |
| `PUBLIC_ENT` | Entidad pública / Ayuntamiento | Agreements, Settlements, reporting del municipio |
| `COORDINATOR` | Coordinador del acuerdo | Lectura transversal del ámbito del `Agreement` |

### 6.2. Gestión de usuarios (`Users`)

- **Entidad**: `Users` ↔ `Profiles` ↔ `Country` / `TerritoryState` / `Municipality`.
- **Campos clave**: `ID`, `Login` (único), `Email`, `IdProfile` (FK → `Profiles`), `NationalId` (FK → `Country.id`), `GeographicalId` (FK → `TerritoryState.id`), `MunicipalityId` (FK → `Municipality.Id`), `OwnerId` (tenant), `PortalEDCProvider`, `PortalEDCConsumer`.
- **Funciones**:
  - Alta/baja/edición con validación contra catálogos geográficos.
  - Asignación a un `OwnerId` (tenant) y/o a una `Entity` del ecosistema (vínculo lógico: un `PLANT_OP` pertenece a una `Entities` con `EntityRole=Plant`).
  - **Vinculación bidireccional con Entidades**: desde la ficha de usuario se muestra la `Entity` de origen (si fue creado automáticamente desde §1.1). Desde la ficha de la entidad se muestra el usuario vinculado. Si un usuario se crea manualmente (sin entidad origen), se puede vincular después a una `Entity` existente.
  - Reseteo de password, 2FA opcional, bloqueo por inactividad.
  - **Sincronización de estado Entity ↔ User**: si una `Entity` se desactiva (`IsActive = 0`), el sistema propone desactivar al usuario vinculado; si se reactiva, propone reactivarlo. El admin siempre confirma antes de ejecutar.
- **Roles**: **Administrador** del tenant.

### 6.3. Credenciales SharePoint (`UserSharePointCredentials`)

- **Lógica**: integración por usuario con SharePoint para gestión documental delegada.
- **Entidad**: `UserSharePointCredentials`.
- **Campos clave**: `TenantId` (Azure AD), `ClientId`, `ClientSecret` (cifrado en reposo), `IsActive` (solo una activa por usuario).
- **Funciones**: alta/rotación segura, prueba de conexión, desactivación.
- **Roles**: usuario propietario + **Administrador**.

### 6.4. Multi-tenancy y visibilidad de datos

- **Lógica**: todas las tablas con `OwnerId` se filtran automáticamente por el `Users.OwnerId` del usuario autenticado. Un usuario NO ve datos de otros tenants salvo que su perfil sea `ADMIN` global.
- **Regla transversal**: middleware/API debe inyectar siempre `WHERE OwnerId = @currentOwner` (salvo maestros compartidos: `LERCodes`, `TreatmentOperations`, geografía).

### 6.5. Auditoría y seguridad

- **Lógica**: todo cambio queda trazado vía `CreatedAt`/`UpdatedAt`/`DateCreateSys`/`DateModifiedSys` + `IdUser`. Para tablas con `Hash`/`Version`, se detectan manipulaciones.
- **Funciones**: log de accesos, log de cambios con diff, verificación de `Hash` en documentos (`AgreementDocuments.DocumentHash`, `WasteMoves.DocumentHash`).

---

## 7. 🎨 Diseño, UX y arquitectura frontend

### 7.1. Estructura de la aplicación

- **SPA**: Angular / React / Blazor (a elegir) + componentes visuales consistentes.
- **Layouts por perfil**:
  - **Operativo** (Transportista, Operador Planta/CAC): foco en formularios rápidos, cámara, firma, lector QR. Mobile-first.
  - **Gestor** (SCRAP, Entidad Pública, Productor): foco en listas, filtros, reporting.
  - **Admin**: foco en catálogos, configuración, auditoría.

### 7.2. Componentes visuales clave

- **Stepper de traslado** reutilizable mostrando `SOLICITADO → PLANIFICADO → RECOGIDO → EN CAC → EN PLANTA → CLASIFICADO` con el paso actual destacado y tooltips con fechas reales.
- **Timeline vertical** para la vista 360º del traslado.
- **Tablas**: ordenación, filtros por columna, exportación CSV/XLSX, selección múltiple para acciones masivas.
- **Formularios con wizard** para alta de `Agreements`, `ServiceOrders`, `TreatmentPlants`.
- **Mapa interactivo** (Leaflet/Mapbox): entidades, rutas, DUM zones.
- **Gráficos** (Chart.js/ApexCharts): series temporales, embudos, donuts, mapas de calor.
- **Badges de estado** con paleta consistente (p.ej. `SOLICITADO`=gris, `PLANIFICADO`=azul, `RECOGIDO`=amarillo, `EN CAC`=naranja claro, `EN PLANTA`=naranja, `CLASIFICADO`=verde, `INCIDENCIA`=rojo).

### 7.3. Sistema de diseño

- Paleta verde/azul (sostenibilidad) + grises neutros.
- Tipografía: Inter / Roboto / similar.
- Iconografía: Lucide / Material Icons con significado estable (🚛 transporte, 🏭 planta, ♻️ valorización, ⚠️ incidencia…).
- **Accesibilidad**: contraste AA, navegación por teclado, etiquetas ARIA.
- **i18n**: preparado para ES/EN/CA/EU (el ámbito es nacional multi-comunidad).

### 7.4. Experiencia móvil (operadores de campo)

- **PWA instalable** para transportistas y operadores de planta/CAC.
- Funcionalidades offline: captura de recogida sin conexión → sincronización al volver.
- Escaneo de códigos QR/barras en contenedores y tickets de báscula.
- Firma digital en pantalla, captura de foto georreferenciada como evidencia (hash guardado).

---

## 8. 🔒 Matriz de permisos resumen

| Funcionalidad | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Maestros (Entities, LER, Residues, TreatmentOperations) | CRUD | R | R (sus productos) | R | R | R | R | R |
| Agreements / AgreementDocuments | CRUD | CRUD (suyos) | R | – | – | – | R (suyos) | R (suyos) |
| Settlements / SettlementLines | CRUD | V | – | – | – | – | R | R |
| MarketShares | CRUD | R | – | – | – | – | R | R |
| ServiceOrders | CRUD | R | CRUD (suyos) | R | R | R | CRUD (suyos) | R |
| WasteMoves / WasteMoveResidues | CRUD | R | R | U (asignados) | R | R | R | R |
| EntryCACs / EntryCACResidues | R | R | – | – | R | CRUD | R | R |
| EntryPlants / EntryPlantResidues | R | R | – | – | CRUD | – | R | R |
| TreatmentPlants / TreatmentPlantResidues | R | R | – | – | CRUD | – | R | R |
| PlantEnergies | R | R | – | – | CRUD | – | R | R |
| Incidents | CRUD | C+R | C+R | C+R | C+R | C+R | C+R | C+R |
| DUMZones / Rules | CRUD | R | R | R | – | – | R | R |
| Users / Profiles | CRUD (tenant) | R (sus ops) | – | – | – | – | – | – |

Leyenda: **C**=Create, **R**=Read, **U**=Update, **D**=Delete, **V**=Validar, **–**=sin acceso.

---

## 9. ✅ Checklist técnico previo al desarrollo

- [ ] Script de creación BD v4.1 ejecutado (`Crear_BD_v4_1.sql`).
- [ ] Seed de catálogos: `LERCodes` (lista oficial), `TreatmentOperations` (R1–R13, D1–D15), geografía, `Profiles`.
- [ ] Seed de un `EmissionFactorSet` por defecto.
- [ ] API REST con autenticación (JWT / OAuth2 / AzureAD).
- [ ] Middleware multi-tenant que inyecta `OwnerId` en todas las consultas operativas.
- [ ] Máquina de estados del traslado como servicio (no como lógica dispersa).
- [ ] Job programado para cálculo de huella al cambio de estado a `RECOGIDO`.
- [ ] Job programado para recordatorios de vencimiento de `Agreements`.
- [ ] Validación de `Hash` en documentos al subir/descargar.
- [ ] Logs de auditoría + verificación periódica de integridad.

---

*Documento generado para guiar la implementación con Copilot/Claude sobre el modelo GreenTransit v4.1.*
