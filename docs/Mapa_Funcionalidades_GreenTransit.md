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
- **Buscador global**: busca por `ServiceOrderNumber`, `WasteMoveReference`, `TicketScale`, `DINumber`, `NTNumber`, `AgreementNumber`, `Entities.Name` / `NationalId` / `CenterCode`. ✅ IMPLEMENTADO — `Application/Features/Search/Queries/GlobalSearchQuery.cs` + `Web/Components/Shared/GlobalSearchBar.razor` integrado en el Topbar de `MainLayout.razor`. Debounce 300 ms, navegación por teclado (↑↓ + Enter + Escape), resultados agrupados por tipo con ícono diferenciador.
- **Menú lateral colapsable por grupos**: ✅ IMPLEMENTADO — Cada grupo del sidebar (Configuración, Operaciones, Economía, Declaraciones, Sostenibilidad, Reporting, Seguridad) puede contraerse o expandirse individualmente haciendo clic en el título del grupo. Un chevron (›) indica el estado; se anima con transición CSS. Por defecto todos los grupos arrancan contraídos al iniciar sesión.
  - **Estado persistente entre navegaciones** ✅ IMPLEMENTADO — `Web/Services/NavMenuStateService.cs` (Scoped) mantiene el `HashSet<string>` de grupos colapsados durante toda la sesión del circuito Blazor Server. Las navegaciones entre páginas no reinicializan el componente `NavMenu`, por lo que el estado del menú se preserva hasta que el usuario cierra la sesión o recarga el navegador.
- **Filtrado dinámico de enlaces del menú por permisos de página**: ✅ IMPLEMENTADO — `NavMenu.razor` consulta `IPagePermissionService.CanAccessRouteAsync` para cada enlace antes de renderizarlo. Los permisos se precargan en `OnInitializedAsync` desde la tabla `PagePermissions` (con caché de 5 min en `IMemoryCache`). Solo aparecen en el menú las rutas que el perfil del usuario tiene asignadas. La doble capa de seguridad se mantiene: `ProfileAuthorizeView` filtra por perfil estático → `PagePermissionService` aplica la configuración dinámica de BD → `RouteAccessGuard` protege el acceso directo por URL aunque el enlace no aparezca.
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

#### ✅ Decisiones de implementación — Campos `WasteStream` y `SubStream`

| Campo | Comportamiento implementado |
|---|---|
| **Flujo de residuo (`WasteStream`)** | Selector cerrado (`<select>`) con las 15 categorías del catálogo `WasteFlowCatalog` (Domain/Constants). Opciones agrupadas en `RAP` y `Operativos`. |
| **Sub-flujo (`SubStream`)** | Selector dependiente del flujo seleccionado. Deshabilitado hasta que se elige `WasteStream`. Al cambiar el flujo el sub-flujo se limpia automáticamente. |
| **Validación cruzada** | Solo combinaciones definidas en `WasteFlowCatalog.IsValidCombination()` son aceptadas. |
| **Catálogo compartido** | `Domain/Constants/WasteFlowCatalog.cs` — fuente de verdad única reutilizada en `AgreementWizard.razor` y `ServiceOrderForm.razor`. |

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

### 2.3. Objetivos y Cuotas de Mercado (MarketShares) ✅ IMPLEMENTADO

- **Lógica**: gestión de objetivos de recogida/reciclaje por SCRAP, categoría, comunidad autónoma y periodo. Se contrasta con el rendimiento real agregado desde `WasteMoves` y `EntryPlants` para mostrar el % de cumplimiento en el dashboard y alertas.
- **Entidad**: `MarketShares`.
- **Campos clave**: `IdScrap` (FK `Entities`), `Category`, `AutonomousCommunity`, `Year`, `Weight` (objetivo en kg), `Period`, `EffectiveFrom`, `EffectiveTo`, `FlowType`.
- **Funciones**: comparativa real vs objetivo en dashboard, alertas cuando el % de cumplimiento a fecha cae bajo un umbral, exportación regulatoria.
- **Roles**: **Administrador** (CRUD), **SCRAP** (lectura — solo sus cuotas).
- **Archivos implementados**:
  - `Application/Features/MarketShares/DTOs/MarketShareDtos.cs` — `MarketShareDto` (listados) y `MarketShareComplianceDto` (cumplimiento con `ObjectiveKg`, `AchievedKg`, `CompliancePercent`, `IsAtRisk`).
  - `Application/Features/MarketShares/Queries/MarketShareQueries.cs` — `GetMarketSharesQuery` (paginada, filtros `IdScrap?`, `Category?`, `AutonomousCommunity?`, `Year?`) y `GetMarketShareComplianceQuery(int year)` (calcula peso real desde `EntryPlantResidues` cruzando `WasteMove.IdScrap` y `Residue.ProductCategory`).
  - `Application/Features/MarketShares/Commands/MarketShareCommands.cs` — `CreateMarketShareCommand`, `UpdateMarketShareCommand` y `DeleteMarketShareCommand` con validadores FluentValidation y control de unicidad por `IdScrap + Category + AutonomousCommunity + Year + Period`.
  - `Domain/Authorization/PolicyConstants.cs` — `CanViewMarketShares` (SCRAP + ADMIN) y `CanManageMarketShares` (solo ADMIN).
  - `Web/Components/Pages/MarketShares/MarketShareList.razor` — página `/market-shares`.
  - `Web/Components/Shared/ProgressBar.razor` — componente reutilizable de barra de progreso coloreada.
- **Ruta**: `/market-shares`

#### ✅ Decisiones de implementación (UI)

