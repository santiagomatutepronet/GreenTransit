# Prompt para GitHub Copilot — Servicio de Seed de Datos Sandbox GreenTransit

> **Objetivo**: Generar un servicio en .NET (C#) que inserte datos de demostración (sandbox) coherentes y completamente trazados en la base de datos GreenTransit v4.1 (SQL Server Azure). Todos los registros deben respetar las FKs declaradas, los discriminadores (`EntityRole`, `ResidueType`, `TreatmentOperations.Code`), las convenciones multi-tenant (`OwnerId`), la auditoría (`CreatedAt`/`UpdatedAt` o `DateCreateSys`/`DateModifiedSys`) y el versionado (`Version`, `Hash`).

---

## 1. Contexto del modelo de datos

La base de datos sigue el modelo relacional **GreenTransit v4.1** sobre SQL Server Azure. Las convenciones clave son:

- **PKs**: `uniqueidentifier` (GUID, `NEWSEQUENTIALID()`) en tablas operativas y económicas. `int IDENTITY` en catálogos, geografía y seguridad.
- **Multi-tenant**: campo `OwnerId` (GUID) en todas las tablas operativas. Todos los registros seed deben compartir un mismo `OwnerId` constante de demo (p.ej. `"00000000-0000-0000-0000-000000000001"`).
- **Auditoría**: `CreatedAt`/`UpdatedAt` (`datetime2(0)`, UTC) en tablas nuevas (Entities, LERCodes, Residues, TreatmentOperations, ServiceOrders, Agreements, Settlements, Incidents, etc.). `DateCreateSys`/`DateModifiedSys` (`datetime`) en tablas legacy (WasteMoves, EntryPlants, EntryCACs, TreatmentPlants).
- **Versionado**: `Version` (`int`, default 1), `Hash` (`nvarchar(128)`) en tablas que lo requieran.
- **`IdUser`**: `int`, FK lógica a `Users.ID`. Usar un usuario seed (p.ej. `ID = 1`).

### Discriminadores clave

| Campo | Tabla | Valores válidos |
|---|---|---|
| `EntityRole` | `Entities` | `Producer`, `OperatorTransfer`, `SCRAP`, `PublicEntity`, `Carrier`, `CAC`, `Plant`, `Coordinator`, `Other` |
| `ResidueType` | `Residues` | `Waste`, `Product`, `ProductSpec` |
| `Code` | `TreatmentOperations` | `R1`–`R13` (valorización), `D1`–`D15` (eliminación) — Directiva 2008/98/CE |

---

## 2. Orden de inserción (respetar dependencias FK)

El servicio **DEBE** insertar los datos en este orden estricto para que las FKs se satisfagan:

```
FASE 0 — Catálogos (si no existen ya)
  0.1  TreatmentOperations (R1–R13, D1–D15)
  0.2  LERCodes (al menos 15–20 códigos LER reales)
  0.3  EmissionFactorSets + EmissionFactors (1 set activo)

FASE 1 — Maestro de Entidades
  1.1  Entities (40 registros, ver desglose abajo)

FASE 2 — Maestro de Residuos
  2.1  Residues (ResidueType='Waste') — al menos 20 residuos vinculados a LERCodes y Producers
  2.2  Residues (ResidueType='Product') — al menos 15 para declaraciones de producción
  2.3  Residues (ResidueType='ProductSpec') — al menos 5 fichas técnicas

FASE 3 — Contratos
  3.1  Agreements (25 acuerdos)
  3.2  AgreementDocuments (2–3 documentos por acuerdo)

FASE 4 — Operación logística
  4.1  ServiceOrders (100)
  4.2  WasteMoves (100, uno por ServiceOrder)
  4.3  WasteMoveResidues (1–3 líneas por WasteMove, total ~150–200)

FASE 5 — Entradas y tratamiento
  5.1  EntryCACs (100) + EntryCACResidues (1–2 por entrada)
  5.2  EntryPlants (100) + EntryPlantResidues (1–2 por entrada)
  5.3  Incidents (15, vinculadas a ServiceOrders/WasteMoves existentes)
  5.4  TreatmentPlants (100) + TreatmentPlantResidues (1–3 por tratamiento)

FASE 6 — Economía y objetivos
  6.1  Settlements (25) + SettlementLines (3–5 por liquidación)
  6.2  MarketShares (varias combinaciones por SCRAP/categoría/CCAA para año actual)

FASE 7 — Declaraciones de producción
  7.1  ProductDeclaration (varias por cada uno de los 10 productores) + Products (líneas)

FASE 8 (opcional) — Usuarios seed
  8.1  Profiles (si no existen: ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE)
  8.2  Users (1 por entidad creada, vinculado al perfil correspondiente)
```

---

## 3. Especificación detallada por entidad

### 3.1 Entities (40 registros)

Insertar en la tabla `Entities` con los siguientes roles y cantidades:

| EntityRole | Cantidad | Convención de nombres | Notas |
|---|---|---|---|
| `Producer` | 10 | `PROD-01` a `PROD-10` (Name: "Productor Demo 01" ... "Productor Demo 10") | NIF simulado (B12345601..10), CenterCode NIMA ficticio, ciudades variadas de España (Zaragoza, Madrid, Barcelona, Sevilla, Valencia, Bilbao, Málaga, Murcia, Valladolid, Palma) |
| `OperatorTransfer` | 3 | `OT-01` a `OT-03` | Operadores de traslado, con InscriptionNumber |
| `SCRAP` | 5 | `SCRAP-01` a `SCRAP-05` (Ej: "EcoEnvases SCRAP", "ReciclaRAEE SCRAP", "GreenPack SCRAP", "EnviroPlas SCRAP", "MetalCiclo SCRAP") | Sistemas colectivos. Cada uno con WasteStream diferente (Envases, RAEE, Voluminosos, Plásticos, Metales) |
| `PublicEntity` | 5 | `PE-01` a `PE-05` | Ayuntamientos (EntityType='Ayuntamiento'), con MunicipalityCode real |
| `Carrier` | 5 | `CARR-01` a `CARR-05` | Transportistas con InscriptionNumber, vehículos variados |
| `CAC` | 5 | `CAC-01` a `CAC-05` | Centros de acopio ciudadano, con Latitude/Longitude reales de España |
| `Plant` | 5 | `PLANT-01` a `PLANT-05` | Plantas de tratamiento, con Latitude/Longitude, CenterCode NIMA |
| `Coordinator` | 1 | `COORD-01` | Coordinador de acuerdos |
| `Other` | 1 | `OFFICE-01` (Oficina de Asignación) | EntityRole='Other', actúa como oficina de despacho |

**Campos obligatorios por registro**:
```csharp
Id              = Guid.NewGuid(),   // o NEWSEQUENTIALID si usas SQL directo
Name            = "...",
NationalId      = "B12345601",      // NIF/CIF simulado, único por entidad
CenterCode      = "NIMA-XXXX",      // Código NIMA ficticio
EntityRole      = "Producer",       // según tabla anterior
CountryCode     = "ES",
StateCode       = "AR",             // variar por entidad (AR, MD, CT, AN, VC, PV, etc.)
ZipCode         = "50001",          // coherente con StateCode
ProvinceCode    = "50",
MunicipalityCode = "50297",         // INE real si es posible
Address         = "Calle Demo 1, Zaragoza",
Latitude        = "41.6488",
Longitude       = "-0.8891",
PhoneNumber     = "+34 976 000 001",
Email           = "demo01@greentransit.test",
ContactPerson   = "Juan Pérez Demo",
EntityType      = "SL",             // o "Ayuntamiento" para PublicEntity
IsActive        = true,
SourceSystem    = "SEED",
CreatedAt       = DateTime.UtcNow,
UpdatedAt       = DateTime.UtcNow,
IdUser          = 1
```

### 3.2 LERCodes (mínimo 15–20)

Insertar códigos LER reales de la Lista Europea de Residuos. Ejemplos:

| Code | Description | Chapter | IsDangerous | IsRAEE |
|---|---|---|---|---|
| `150101` | Envases de papel y cartón | 15 | false | false |
| `150102` | Envases de plástico | 15 | false | false |
| `150103` | Envases de madera | 15 | false | false |
| `150104` | Envases metálicos | 15 | false | false |
| `150107` | Envases de vidrio | 15 | false | false |
| `150110` | Envases con restos peligrosos (*) | 15 | true | false |
| `160211` | Equipos desechados con CFC, HCFC, HFC | 16 | true | true |
| `160213` | Equipos desechados con componentes peligrosos (*) | 16 | true | true |
| `160214` | Equipos desechados distintos de 16 02 09 a 16 02 13 | 16 | false | true |
| `170101` | Hormigón | 17 | false | false |
| `170201` | Madera | 17 | false | false |
| `170401` | Cobre, bronce, latón | 17 | false | false |
| `170405` | Hierro y acero | 17 | false | false |
| `200101` | Papel y cartón (municipal) | 20 | false | false |
| `200136` | Equipos eléctricos y electrónicos desechados (RAEE municipal) | 20 | false | true |
| `200301` | Mezclas de residuos municipales | 20 | false | false |

### 3.3 Residues

**ResidueType='Waste'** (≥20 registros):
- Vincular cada residuo a un `IdLERCode` existente y opcionalmente a un `IdProducer` (Entities con EntityRole='Producer').
- Campos: `Name`, `Description`, `ResidueType='Waste'`, `IdLERCode`, `IdProducer` (nullable), `IsActive=true`, `SourceSystem='SEED'`, `Version=1`.

**ResidueType='Product'** (≥15 registros):
- Productos declarados por productores. Vincular a `IdProducer`.
- Incluir campos de ecodiseño si aplica: `WeightPerUnitKg`, `ReparabilityIndex`, `RecycledContentPercent`, `MaterialsJson`.

**ResidueType='ProductSpec'** (≥5 registros):
- Fichas técnicas. Vincular a `IdProducer`.

### 3.4 TreatmentOperations

Si no existen ya, insertar las 28 operaciones estándar:
- **Valorización**: R1 (Utilización como combustible), R2, R3 (Reciclado materia orgánica), R4 (Reciclado metales), R5 (Reciclado materiales inorgánicos), R6–R13.
- **Eliminación**: D1 (Depósito), D2, D3, D4, D5 (Vertedero controlado), D6–D15.
- Campos: `Code`, `Description`, `IsRecycling`, `IsEnergyRecovery`, `IsPreparationForReuse`, `IsActive=true`.

### 3.5 Agreements (25)

```
Tabla: Agreements
```

Generar 25 acuerdos. Cada acuerdo vincula exactamente:
- `IdScrap` → una de las 5 entidades SCRAP
- `IdPublicEntity` → una de las 5 entidades PublicEntity
- `IdCoordinator` → la entidad Coordinator (COORD-01)

Distribución: 5 acuerdos por cada SCRAP (cada uno con una PublicEntity diferente).

**Campos clave**:
```csharp
AgreementNumber    = $"AGR-2026-{i:D4}",    // AGR-2026-0001 ... AGR-2026-0025
Status             = "Active",                // Mezclar: 20 Active, 3 Expired, 2 Draft
EffectiveFrom      = new DateTime(2026, 1, 1),
EffectiveTo        = new DateTime(2026, 12, 31),  // null para los Active indefinidos
WasteStream        = "Envases",               // variar según SCRAP
SubStream          = "Plástico",              // variar
AutonomousCommunity = "Aragón",               // variar coherente con PublicEntity
Currency           = "EUR",
TariffModelType    = "PorPeso",               // o "PorUnidad", "Mixto"
TariffRulesJson    = "{\"pricePerKg\": 0.15, \"minWeight\": 1000}",
MinimumsJson       = "{\"minMonthlyKg\": 500}",
ObligationsJson    = "{\"reportingFrequency\": \"Monthly\"}",
Version            = 1,
Hash               = ComputeSHA256(...),
IdUser             = 1
```

Para cada Agreement, crear 2–3 `AgreementDocuments`:
```csharp
AgreementId     = <Id del Agreement padre>,
DocumentType    = "Contrato" | "Anexo" | "Acta",
DocumentId      = $"DMS-DOC-{Guid.NewGuid():N}",
DocumentHash    = "sha256-placeholder-...",
SignedAt         = DateTime.UtcNow.AddDays(-30),
SignatureProvider = "VIDsigner"
```

### 3.6 ServiceOrders (100)

```
Tabla: ServiceOrders
```

**FKs requeridas**:
- `IdIssuedBy` → Entities (Producer o PublicEntity) — distribuir ~70% productores, ~30% entidades públicas
- `IdLERCode` → LERCodes
- `IdPickupPoint` → Entities (CAC, Producer, o PublicEntity según contexto)
- `IdCarrier` → Entities (Carrier) — puede ser null en las primeras SOs
- `IdPlannedPlant` → Entities (Plant)

**Campos clave**:
```csharp
ServiceOrderNumber  = $"SO-2026-{i:D5}",     // SO-2026-00001 ... SO-2026-00100
IssuedAt            = fechaAleatoria(2026),    // distribuidas a lo largo de ene–may 2026
Status              = "Active",                // Mezclar: Active, Completed, Cancelled
Priority            = "Normal",                // Mezclar: 80% Normal, 15% High, 5% Urgent
WasteStream         = "Envases",               // coherente con el LERCode elegido
SubStream           = "Plástico",
IdLERCode           = <GUID de un LERCode>,
IdPickupPoint       = <GUID de una Entity origen>,
PlannedPickupStart  = IssuedAt.AddDays(2),
PlannedPickupEnd    = IssuedAt.AddDays(3),
PlannedDeliveryStart = IssuedAt.AddDays(3),
PlannedDeliveryEnd  = IssuedAt.AddDays(4),
EstimatedWeight     = random(100, 5000),       // kg
MeasureUnit         = 1,                       // siempre Kg
IdCarrier           = <GUID Carrier>,
IdPlannedPlant      = <GUID Plant>,
Version             = 1
```

### 3.7 WasteMoves (100) + WasteMoveResidues (~150–200)

```
Tablas: WasteMoves, WasteMoveResidues
```

Un WasteMove por cada ServiceOrder. Cada WasteMove tiene 1–3 líneas WasteMoveResidues.

**WasteMoves — FKs**:
- `ServiceOrderId` → ServiceOrders.Id
- `IdSource` → Entities (el mismo IdPickupPoint de la SO, o Producer/CAC/PublicEntity)
- `IdDestination` → Entities (Plant o CAC)
- `IdScrap` → Entities (SCRAP) — heredar del acuerdo asociado lógicamente
- `IdScrap2` → null (o segunda SCRAP en ~10% de casos)
- `IdOperatorTransfer` → Entities (OperatorTransfer) — en ~30% de traslados

**Campos clave WasteMoves**:
```csharp
WasteMoveReference  = $"WM-2026-{i:D5}",
Lot                 = $"LOT-{i:D3}",
ServiceStatus       = "CLASIFICADO",  // Mezclar estados: SOLICITADO(10), PLANIFICADO(15), RECOGIDO(15), EN_CAC(10), EN_PLANTA(20), CLASIFICADO(30)
RequestDate         = <fecha de la SO>,
GatheredDate        = RequestDate.AddDays(3),
PlantEntryDate      = GatheredDate.AddDays(1),
PlannedPickupStart/End  = <copiar de SO>,
ActualPickupStart/End   = PlannedPickup + variación aleatoria ±2h,
ActualDeliveryStart/End = ActualPickup + 2–6h,
Version             = 1
```

**WasteMoveResidues — FKs por línea**:
- `IdWasteMove` → WasteMoves.Id
- `IdResidue` → Residues (ResidueType='Waste')
- `IdTreatmentOperationDestiny` → TreatmentOperations (obligatorio si residuo peligroso o RAEE)
- `IdCarrier` → Entities (Carrier)
- `EmissionFactorSetId` → EmissionFactorSets (si se ha calculado huella)

**Campos clave WasteMoveResidues**:
```csharp
Weight              = random(50, 3000),        // kg
MeasureUnit         = "Kg",
Units               = random(1, 50),
unitPriceKg         = random(0.05m, 0.50m),
NTNumber            = residuo.IsDangerous ? $"NT-2026-{n:D4}" : null,
DINumber            = residuo.IsDangerous ? $"DI-2026-{n:D4}" : null,
DIPhase             = residuo.IsDangerous ? "E1" : null,  // variar E1–E5
TransportInfo_vehicleRegistration = "1234 ABC",  // matrículas ficticias
TransportInfo_TransportDistance   = random(10, 300),  // km
TransportInfo_TransportDuration   = distance / 60,    // horas aprox
TransportInfo_TransportCarbonEmissions = distance * 0.9,  // kgCO2e aproximado
VehicleType         = "Camión 12t",            // variar
FuelType            = "Diesel",                // variar: Diesel, GNC, Eléctrico
EuroClass           = "Euro6"                  // variar: Euro4, Euro5, Euro6
```

### 3.8 EntryCACs (100) + EntryCACResidues

```
Tablas: EntryCACs, EntryCACResidues
```

**EntryCACs — campos**:
- `IdWasteMove` → referencia lógica al GUID del WasteMove (campo NOT NULL)
- `WasteMoveReference` → copiar de WasteMoves
- `ServiceOrderId` → NO EXISTE en esta tabla (a diferencia de EntryPlants)
- `CACEntryDate` → fecha coherente con el traslado
- `TypeContainer` → "Bigbag" | "Contenedor" | "Palé" | "Granel"
- `CollectionMethod` → "Puerta a puerta" | "Punto limpio" | "Contenedor vía pública"

**EntryCACResidues** (1–2 por entrada):
- `IdEntryCAC` → EntryCACs.Id
- `IdResidue` → Residues (ResidueType='Waste')
- `Weight` → random(20, 1500) kg
- `MeasureUnit` → "Kg"

### 3.9 EntryPlants (100) + EntryPlantResidues

```
Tablas: EntryPlants, EntryPlantResidues
```

**EntryPlants — FKs**:
- `IdWasteMove` → WasteMoves.Id (campo NOT NULL, relación lógica sin FK declarada)
- `ServiceOrderId` → ServiceOrders.Id (FK declarada)

**Campos clave**:
```csharp
WasteMoveReference  = <copiar de WasteMove>,
TicketScale         = $"TICKET-{i:D5}",
PlantEntryDate      = <fecha coherente con WasteMove.PlantEntryDate>,
GrossWeight         = random(100, 5000),
TareWeight          = random(50, 200),
NetWeight           = GrossWeight - TareWeight,  // calculado
WeighbridgeId       = "BASCULA-01",
TypeContainer       = "Contenedor"
```

**EntryPlantResidues** (1–2 por entrada):
- `IdEntryPlant` → EntryPlants.Id
- `IdResidue` → Residues (ResidueType='Waste')
- `Weight` → coherente con NetWeight del padre

### 3.10 TreatmentPlants (100) + TreatmentPlantResidues

```
Tablas: TreatmentPlants, TreatmentPlantResidues
```

**TreatmentPlants — FKs**:
- `ServiceOrderId` → ServiceOrders.Id
- `IdTreatmentOperation` → TreatmentOperations (la operación R/D realizada)
- `IncidentId` → Incidents.Id (null en la mayoría, vincular en ~5 casos)

**Campos clave**:
```csharp
IdWasteMove          = <GUID>,                   // relación lógica
WasteMoveReference   = <string>,
TicketScale          = $"TICKET-TREAT-{i:D5}",
PlantTreatmentDate   = PlantEntryDate.AddDays(1),
IdTreatmentOperation = <GUID de R3, R4, R5, D5, etc.>,
ImproperWeight       = random(0, 50),             // kg impropios
QualityMetricsJson   = "{\"contaminationPct\": 3.2, \"moisture\": 12.5}"
```

**TreatmentPlantResidues** — BALANCE DE MASAS OBLIGATORIO:
```csharp
IdTreatmentPlant = <padre>,
IdResidue        = <residuo entrante>,
WeightTotal      = 1000,              // peso total tratado
// Fracción reutilizada
IdResidueReused  = <GUID Residue de salida>,
WeightReused     = 150,
// Fracción valorizada
IdResidueValued  = <GUID Residue de salida>,
WeightValued     = 700,
// Fracción rechazo
IdResidueRemove  = <GUID Residue de salida>,
WeightRemove     = 150,
// VALIDAR: |WeightTotal - (WeightReused + WeightValued + WeightRemove) - ImproperWeight| < 1%
```

> ⚠️ **Importante**: `IdResidueReused`, `IdResidueValued` e `IdResidueRemove` son FKs separadas a `Residues`. Cada una apunta a un residuo/producto de salida diferente (con su propio LER de salida). Deben existir previamente en la tabla Residues.

### 3.11 Incidents (15)

```
Tabla: Incidents
```

Crear 15 incidencias vinculadas a ServiceOrders y WasteMoves existentes:

**Distribución de tipos y severidades**:
| Tipo | Cantidad | Severidades |
|---|---|---|
| `WeightMismatch` (descuadre de peso) | 4 | 2 Medium, 1 High, 1 Critical |
| `NonCompliantWaste` (residuo no conforme) | 3 | 1 Low, 1 Medium, 1 High |
| `TransportDelay` (retraso) | 3 | 2 Low, 1 Medium |
| `VehicleBreakdown` (avería) | 2 | 1 Medium, 1 High |
| `FractionContamination` (contaminación) | 2 | 1 High, 1 Critical |
| `MissingDocument` (documento faltante) | 1 | 1 Medium |

**Campos clave**:
```csharp
Type                 = "WeightMismatch",
Severity             = "High",           // Low | Medium | High | Critical
OpenedAt             = <fecha dentro del rango de traslados>,
ClosedAt             = <null para ~5 abiertas, fecha para ~10 cerradas>,
ServiceOrderId       = <GUID de una SO existente>,
WasteMoveReference   = <referencia del WasteMove asociado>,
ReportedByName       = "Operador Demo",
ReportedByNationalId = "12345678A",
Description          = "Descuadre de peso detectado en báscula...",
ResolutionJson       = ClosedAt != null ? "{\"action\":\"Repesaje\",\"notes\":\"Corregido\"}" : null,
Version              = 1
```

> Vincular ~5 de estas incidencias al campo `IncidentId` de `TreatmentPlants` (las de tipo WeightMismatch y FractionContamination).

### 3.12 Settlements (25) + SettlementLines

```
Tablas: Settlements, SettlementLines
```

**Settlements — FKs**:
- `AgreementId` → Agreements.Id (1 liquidación por acuerdo)
- `IdScrap` → Entities (SCRAP) — heredar del Agreement
- `IdPublicEntity` → Entities (PublicEntity) — heredar del Agreement

**Campos clave**:
```csharp
SettlementNumber = $"LIQ-2026-{i:D4}",
Status           = "Approved",         // Mezclar: 15 Approved, 5 Pending, 3 Rejected, 2 Draft
AgreementId      = <GUID>,
Year             = 2026,
Month            = random(1, 5),       // Ene–May 2026
Currency         = "EUR",
BaseAmount       = random(5000, 50000),
AdjustmentsAmount = random(-500, 500),
TaxAmount        = BaseAmount * 0.21m,
TotalAmount      = BaseAmount + AdjustmentsAmount + TaxAmount,
Version          = 1
```

**SettlementLines** (3–5 por liquidación):
```csharp
SettlementId    = <padre>,
ProductCategory = random(1, 5),
IdLERCode       = <GUID de LERCodes>,
WeightKg        = random(500, 10000),
PricePerKg      = random(0.05m, 0.30m),
Amount          = WeightKg * PricePerKg,
EvidenceType    = "TicketBascula",
SourceIdsJson   = "[\"<GUID EntryPlant 1>\", \"<GUID EntryPlant 2>\"]"
```

### 3.13 MarketShares (cuotas de mercado)

```
Tabla: MarketShares
```

Generar varias combinaciones para el año 2026:

- Por cada SCRAP (5) × por cada categoría relevante (Envases, RAEE, Voluminosos) × por cada CCAA (Aragón, Madrid, Cataluña) = ~45 registros.
- Variar también por `Period` (1=T1, 2=T2) y `FlowType` ("Recogida", "Reciclaje", "Valorización").

```csharp
IdScrap             = <GUID SCRAP>,
Category            = "Envases",
AutonomousCommunity = "Aragón",
Year                = 2026,
Weight              = random(10000, 500000),  // objetivo en kg
Period              = 1,                       // trimestre
EffectiveFrom       = new DateOnly(2026, 1, 1),
EffectiveTo         = new DateOnly(2026, 3, 31),
FlowType            = "Recogida",
Version             = 1
```

### 3.14 ProductDeclaration + Products

```
Tablas: ProductDeclaration, Products
```

Generar declaraciones de producción para **cada uno de los 10 productores**, con variedad de tipos y estados:

**ProductDeclaration** (~30–40 registros, 3–4 por productor):
```csharp
IdProducer  = <GUID Producer>,
Period      = 1,                   // 1=Trimestral, 2=Semestral, 4=Anual
Year        = 2026,
Month       = random(1, 5),
Currency    = "EUR",
State       = "Borrador",          // Mezclar: Borrador, Emitido, Validado, Rechazado
Type        = "DeclaraciónAnual",  // variar
DateCreate  = DateTime.Now,
DateEmit    = State != "Borrador" ? DateTime.Now.AddDays(-5) : null,
Reference   = $"DECL-{producer.CenterCode}-2026-{n:D2}",
Amount      = random(1000, 100000),
IdUser      = 1
```

**Products** (2–5 líneas por declaración):
```csharp
IdProductDeclaration = <padre>,
IdResidue            = <GUID Residues con ResidueType='Product'>,
Reference            = "REF-PROD-001",
Source               = "Producción propia",
Quantity             = random(100, 10000),
MeasureUnit          = "Kg",
Units                = random(10, 500),
Price                = random(1, 50)
```

---

## 4. Requisitos técnicos del servicio

### 4.1 Estructura del servicio

```csharp
namespace GreenTransit.Infrastructure.Persistence.Seeding
{
    public interface ISandboxDataSeeder
    {
        /// <summary>
        /// Ejecuta la inserción completa de datos sandbox en orden de dependencias FK.
        /// Idempotente: si ya existen datos con SourceSystem='SEED', no duplica.
        /// </summary>
        Task SeedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Elimina todos los datos sandbox (SourceSystem='SEED') en orden inverso de FK.
        /// </summary>
        Task CleanAsync(CancellationToken cancellationToken = default);
    }
}
```

### 4.2 Reglas de implementación

1. **Idempotencia**: antes de insertar, verificar si ya existen registros con `SourceSystem = 'SEED'`. Si existen, hacer skip o upsert.
2. **Transaccionalidad**: usar una transacción por fase. Si falla una fase, rollback de esa fase y log de error.
3. **GUIDs deterministas**: para facilitar el debugging, generar los GUIDs con un seed fijo (p.ej. `Guid.Parse($"00000000-0000-0000-{entityRole:X4}-{index:D12}")`) o usar `NEWSEQUENTIALID()` en SQL pero almacenar en variables para referenciarlos entre fases.
4. **Coherencia temporal**: todas las fechas deben caer entre enero y mayo de 2026. Los traslados deben tener progresión temporal lógica: `IssuedAt < PlannedPickup < ActualPickup < PlantEntry < Treatment`.
5. **Coherencia referencial**: cada WasteMove debe referenciar una ServiceOrder real. Cada EntryPlant debe referenciar un WasteMove y una ServiceOrder reales. Las Settlements deben referenciar Agreements activos. Los Incidents deben referenciar ServiceOrders existentes.
6. **Balance de masas**: en TreatmentPlantResidues, asegurar que `WeightReused + WeightValued + WeightRemove + ImproperWeight ≈ WeightTotal` (tolerancia <1%).
7. **Estados del traslado coherentes**: si `ServiceStatus = 'CLASIFICADO'`, todos los registros downstream deben existir (EntryPlant, TreatmentPlant). Si `ServiceStatus = 'PLANIFICADO'`, no debe haber EntryPlant ni TreatmentPlant para ese traslado.

### 4.3 Registro del servicio

```csharp
// En Program.cs o Startup.cs
services.AddScoped<ISandboxDataSeeder, SandboxDataSeeder>();

// Endpoint o command para ejecutar
app.MapPost("/api/admin/seed-sandbox", async (ISandboxDataSeeder seeder) =>
{
    await seeder.SeedAsync();
    return Results.Ok("Sandbox data seeded successfully");
}).RequireAuthorization("AdminOnly");
```

### 4.4 Logging

Cada fase debe loggar:
- Inicio/fin con conteo de registros insertados
- Tiempo de ejecución por fase
- Errores con detalle de la FK que falló (si aplica)

---

## 5. Resumen de volúmenes

| Tabla | Registros | Notas |
|---|---|---|
| Entities | 40 | 10 Prod + 3 OT + 5 SCRAP + 5 PE + 5 Carrier + 5 CAC + 5 Plant + 1 Coord + 1 Office |
| LERCodes | 15–20 | Códigos LER reales |
| TreatmentOperations | ~28 | R1–R13 + D1–D15 |
| Residues | ~40 | 20 Waste + 15 Product + 5 ProductSpec |
| EmissionFactorSets | 1 | + ~10 EmissionFactors |
| Agreements | 25 | + ~60 AgreementDocuments |
| ServiceOrders | 100 | |
| WasteMoves | 100 | |
| WasteMoveResidues | ~150–200 | 1–3 por WasteMove |
| EntryCACs | 100 | + ~150 EntryCACResidues |
| EntryPlants | 100 | + ~150 EntryPlantResidues |
| TreatmentPlants | 100 | + ~200 TreatmentPlantResidues |
| Incidents | 15 | |
| Settlements | 25 | + ~100 SettlementLines |
| MarketShares | ~45 | |
| ProductDeclaration | ~35 | + ~100 Products |
| **TOTAL** | **~1.400+** | |

---

## 6. Notas adicionales

- **No tocar tablas geográficas** (Country, TerritoryState, Province, Municipality): se asume que ya están pobladas con datos reales de España.
- **No tocar Profiles**: se asume que los 9 perfiles estándar ya existen (ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE).
- **Usuarios**: opcionalmente crear 1 usuario por entidad con el perfil correspondiente (Producer→PRODUCER, SCRAP→SCRAP, etc.).
- **EmissionFactorSets**: crear 1 set activo con ~10 factores (combinaciones de VehicleType × FuelType × EuroClass).
- **WasteStream/SubStream**: usar valores coherentes con los SCRAP. P.ej. SCRAP "EcoEnvases" → WasteStream="Envases"; SCRAP "ReciclaRAEE" → WasteStream="RAEE".
- **Consistencia de estados**: respetar la máquina de estados del traslado (`SOLICITADO → PLANIFICADO → RECOGIDO → EN_CAC → EN_PLANTA → CLASIFICADO`). No crear EntryPlants para traslados en estado SOLICITADO ni TreatmentPlants para traslados en estado RECOGIDO.