| Aspecto | Comportamiento implementado |
|---|---|
| **Filtrado SCRAP** | `GetMarketSharesQuery` aplica automáticamente `WHERE IdScrap = @LinkedEntityId` si el perfil es SCRAP. El usuario no puede anular este filtro. |
| **Cálculo de cumplimiento** | `GetMarketShareComplianceQuery` agrega `EntryPlantResidues.Weight` del año filtrado, cruzando `EntryPlant.WasteMove.IdScrap` con `MarketShare.IdScrap` y `Residue.ProductCategory` con `MarketShare.Category`. |
| **`IsAtRisk`** | `true` si `CompliancePercent < 80 %`. Progreso esperado proporcional al mes en curso dentro del año. |
| **Barra de progreso** | Verde ≥ 100 %, naranja 80–99 %, rojo < 80 %. Componente `<ProgressBar>` reutilizable en Dashboard. |
| **Tipo flujo (`FlowType`)** | Selector cerrado con 6 valores: `RAEE`, `ENV`, `BAT`, `NFU`, `OIL`, `VFU` (con descripción larga). |
| **Categoría (`Category`)** | Selector dependiente del `FlowType` seleccionado. Se limpia automáticamente al cambiar el flujo. Catálogo estático en el componente (`CategoriesByFlow`). |
| **Comunidad autónoma** | Selector cerrado con las 19 comunidades y ciudades autónomas oficiales. |
| **Periodo** | Selector con los 4 trimestres (`T1`–`T4`) más opción «Anual» (valor vacío). |
| **Modal centrado** | Clase `modal-dialog-centered` en Bootstrap para centrar verticalmente el formulario. |
| **CRUD** | Botones de edición y eliminación visibles solo para ADMIN (`AuthorizeView Policy="CanManageMarketShares"`). Modal inline con confirmación para borrado. |
| **Widget Dashboard** | `GetMarketShareComplianceQuery` puede invocarse directamente desde `Dashboard.razor` + `<ProgressBar>` para mostrar el widget de cumplimiento (§0.1). |

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
  - Estimación: `EstimatedWeight`, `MeasureUnit` (**siempre 1 = Kg**, no editable por usuario), `Units`, `ContainersJson`.
  - Asignaciones previstas: `IdCarrier` → `Entities` (Carrier), `IdPlannedPlant` → `Entities` (Plant).
  - Ejecución (se actualizan luego): `WasteMoveReference`, `TicketScalePlanned`, `ActualPickupStart/End`, `ActualDeliveryStart/End`, `TransportDistanceKm`, `TransportDurationMin`, `VehicleRegistration`, `VehicleType`, `FuelType`, `EuroClass`.
  - Auditoría: `Version`, `Hash`, `CreatedAt`, `UpdatedAt`, `IdUser`.
- **Validaciones**:
  - `IdPickupPoint` debe pertenecer a `OwnerId` o ámbito permitido.
  - Si `IdLERCode.IsDangerous = 1`, avisar al usuario de obligaciones NT/DI aguas abajo.
  - Validar cruce con `Agreements` vigente (ámbito geográfico + waste stream).
- **Funciones**: alta rápida, duplicación de orden recurrente (`DuplicateServiceOrderCommand`), adjuntar `ContainersJson` (nº y tipo de contenedores), vinculación opcional a un `Agreement`, vinculación a un `WasteMove` mediante `LinkToWasteMoveCommand` (actualiza `WasteMoveReference` y cambia estado a `InProgress`), cancelación mediante `CancelServiceOrderCommand` (bloqueada si existe traslado activo vinculado).
- **Tabla hija `ServiceOrderResidues`**: cada SO puede tener **varias líneas de residuo** (`SortOrder`, `IdLERCode`, `ProductUse`, `ProductCategory`, `EstimatedWeight`, `MeasureUnit`, `Units`). La cabecera de la SO sincroniza siempre los campos de clasificación con la primera línea (`SortOrder = 0`). Al editar, las líneas se reemplazan íntegramente con `ExecuteDeleteAsync` para evitar conflictos de concurrencia en contextos de larga vida (Blazor Server).
- **Estados implementados** (`ServiceOrderStatuses`): `Pending`, `Scheduled`, `InProgress`, `Completed`, `Cancelled`. Solo `Pending` y `Scheduled` permiten edición (`Editable`). Cada estado tiene label y CSS badge propios.
- **Prioridades implementadas** (`ServiceOrderPriorities`): `Low`, `Normal`, `High`, `Critical`.
- **Roles**: **Productor**, **Entidad Pública**, **Administrador**, **Gestor logístico**.

#### ✅ Decisiones de implementación (UI)

| Campo | Comportamiento implementado |
|---|---|
| **Emisor (`IdIssuedBy`)** | Para `PRODUCER` y `PUBLIC_ENT`: se autocompleta con `CurrentUser.LinkedEntityId` y es de solo lectura. Para el resto de perfiles: selector libre sobre `Entities`. |
| **Unidad de medida (`MeasureUnit`)** | No se muestra en el formulario. Se almacena siempre como `1` (Kg) en el backend. El peso estimado se introduce siempre en kilogramos. |
| **Tipo de contenedor (`ContainersJson[].Type`)** | Campo `<select>` con opciones fijas: `Bigbag`, `Contenedor`, `Palé`, `Granel`, `Otro`. No se permite texto libre. |
| **Filtrado en lista** | `PRODUCER` y `PUBLIC_ENT` ven únicamente sus propias SOs (`IdIssuedBy = LinkedEntityId`). `SCRAP` ve las SOs **sin traslado asignado aún** (cualquier SCRAP del tenant puede reclamarlas) **más** las SOs cuyo traslado lo tiene como `IdScrap` o `IdScrap2`. El filtro se aplica en servidor vía `GetServiceOrdersQuery`. |
| **Flujo de residuo (`WasteStream`)** | Selector cerrado (`<select>`) con las 15 categorías del catálogo `WasteFlowCatalog` (Domain/Constants), agrupadas en `RAP` y `Operativos`. No se permite texto libre. |
| **Sub-flujo (`SubStream`)** | Selector dependiente del flujo seleccionado; deshabilitado hasta que se elige `WasteStream`. Al cambiar el flujo el sub-flujo se limpia. Catálogo compartido con `AgreementWizard`. |
| **Filtro por flujo en listado** | Desplegable `WasteStream` en la barra de filtros de `ServiceOrderList.razor`. Pasa el código a `GetServiceOrdersQuery.WasteStream` para filtrar en BD. La columna muestra el nombre completo del flujo (no el código). |

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

#### ✅ Decisiones de implementación (UI)

| Aspecto | Comportamiento implementado |
|---|---|
| **Filtrado en lista SCRAP** | `SCRAP` ve únicamente los traslados donde figura como `IdScrap` o `IdScrap2`. Filtro en servidor vía `GetWasteMovesQuery` con `ICurrentUserService`. |
| **Filtrado OwnerId** | Todos los perfiles ven solo traslados de su tenant (`OwnerId`). Filtro aplicado en el handler, no en la UI. |

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

#### ✅ Decisiones de implementación (UI)

| Aspecto | Comportamiento implementado |
|---|---|
| **Filtrado en lista SCRAP** | `SCRAP` ve únicamente las entradas en CAC cuyo traslado vinculado (`IdWasteMove → WasteMoves`) tiene `IdScrap` o `IdScrap2` igual a `LinkedEntityId`. Filtro en servidor vía `GetEntryCACsQuery`. |

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

#### ✅ Decisiones de implementación (UI)

| Aspecto | Comportamiento implementado |
|---|---|
| **Filtrado en lista SCRAP** | `SCRAP` ve únicamente las entradas en planta cuyo traslado vinculado (`IdWasteMove → WasteMoves`) tiene `IdScrap` o `IdScrap2` igual a `LinkedEntityId`. Filtro en servidor vía `GetEntryPlantsQuery`. |

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

#### ✅ Decisiones de implementación (UI)

| Aspecto | Comportamiento implementado |
|---|---|
| **Filtrado en lista SCRAP** | `SCRAP` ve únicamente los tratamientos en planta cuyo traslado vinculado (`IdWasteMove → WasteMoves`) tiene `IdScrap` o `IdScrap2` igual a `LinkedEntityId`. Filtro en servidor vía `GetTreatmentPlantsQuery`. |

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

#### ✅ Decisiones de implementación (UI)

| Aspecto | Comportamiento implementado |
|---|---|
| **Listado PRODUCER** | Solo muestra incidencias cuya `ServiceOrderId → ServiceOrder.IdIssuedBy == LinkedEntityId`. Filtro en servidor (`GetIncidentsQuery`). |
| **Listado SCRAP** | Solo muestra incidencias vinculadas a traslados donde figura como `IdScrap` o `IdScrap2` (filtro en servidor vía `ServiceOrderId` o `WasteMoveReference` cruzado con `WasteMoves`). |
| **Creación PRODUCER** | El campo "Traslado vinculado" es un `<select>` cargado con los traslados del productor (`GetWasteMovesQuery(ServiceOrderIssuedBy: LinkedEntityId)`). No se permite texto libre. |
| **Dashboard PRODUCER** | El widget de incidencias abiertas solo cuenta las vinculadas a sus SOs. |

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

### 5.1. Trazabilidad end-to-end del residuo ✅ IMPLEMENTADO

- **Lógica**: dado un `DINumber`, `NTNumber`, `TicketScale` o `WasteMoveReference`, recupera la cadena completa: SO → WasteMove → CAC → planta → fracciones de tratamiento → liquidaciones → incidencias → huella CO₂.
- **Archivos implementados**:
  - `Application/Features/Reporting/Queries/GetResidueTraceabilityQuery.cs` — resuelve el `WasteMoveId` a partir del término de búsqueda y delega en `GetWasteMoveTimelineQuery`.
  - `Web/Components/Pages/Reporting/ResidueTraceability.razor` — página `/traceability` con buscador, timeline completo, exportación PDF (QuestPDF) y exportación XML.
- **Ruta**: `/traceability`
- **Exportaciones**: PDF (reutiliza `WasteMoveTimelinePdfGenerator`) + XML estándar (`downloadBase64File` JS).
- **Funciones**: buscador por DI/NT/Ticket/Referencia, timeline con stepper de estado, KPIs (CO₂, pesos por fracción), liquidaciones vinculadas, incidencias.
- **Roles**: todos (con filtrado por `OwnerId`).

### 5.2. KPIs y cumplimiento regulatorio ✅ IMPLEMENTADO

- **Lógica**: vistas agregadas para justificar cumplimiento de objetivos (Ley 7/2022, RD envases, RD RAEE…).
- **KPIs**:
  - Tasa de reciclaje = `Σ WeightValued (operaciones R con IsRecycling=1)` / `Σ WeightTotal`.
  - Tasa de preparación para reutilización = `Σ WeightReused (IsPreparationForReuse=1)` / `Σ WeightTotal`.
  - % cumplimiento MarketShares = real / `MarketShares.Weight`.
  - Intensidad CO₂ (kgCO₂e / tonelada movida).
  - Desglose por categoría de residuo.
  - Histórico por los 4 trimestres del año seleccionado.
- **Archivos implementados**:
  - `Domain/Entities/RegulatoryTarget.cs` — objetivos mínimos configurables por `OwnerId` / `Category` / `Year`.
  - `Application/Common/Interfaces/IRegulatoryTargetDefaults.cs` — abstracción para valores por defecto.
  - `Application/Features/Reporting/DTOs/RegulatoryKpisDtos.cs` — DTOs (`RegulatoryKpisDto`, `QuarterlyKpiDto`, `CategoryKpiDto`, `MarketShareComplianceKpiDto`, `ExportKpisResultDto`).
  - `Application/Features/Reporting/Queries/GetRegulatoryKpisQuery.cs` — filtros: `Year`, `Quarter?`, `IdScrap?`, `AutonomousCommunity?`, `Category?`.
  - `Application/Features/Reporting/Queries/ExportKpisToExcelQuery.cs` — genera XLSX en memoria (ClosedXML) con 3 hojas: Resumen, Por Categoría, Histórico Trimestral.
  - `Web/Services/RegulatoryTargetDefaults.cs` — lee `appsettings["RegulatoryTargets"]` (DefaultMinRecyclingPercent=55, DefaultMinReusePercent=5).
  - `Web/Components/Pages/Reporting/RegulatoryKpis.razor` — página `/kpis`: cards con semáforo objetivo, gráfico ApexCharts trimestral, tabla MarketShare compliance, tabla por categoría, botón "Exportar XLSX".
- **Tabla BD**: `RegulatoryTargets` (OwnerId, Category, Year, MinRecyclingPercent, MinReusePercent) — si no existe registro usa valores appsettings.
- **Exportación XLSX**: 3 hojas en memoria, nunca persiste en servidor. `ContentType: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- **Roles**: **Administrador**, **SCRAP**, **Entidad Pública**.

### 5.3. Gestión documental centralizada ✅ IMPLEMENTADO

- **Lógica**: único punto de acceso a todos los documentos (contratos, anexos, DI, NT, tickets, certificados, actas de liquidación).
- **Entidades implicadas**: `AgreementDocuments`, campos `DocumentId/DocumentHash/SignatureStatus` en `WasteMoves`, `EvidenceRefsJson` + `Hash` en `Settlements`.
- **Archivos implementados**:
  - `Web/Components/Pages/Reporting/DocumentRepository.razor` — página `/documents`: lista unificada de documentos de `AgreementDocuments`, `WasteMoves` (con `DocumentId`) y `Settlements` (con `EvidenceRefsJson`). Columnas: Tipo, Referencia, Tipo Doc, Fecha, Hash, Estado firma, botón "Verificar hash". Filtros: tipo, referencia, rango de fechas.
- **Verificación de integridad**: SHA-256 recalculado en cliente y comparado con el hash almacenado. Muestra badge verde (OK) o rojo (ALTERADO).
- **Roles**: todos con permisos acordes a visibilidad.

### 5.4. Dashboards Logísticos Especializados ✅ IMPLEMENTADO

El sistema incluye tres dashboards logísticos diferenciados según el perfil del usuario, accesibles desde la sección **Reporting → Dashboards Logística** del menú lateral.

---

#### 5.4.1. Dashboard 1 — Panel de Optimización Logística SCRAP (`/logistics/optimization`)

- **Perfil objetivo**: SCRAP, COORDINATOR, ADMIN.
- **Policy**: `CanViewLogisticsOptimization`.
- **Lógica**: KPIs de eficiencia de rutas, volúmenes RAEE por zona geográfica, mapa interactivo de puntos de recogida y plantas, cumplimiento de zonas DUM y utilización de vehículos.
- **Archivos implementados**:
  - `Application/Features/Logistics/Queries/GetLogisticsOptimizationQuery.cs` — filtros: `Year`, `Month?`, `IdScrap?`, `WasteStream?`, `ProvinceCode?`. Aplica automáticamente filtro por `LinkedEntityId` si el perfil es SCRAP.
  - `Application/Features/Logistics/DTOs/LogisticsOptimizationDto.cs` — `LogisticsOptimizationDto` raíz con: `RouteEfficiencyDto` (AvgDistanceKmPerPickup, AvgCO2eKgPerPickup, CO2eKgPerTonne, CO2eTrendPercent%), `VolumeByZoneDto[]` (ProvinceCode, TotalKg, PickupCount), `LogisticsMapPointDto[]` (para mapa), `DumZoneLayerDto[]` (geometría + semáforo de acción), `DumComplianceDto`, `PlantArrivalHeatmapDto[]` (distribución día/hora), `OpenLogisticsIncidentDto[]`, `VehicleUtilizationDto[]`.
  - `Web/Components/Pages/Logistics/LogisticsOptimization.razor` — página `/logistics/optimization`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| KPIs de ruta | `WasteMoveResidues` | Distancia media/recogida, CO₂e/recogida, CO₂e/tonelada, variación % vs periodo anterior |
| Volumen por zona | `WasteMoveResidues` + geografía | Kg y nº recogidas por `ProvinceCode` |
| Mapa interactivo | `Entities` (Lat/Long) | Puntos de recogida, plantas, zonas DUM sobre mapa |
| Cumplimiento DUM | `DUMZones` + `DUMRestrictionRules` | % traslados en ventana DUM vs fuera de ventana |
| Heatmap llegadas | `EntryPlants.PlantEntryDate` | Distribución por día de semana y hora (7×24) |
| Incidencias abiertas | `Incidents` | Solo las logísticas del periodo |
| Utilización vehículos | `WasteMoveResidues.VehicleType` | Kg y recogidas por tipo de vehículo |

---

#### 5.4.2. Dashboard 2 — Panel de Monitorización Pública (`/logistics/public-monitoring`)

- **Perfil objetivo**: PUBLIC_ENT, ADMIN.
- **Policy**: `CanViewPublicMonitoring`.
- **Lógica**: vista orientada a entidades públicas (ayuntamientos, administraciones) para supervisar los servicios prestados por los SCRAP bajo sus acuerdos, el historial de recogidas, las liquidaciones y el cumplimiento de objetivos municipales.
- **Archivos implementados**:
  - `Application/Features/Logistics/Queries/GetPublicMonitoringQuery.cs` — filtros: `Year`, `Month?`. Restringe automáticamente a los acuerdos donde el usuario figura como `IdPublicEntity`.
  - `Application/Features/Logistics/DTOs/PublicMonitoringDto.cs` — `PublicMonitoringDto` con: `ScrapServiceSummaryDto[]` (resumen por SCRAP: TotalMoves, TotalKg, PendingMoves, CompletedMoves, CancelledMoves), `MonthlyPickupSeriesDto[]` (serie mensual kg/SCRAP), `SettlementRowDto[]` (liquidaciones de la entidad pública), `EmissionComparisonDto` (CO₂e actual vs anterior), `MunicipalTargetDto[]` (cumplimiento cuotas municipales por SCRAP).
  - `Web/Components/Pages/Logistics/PublicMonitoring.razor` — página `/logistics/public-monitoring`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Servicios por SCRAP | `WasteMoves` + `Agreements` | Tabla: nº traslados, kg, estado por SCRAP |
| Histórico mensual | `WasteMoveResidues` + `EntryPlants` | Serie de kg recogidos por SCRAP (últimos 12 meses) |
| Liquidaciones | `Settlements` | Lista con estado, importe, validación |
| Emisiones CO₂e | `WasteMoveResidues` | Comparativa periodo actual vs anterior |
| Objetivos municipales | `MarketShares` + `EntryPlantResidues` | % cumplimiento por SCRAP y categoría |

---

#### 5.4.3. Dashboard 3 — Panel Operativo (`/logistics/operations`)

- **Perfil objetivo**: DISPATCH_OFFICE, CAC_OP, PLANT_OP, ADMIN.
- **Policy**: `CanViewOperationalDashboard`.
- **Lógica**: panel multirrol que adapta su contenido al perfil activo. Incluye widgets específicos para la oficina de despacho, operadores de CAC y operadores de planta. El badge de perfil activo se muestra en el encabezado.
- **Archivos implementados**:
  - `Application/Features/Logistics/Queries/GetOperationalDashboardQuery.cs` — filtros: `Year`, `Month?`. Detecta el perfil activo y ajusta qué widgets se calculan.
  - `Application/Features/Logistics/DTOs/OperationalDashboardDto.cs` — `OperationalDashboardDto` con: `PendingServiceOrderDto[]` (SO pendientes de planificar), `WasteMoveFunnelItemDto[]` (embudo por estado), `WeeklyPlanItemDto[]` (próximos 7 días), `OpenIncidentRowDto[]` (incidencias abiertas), `CacEntryTodayDto[]` (entradas CAC hoy), `CacStockByResidueDto[]` (stock acumulado por residuo), `CacTicketsPending` (int), `PlantEntryTodayDto[]` (entradas planta hoy), `TreatmentBalanceDto` (balance reutilizado/valorizado/rechazo), `ImproperWeightKg` (decimal), `PlantOpenIncidents` (incidencias de planta abiertas).
  - `Web/Components/Pages/Logistics/OperationalDashboard.razor` — página `/logistics/operations`.
  - `Web/Components/Pages/Logistics/DumLegendRow.razor` — componente de leyenda de zonas DUM reutilizable.

**Widgets por sección**:

| Sección | Widget | Perfil |
|---|---|---|
| **DISPATCH_OFFICE** | W1 SO pendientes de planificar | DISPATCH_OFFICE, ADMIN |
| | W2 Embudo de traslados por estado | DISPATCH_OFFICE, ADMIN |
| | W3 Planificación semanal (próximos 7 días) | DISPATCH_OFFICE, ADMIN |
| | W4 Incidencias abiertas del ámbito | DISPATCH_OFFICE, ADMIN |
| **CAC_OP** | W5 Entradas en CAC hoy | CAC_OP, ADMIN |
| | W6 Stock acumulado por tipología de residuo | CAC_OP, ADMIN |
| | W7 Tickets de pesaje pendientes | CAC_OP, ADMIN |
| **PLANT_OP** | W8 Entradas en planta hoy | PLANT_OP, ADMIN |
| | W9 Balance de tratamiento del periodo | PLANT_OP, ADMIN |
| | W10 Impropios detectados (kg) | PLANT_OP, ADMIN |
| | W11 Incidencias de planta abiertas | PLANT_OP, ADMIN |

---

### 5.5. Interoperabilidad y Data Space (EDC)

- **Lógica**: la plataforma está preparada para participar en ecosistemas tipo IDSA/Gaia-X. Los usuarios tienen `PortalEDCProvider` y `PortalEDCConsumer` (URLs de conector EDC) que permiten publicar/consumir datasets regulados.
- **Entidades**: `Users.PortalEDCProvider`, `Users.PortalEDCConsumer`, `SourceSystem`, `Hash` (integridad entre sistemas).
- **Funciones**: publicación de datasets agregados (sin PII), catálogo de recursos disponibles, contratos de uso de datos.
- **Roles**: **Administrador**, **SCRAP**, **Entidad Pública**.

---

### 5.6. Módulo de Movilidad Urbana — Impacto RAEE ✅ IMPLEMENTADO

> Agrupa las vistas UC3 orientadas a analizar el impacto de los traslados RAEE en la movilidad urbana: horas punta, cumplimiento de zonas DUM, planificación semanal y serie histórica mensual.

#### 5.6.1. UC3-C — Datos de Impacto RAEE en Movilidad (Oficina de Asignación)

- **Ruta**: `/mobility/dispatch-data`
- **Perfil objetivo**: `DISPATCH_OFFICE`, `ADMIN`.
- **Policy**: `CanViewMobilityDispatchData`.
- **Lógica**: KPIs de movilidad calculados a partir de los traslados del periodo. Identifica recogidas en hora punta y mide el cumplimiento de franjas DUM.

**Archivos implementados**:
- `Application/Features/Mobility/Queries/GetMobilityDispatchDataQuery.cs` — handler principal con filtros: `Year`, `Month?`, `IdScrap?`, `ProvinceCode?`, `MunicipalityCode?`.
- `Application/Features/Mobility/DTOs/MobilityDtos.cs` — DTOs: `MobilityDispatchDataDto`, `MobilityExportRowDto`, `DispatchScrapSummaryDto`, `WeeklyPlanMobilityItemDto`, `MonthlyMobilitySeriesDto`.
- `Application/Features/Mobility/Queries/ExportMobilityDataToExcelQuery.cs` — genera XLSX en memoria (ClosedXML) con los datos del `ExportDataset`.
- `Application/Features/Mobility/Services/MobilityRecommendationEngine.cs` — motor de recomendaciones para replanificación de recogidas conflictivas.
- `Web/Components/Pages/Mobility/DispatchData.razor` — página `/mobility/dispatch-data`.

**Cálculo de hora punta** (configurable en `appsettings.json` → sección `MobilitySettings`):

| Parámetro | Default | Descripción |
|---|---|---|
| `PeakHourStart1` | 7.5 | Inicio franja matutina (07:30) |
| `PeakHourEnd1` | 9.5 | Fin franja matutina (09:30) |
| `PeakHourStart2` | 17.5 | Inicio franja vespertina (17:30) |
| `PeakHourEnd2` | 19.5 | Fin franja vespertina (19:30) |

Una recogida se considera **en hora punta** si `hora_decimal ∈ [Start1, End1) ∪ [Start2, End2)` donde `hora_decimal = Hour + Minute/60`.

**Cálculo de emisiones CO₂e** (campo `CO2eKg` en `MobilityExportRowDto`):
- Se lee directamente de `WasteMoveResidues.TransportInfo_TransportCarbonEmissions`.
- Dicho campo es calculado por `CalculateEmissionsCommand` al confirmar la recogida (estado → `RECOGIDO`):
  ```
  CO₂e (kg) = TransportInfo_TransportDistance (km) × EmissionFactor.Value (kgCO₂e/km)
  ```
- El `EmissionFactor` se selecciona del `EmissionFactorSet` activo más reciente (`Status = "Active"`, `ValidFrom <= UtcNow`), cruzando `VehicleType` + `FuelType` + `EuroClass`.
- La versión del set aplicado queda almacenada en `WasteMoveResidues.EmissionFactorVersion`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Dataset exportable | `WasteMoves` + `WasteMoveResidues` + `BusinessEntities` | Tabla detallada por traslado: distancia, duración, CO₂e, hora punta, cumplimiento DUM |
| Resumen por SCRAP | `WasteMoves` agrupado | `TotalPickups`, `PeakHourPercent`, `DumCompliancePercent`, `OpenIncidents` |
| Planificación semanal | `ServiceOrders` (próximos 7 días, estado `Pending`/`Scheduled`) | Semáforo `Red/Green` según si la hora planificada cae en hora punta |
| Serie mensual | `WasteMoves` (últimos 12 meses) | `PeakHourPercent`, `DumCompliancePercent`, `AvgConflictIndex` por periodo `YYYY-MM` |

**Filtros disponibles**: `Year` (obligatorio), `Month?`, `IdScrap?`, `ProvinceCode?`, `MunicipalityCode?`.

**Exportación**: botón "Exportar XLSX" → `ExportMobilityDataToExcelQuery` genera el `ExportDataset` como hoja Excel.

---

## 6. 👥 Gestión de Usuarios, Perfiles y Seguridad

> **Estado**: ✅ Implementado — Application + Web layers completos.

### 6.1. Perfiles y control de acceso (`Profiles`)

- **Lógica**: cada usuario pertenece a un `Profile` que determina sus permisos funcionales y de visibilidad.
- **Entidad**: `Profiles`.
- **Campos clave**: `ID`, `Reference` (código único), `Description`.
- **Implementación**:
  - `GetProfilesQuery` → `IReadOnlyList<ProfileDto>` — catálogo del sistema sin filtro OwnerId.
  - `ProfileList.razor` → `/profiles` — solo lectura, accesible a perfiles de seguridad.

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
| `DISPATCH_OFFICE` | Oficina de despacho | Gestión operativa transversal |

### 6.2. Gestión de usuarios (`Users`)

- **Entidad**: `Users` ↔ `Profiles` ↔ `Country` / `TerritoryState` / `Municipality`.
- **Campos clave**: `ID`, `Login` (único por `OwnerId`), `Email`, `IdProfile` (FK → `Profiles`), `NationalId` (FK → `Country.Id`), `GeographicalId` (FK → `TerritoryState.Id`), `MunicipalityId` (FK → `Municipality.Id`), `OwnerId` (tenant), `PortalEDCProvider`, `PortalEDCConsumer`, `IsActive` (acceso habilitado).
- **Restricciones de seguridad**:
  - Solo perfil `ADMIN` accede al módulo.
  - `ClientSecret` **nunca** se devuelve en queries ni DTOs.
  - `OwnerId` del usuario creado = `OwnerId` del admin autenticado (no configurable desde UI).
- **Implementación Application** (`Features/Security/`):
  - `GetUsersQuery` — paginada, filtros `IdProfile?`, `IsActive?`, `SearchTerm?`, filtra por `OwnerId` del admin.
  - `GetUserByIdQuery` — devuelve `UserDetailDto` con nombres de País/CCAA/Municipio resueltos y `LinkedEntityName`.
  - `CreateUserCommand` — validación: `Login` único por `OwnerId`.
  - `UpdateUserCommand` — loguea (`Warning`) cambios de perfil con perfil anterior y nuevo.
  - `DeactivateUserCommand` — establece `IsActive = false`.
  - `LinkUserToEntityCommand` — actualiza `BusinessEntity.IdUser` y loguea la operación.
- **Implementación Web** (`Components/Pages/Security/`):
  - `UserList.razor` → `/users` — tabla con filtros, badge de perfil coloreado, acciones Ver/Editar/Desactivar.
  - `UserForm.razor` → `/users/new`, `/users/{id}/edit` — formulario con `GeographySelector`, sección EDC colapsable.
  - `UserDetail.razor` → `/users/{id}` — ficha completa con botón "Ir a Entidad vinculada" condicional.
- **Migración EF**: `AddUserIsActive` — añade columna `IsActive bit NOT NULL DEFAULT 1` a tabla `Users`.

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

## 10. 📦 Módulo de Declaraciones de Producción

> Módulo que permite a los productores declarar periódicamente los productos puestos en el mercado, cumpliendo con las obligaciones de Responsabilidad Ampliada del Productor (RAP). La declaración se estructura como maestro-detalle: `ProductDeclaration` (cabecera) → `Products` (líneas de producto). Los diccionarios `dicProductDeclaration*` alimentan los selectores del formulario.

### 🔄 Estados de la declaración (máquina de estados)

> Estado gestionado en `ProductDeclaration.State` (nvarchar(64)). Las transiciones se controlan en el servicio de dominio `ProductDeclarationStateService`. Los estados documentales disponibles se definen en `DocStates`.

```
[BORRADOR] → [EMITIDO] → [VALIDADO]
                  ↓
             [RECHAZADO] → [BORRADOR] (corrección y reenvío)
```

Reglas de transición:

| Desde → Hasta | Dispara | Requiere | Quién |
|---|---|---|---|
| `BORRADOR → EMITIDO` | Productor confirma declaración | Al menos 1 línea en `Products`, `IdProducer` informado, `Year` + `Period` informados | PRODUCER, ADMIN |
| `EMITIDO → VALIDADO` | Administrador aprueba | Revisión de cantidades y referencias vs catálogo `Residues` | ADMIN |
| `EMITIDO → RECHAZADO` | Administrador rechaza | Motivo de rechazo obligatorio (campo `Reference` o campo adicional en log) | ADMIN |
| `RECHAZADO → BORRADOR` | Productor corrige | El productor modifica líneas y reenvía | PRODUCER, ADMIN |

---

---

## 11. 🌐 Módulo EcoDataNet — Espacio de Datos

> Epígrafe dedicado a la integración de GreenTransit con el **data space EcoDataNet** mediante conectores EDC (Eclipse Dataspace Components). En esta primera fase la funcionalidad es de tipo **atrezzo / mock frontend**: permite visualizar el flujo de publicación y validar la propuesta UX antes de conectar el backend real.

### 11.1. Publicar Datos en EcoDataNet

- **Lógica (mock)**: permite al usuario iniciar un proceso de publicación de sus datos de gestión de residuos hacia la plataforma EcoDataNet. No realiza llamadas reales a ningún API; el proceso de publicación se simula en el frontend mediante una barra de progreso animada.
- **Ruta**: `/ecodatanet/publish`
- **Acceso**: `@attribute [Authorize]` — cualquier usuario autenticado.
- **Entidades / campos referenciados**:
  - `Users.PortalEDCProvider` → campo de solo lectura que identifica el conector EDC del participante (actualmente simulado con un valor mock constante).
- **Componentes de la pantalla**:
  | Elemento | Detalle |
  |---|---|
  | Bloque informativo | Texto descriptivo sobre EcoDataNet: soberanía del dato, interoperabilidad, trazabilidad y cumplimiento regulatorio. |
  | Diagrama de integración | Imagen estática `wwwroot/images/ecodatanet/integracion-greentransit-ecodatanet.png` que ilustra el flujo GreenTransit → Secure API / Data ingestion / HTTPS REST → EcoDataNet (Data Platform, Data Catalog, Observability). |
  | Campo "Conector EDC EcoDataNet del participante" | Solo lectura. Valor procedente de `Users.PortalEDCProvider` (mock: `https://edc.greentransit.example.com/connector`). |
  | Campo "API Key" | Solo lectura. GUID autogenerado en el frontend al cargar la pantalla (`Guid.NewGuid()`). Botón de regeneración disponible. |
  | Barra de progreso | Visible únicamente durante la simulación. Avanza de 0 % a 100 % en 20 pasos de 80 ms cada uno. |
  | Alerta de éxito | Aparece al completar el proceso: `"Proceso completado con éxito"`. |
  | Botón principal | `"Publicar datos en EcoDataNet"`. Deshabilitado mientras el proceso está en curso. |
- **Comportamiento del botón**:
  1. Inicia `_publishing = true` → deshabilita el botón.
  2. Itera 20 pasos con `Task.Delay(80 ms)` actualizando `_progress` (0 → 100 %).
  3. Al finalizar: `_publishing = false`, `_completed = true` → muestra alerta de éxito.
- **Ficheros**:
  - `src/GreenTransit.Web/Components/Pages/EcoDataNet/PublishData.razor`
  - `src/GreenTransit.Web/Components/Pages/EcoDataNet/PublishData.razor.css`
- **Menú lateral**: nuevo epígrafe colapsable **EcoDataNet** (icono `bi-broadcast`) con ítem hijo **Publicar Datos** (icono `bi-cloud-upload-fill`). Posicionado antes del epígrafe Seguridad en `NavMenu.razor`.
- **Estado**: ✅ IMPLEMENTADO (mock frontend) — pendiente de conectar con backend EDC real.

---

### 10.1. Listado de Declaraciones de Producción

- **Lógica**: vista paginada de todas las declaraciones del tenant, con filtros y acciones contextuales según estado y perfil.
- **Entidades**: `ProductDeclaration` ↔ `Entities` (IdProducer).
- **Ruta**: `/product-declarations`
- **Columnas del listado**:
  - `Reference` (referencia de la declaración).
  - Productor (`Entities.Name` vía `IdProducer`).
  - `Year` + `Period` (referencia a `dicProductDeclarationPeriods`).
  - `Type` (referencia a `dicProductDeclarationType`).
  - `State` (badge de color: BORRADOR=gris, EMITIDO=azul, VALIDADO=verde, RECHAZADO=rojo).
  - `Amount` (importe total formateado con `Currency`).
  - `DateCreate` / `DateEmit`.
  - Acciones: Ver / Editar / Emitir / Validar / Rechazar (según estado y perfil).
- **Filtros**:
  - Por `State` (multi-select).
  - Por `Year` y `Period`.
  - Por `IdProducer` (selector de entidades con `EntityRole=Producer`; oculto para PRODUCER, que ve solo los suyos).
  - Por `Type` (combo alimentado por `dicProductDeclarationType`).
  - Por rango de `DateCreate`.
- **Acciones masivas**: exportación CSV/XLSX del listado filtrado.
- **Roles**:
  - **ADMIN**: ve todas las declaraciones del tenant, puede crear/editar/validar/rechazar.
  - **PRODUCER**: ve solo las declaraciones donde `IdProducer` = su entidad vinculada. Puede crear y editar las que están en `BORRADOR` o `RECHAZADO`.
  - **SCRAP**: lectura de declaraciones vinculadas a productores de sus acuerdos.

#### ✅ Decisiones de implementación (UI)

| Aspecto | Comportamiento implementado |
|---|---|
| **Filtrado PRODUCER** | `GetProductDeclarationsQuery` filtra automáticamente `WHERE IdProducer = @LinkedEntityId` si el perfil es PRODUCER. El usuario no puede anular este filtro. |
| **Filtrado SCRAP** | Solo ve declaraciones cuyo `IdProducer` está adherido a alguno de sus `Agreements`. Filtro cruzado: `ProductDeclaration.IdProducer IN (SELECT IdProducer FROM ... WHERE Agreements.IdScrap = @LinkedEntityId)`. |
| **Creación rápida** | Botón "Nueva declaración" visible solo para ADMIN y PRODUCER. Para PRODUCER, `IdProducer` se asigna automáticamente. |

---

### 10.2. Formulario de Declaración de Producción (Cabecera)

- **Lógica**: formulario wizard de 2 pasos: (1) datos de cabecera, (2) líneas de producto. La cabecera se guarda primero en estado `BORRADOR`; las líneas se añaden/editan después.
- **Entidades**: `ProductDeclaration`, `Entities`, `dicProductDeclarationPeriods`, `dicProductDeclarationType`.
- **Ruta**: `/product-declarations/new` (alta) · `/product-declarations/{id}` (edición/detalle).

#### Paso 1 — Cabecera (`ProductDeclaration`)

| Campo UI | Campo BD | Tipo | Comportamiento |
|---|---|---|---|
| Productor | `IdProducer` | Selector → `Entities` (EntityRole=Producer) | Para PRODUCER: solo lectura, autocompletado con `LinkedEntityId`. Para ADMIN: selector libre. |
| Año | `Year` | Numérico (4 dígitos) | Obligatorio. Default: año actual. |
| Periodo | `Period` | Combo → `dicProductDeclarationPeriods` | Obligatorio. Opciones: Trimestral, Semestral, Anual… |
| Mes | `Month` | Combo 1-12 | Opcional. Solo visible si el periodo lo requiere (mensual). |
| Tipo | `Type` | Combo → `dicProductDeclarationType` | Obligatorio. |
| Moneda | `Currency` | Combo (EUR, USD…) | Default: EUR. |
| Referencia | `Reference` | Texto libre (256 chars) | Opcional. Referencia interna del productor. |
| Fecha creación | `DateCreate` | Datetime (readonly) | Se asigna automáticamente al crear. |
| Estado | `State` | Badge (readonly) | Se asigna como `BORRADOR` al crear. |

- **Validaciones de cabecera**:
  - `IdProducer` obligatorio.
  - `Year` + `Period` obligatorios.
  - **Unicidad**: no pueden existir dos declaraciones con el mismo `IdProducer` + `Year` + `Period` + `Type` en estado distinto de `RECHAZADO`. Validar en servidor.
  - `Type` obligatorio.

#### Paso 2 — Líneas de producto (`Products`)

- Se muestra como tabla editable inline debajo de la cabecera.
- Botón "Añadir línea" para insertar nuevas filas.

| Campo UI | Campo BD | Tipo | Comportamiento |
|---|---|---|---|
| Producto | `IdResidue` | Selector → `Residues` (ResidueType=Product) | Busqueda por `Name`, `Reference`, `ProductCategory`. Al seleccionar, se autocompleta `MeasureUnit` desde `Residues.DefaultMeasureUnit`. |
| Referencia | `Reference` | Texto libre (512 chars) | Opcional. Referencia específica de esta línea. |
| Fuente | `Source` | Combo → `dicProductDeclarationSource` | Opcional. |
| Cantidad | `Quantity` | Decimal (18,2) | Obligatorio. Validación > 0. |
| Unidad medida | `MeasureUnit` | Combo → valores estándar (kg, t, ud, l…) | Se inicializa desde `Residues.DefaultMeasureUnit`. Editable. |
| Unidades | `Units` | Entero | Opcional. Número de unidades físicas. |
| Precio | `Price` | Decimal (18,0) | Opcional. Precio unitario. |

- **Cálculo automático**: al cambiar líneas, se recalcula `ProductDeclaration.Amount` = Σ(`Products.Quantity × Products.Price`) para las líneas con precio informado.
- **Validaciones de líneas**:
  - Al menos 1 línea para poder emitir la declaración.
  - `IdResidue` obligatorio en cada línea (el producto debe existir en el catálogo `Residues` con `ResidueType=Product`).
  - `Quantity` > 0.
  - No duplicar `IdResidue` + `Source` en la misma declaración (warning, no bloqueo).

---

### 10.3. Flujo de Emisión y Validación

- **Lógica**: transiciones de estado controladas por el servicio de dominio. Cada transición registra `IdUser`, fecha y motivo (si aplica) en un log de auditoría.
- **Entidades**: `ProductDeclaration`, `DocStates`.

#### Emitir declaración (BORRADOR → EMITIDO)

- **Acción**: botón "Emitir" en el formulario de detalle.
- **Precondiciones**: al menos 1 línea en `Products`, todos los campos obligatorios de cabecera informados.
- **Efecto**:
  - `State` = `EMITIDO`.
  - `DateEmit` = `DateTime.UtcNow`.
  - La declaración pasa a solo lectura para el PRODUCER.
  - Se genera una notificación para el ADMIN.
- **Roles**: PRODUCER (sus declaraciones), ADMIN.

#### Validar declaración (EMITIDO → VALIDADO)

- **Acción**: botón "Validar" en el listado o detalle (solo visible en estado EMITIDO).
- **Precondiciones**: revisión manual por ADMIN (cantidades coherentes, productos existentes).
- **Efecto**:
  - `State` = `VALIDADO`.
  - La declaración queda definitivamente en solo lectura.
  - Se notifica al PRODUCER.
- **Roles**: ADMIN.

#### Rechazar declaración (EMITIDO → RECHAZADO)

- **Acción**: botón "Rechazar" con modal de motivo obligatorio.
- **Efecto**:
  - `State` = `RECHAZADO`.
  - Se almacena motivo de rechazo (campo `Reference` o campo de log).
  - Se notifica al PRODUCER con el motivo.
  - El PRODUCER puede editar y reemitir.
- **Roles**: ADMIN.

---

### 10.4. Detalle y vista 360° de la Declaración

- **Lógica**: vista consolidada de una declaración con toda su información: cabecera, líneas de producto con detalle del catálogo `Residues`, timeline de estados, y vinculación con eco-modulación si aplica.
- **Ruta**: `/product-declarations/{id}`
- **Secciones**:
  1. **Cabecera**: datos del productor (nombre, NIF, centro), periodo, tipo, estado con badge, importe total.
  2. **Líneas de producto**: tabla con columnas Producto (nombre + referencia del catálogo), Categoría (desde `Residues.ProductCategory`), Cantidad, Unidad, Unidades, Precio, Subtotal. Fila de totales al pie.
  3. **Timeline de estados**: stepper horizontal mostrando BORRADOR → EMITIDO → VALIDADO (o RECHAZADO), con fechas y usuario de cada transición.
  4. **Histórico de cambios**: log de quién editó qué y cuándo (usando `DateCreateSys` / `DateModifiedSys` / `IdUser`).
- **Exportación**: botón "Exportar PDF" con resumen de la declaración (cabecera + tabla de líneas + firma del productor). Botón "Exportar XLSX" con todas las líneas.
- **Roles**: PRODUCER (sus declaraciones), ADMIN (todas), SCRAP (lectura de las de sus productores adheridos).

---

### 10.5. Dashboard de Declaraciones (KPIs del módulo)

- **Lógica**: widgets específicos de declaraciones que se integran en el dashboard principal (§0.1) o en una sub-página dedicada `/product-declarations/dashboard`.
- **KPIs**:
  - **Declaraciones por estado**: donut chart con nº de declaraciones en BORRADOR / EMITIDO / VALIDADO / RECHAZADO.
  - **Volumen declarado por periodo**: bar chart con Σ `Products.Quantity` agrupado por `Year` + `Period`.
  - **Top 10 productos declarados**: ranking por `Σ Quantity` de las líneas `Products`, resolviendo el nombre vía `Residues.Name`.
  - **Productores sin declaración**: listado de `Entities` con `EntityRole=Producer` que NO tienen `ProductDeclaration` en el periodo actual.
  - **Importe total declarado**: card con `Σ ProductDeclaration.Amount` del periodo filtrado.
- **Filtros**: `Year`, `Period`, `IdProducer` (solo ADMIN), `Type`.
- **Roles**:
  - **ADMIN**: todos los KPIs.
  - **PRODUCER**: solo sus propios KPIs (volumen propio, estado de sus declaraciones).
  - **SCRAP**: KPIs de sus productores adheridos.

---

### 10.6. Importación masiva de declaraciones

- **Lógica**: carga de un fichero CSV/XLSX con múltiples líneas de producto para una declaración existente (o creando cabecera + líneas en una sola operación).
- **Formato esperado del fichero**:
  - Columnas: `ProductReference` (referencia en `Residues`), `Source`, `Quantity`, `MeasureUnit`, `Units`, `Price`.
  - El sistema busca `Residues` por `Reference` + `ResidueType=Product`. Si no encuentra el producto, marca la fila como error.
- **Validaciones**:
  - Formato correcto de cada columna.
  - Producto existente en catálogo `Residues`.
  - `Quantity` > 0.
  - Informe de errores fila a fila con descarga del fichero de errores.
- **Flujo**:
  1. El usuario sube el fichero en la pantalla de detalle de la declaración (o en la pantalla de alta).
  2. El sistema parsea, valida y muestra preview con semáforo por fila (verde=ok, rojo=error).
  3. El usuario confirma la importación de las filas válidas.
  4. Las filas se insertan en `Products` vinculadas a la `ProductDeclaration`.
- **Roles**: PRODUCER (su declaración en BORRADOR), ADMIN.

---

### 10.7. Gestión de Diccionarios de Declaración

- **Lógica**: mantenimiento CRUD de las tablas de referencia que alimentan los selectores del módulo de declaraciones.
- **Entidades**: `dicProductDeclarationCategory`, `dicProductDeclarationPeriods`, `dicProductDeclarationProducts`, `dicProductDeclarationSource`, `dicProductDeclarationType`, `dicProductDeclarationUse`.
- **Ruta**: `/admin/dictionaries/product-declarations`
- **Funciones**:
  - Listado con búsqueda por `Ref` y `description`.
  - Alta/edición/desactivación (nunca eliminación física para preservar integridad referencial).
  - En `dicProductDeclarationProducts`: selector de `CategoryId` → `dicProductDeclarationCategory` (relación FK interna).
- **Roles**: solo **ADMIN**.

---

## Actualización de la Matriz de Permisos (§8)

La siguiente fila se añade a la tabla de la sección 8:

| Funcionalidad | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| ProductDeclaration / Products | CRUD | R (adheridos) | CRUD (suyos) | – | – | – | – | R |
| dicProductDeclaration* (diccionarios) | CRUD | – | – | – | – | – | – | – |

---

## Actualización de la Matriz de Autorización (Mapa_Autorizacion)

### Pantalla nueva en §4 (matriz por pantalla)

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Declaraciones Producción** | `ProductDeclaration` / `Products` | **CRUD-P** | — | R-P | — | — | — | R | R | **CRUD** |
| **Diccionarios Declaración** | `dicProductDeclaration*` | — | — | — | — | — | — | — | — | **CRUD** |

### Policy de autorización nueva (§7.1)

```
Policy                          Perfiles permitidos
─────────────────────────────── ─────────────────────────────────────
CanManageProductDeclarations    PRODUCER (CRUD-P), ADMIN (CRUD)
CanValidateProductDeclarations  ADMIN
CanViewProductDeclarations      PRODUCER (propias), SCRAP (adheridos), COORDINATOR, DISPATCH_OFFICE, ADMIN
CanManageDeclarationDicts       ADMIN
```

### Filtro por datos propios

| Perfil | Campo de filtro | Lógica |
|---|---|---|
| `PRODUCER` | `ProductDeclaration.IdProducer` | Solo ve/edita declaraciones donde `IdProducer` = su entidad vinculada |
| `SCRAP` | `ProductDeclaration.IdProducer` cruzado con `Agreements.IdScrap` | Solo ve declaraciones de productores adheridos a sus acuerdos |

---

## Actualización del Checklist Técnico (§9)

Se añaden los siguientes ítems:

- [ ] Seed de diccionarios: `dicProductDeclarationCategory`, `dicProductDeclarationPeriods`, `dicProductDeclarationProducts`, `dicProductDeclarationSource`, `dicProductDeclarationType`, `dicProductDeclarationUse`.
- [ ] Seed de `DocStates` con estados: `Borrador`, `Emitido`, `Validado`, `Rechazado`.
- [ ] Servicio de dominio `ProductDeclarationStateService` para transiciones de estado.
- [ ] Notificaciones al cambiar estado (EMITIDO → notifica a ADMIN; VALIDADO/RECHAZADO → notifica a PRODUCER).
- [ ] Template de importación CSV/XLSX disponible para descarga.

*Documento generado para guiar la implementación con Copilot/Claude sobre el modelo GreenTransit v4.1.*
