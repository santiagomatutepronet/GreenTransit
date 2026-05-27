# 📖 Documentación Técnica Completa — GreenTransit (v4.1)

> Documento generado automáticamente a partir de las fuentes de verdad del proyecto:
> `Mapa_Funcionalidades.md`, `Crear_BD_v4_1.sql`, `COPILOT_CONTEXT.md`, `README.md`.
>
> **No modificar manualmente sin actualizar las fuentes originales.**

---

## 📋 Tabla de Contenidos

- [1. Visión General del Sistema](#1-visión-general-del-sistema)
  - [1.1 Propósito y Alcance](#11-propósito-y-alcance)
  - [1.2 Stack Tecnológico](#12-stack-tecnológico)
  - [1.3 Arquitectura](#13-arquitectura)
  - [1.4 Autenticación y Multi-Tenant](#14-autenticación-y-multi-tenant)
  - [1.5 Reglas Transversales](#15-reglas-transversales)
- [2. 📋 Catálogo de Entidades del Modelo de Datos](#2--catálogo-de-entidades-del-modelo-de-datos)
  - [2.1 Entities](#21-entities)
  - [2.2 LERCodes](#22-lercodes)
  - [2.3 Residues](#23-residues)
  - [2.4 TreatmentOperations](#24-treatmentoperations)
  - [2.5 WasteMoves](#25-wastemoves)
  - [2.6 WasteMoveResidues](#26-wastemoveresidues)
  - [2.7 ServiceOrders](#27-serviceorders)
  - [2.8 ServiceOrderResidues](#28-serviceorderresidues)
  - [2.9 Agreements](#29-agreements)
  - [2.10 AgreementDocuments](#210-agreementdocuments)
  - [2.11 Settlements](#211-settlements)
  - [2.12 SettlementLines](#212-settlementlines)
  - [2.13 EntryPlants / EntryPlantResidues](#213-entryplants--entryplantresidues)
  - [2.14 TreatmentPlants / TreatmentPlantResidues](#214-treatmentplants--treatmentplantresidues)
  - [2.15 EntryCACs / EntryCACResidues](#215-entrycacs--entrycacresidues)
  - [2.16 ProductDeclaration](#216-productdeclaration)
  - [2.17 Products](#217-products)
  - [2.18 ProductSpecs](#218-productspecs)
  - [2.19 EmissionFactorSets / EmissionFactors](#219-emissionfactorsets--emissionfactors)
  - [2.20 EcoModulationRuleSets / EcoModulationRules](#220-ecomodulationrulesets--ecomodulationrules)
  - [2.21 DUMZones / DUMRestrictionRules](#221-dumzones--dumrestrictionrules)
  - [2.22 PlantEnergies](#222-plantenergies)
  - [2.23 Incidents](#223-incidents)
  - [2.24 MarketShares](#224-marketshares)
  - [2.25 Profiles y Users](#225-profiles-y-users)
  - [2.26 PageDefinitions / PagePermissions](#226-pagedefinitions--pagepermissions)
  - [2.27 Geografía (Country → ZipCodes)](#227-geografía-country--zipcodes)
  - [2.28 Diccionarios (dic*)](#228-diccionarios-dic)
- [3. 🔗 Relaciones entre Entidades](#3--relaciones-entre-entidades)
  - [3.1 Diagrama conceptual de relaciones](#31-diagrama-conceptual-de-relaciones)
  - [3.2 Claves foráneas principales](#32-claves-foráneas-principales)
- [4. 🗂️ Módulos Funcionales](#4-️-módulos-funcionales)
  - [4.1 Módulo de Configuración y Maestros](#41-módulo-de-configuración-y-maestros)
  - [4.2 Módulo de Contratos, Liquidaciones y Cuotas](#42-módulo-de-contratos-liquidaciones-y-cuotas)
  - [4.3 Módulo Operativo — Traslados y Logística](#43-módulo-operativo--traslados-y-logística)
  - [4.4 Módulo de Incidencias](#44-módulo-de-incidencias)
  - [4.5 Módulo de Entradas a Planta y CAC](#45-módulo-de-entradas-a-planta-y-cac)
  - [4.6 Módulo de Declaraciones de Producto](#46-módulo-de-declaraciones-de-producto)
  - [4.7 Módulo de Sostenibilidad y DUM](#47-módulo-de-sostenibilidad-y-dum)
    - [4.7.4 Reglas de Ecomodulación](#474-reglas-de-ecomodulación)
  - [4.8 Módulo de Trazabilidad y KPIs](#48-módulo-de-trazabilidad-y-kpis)
  - [4.9 Módulo de Seguridad](#49-módulo-de-seguridad)
- [5. 🔄 Flujo Global de Operaciones](#5--flujo-global-de-operaciones)
  - [5.1 Ciclo de vida de un traslado](#51-ciclo-de-vida-de-un-traslado)
  - [5.2 Ciclo de vida de una orden de servicio](#52-ciclo-de-vida-de-una-orden-de-servicio)
  - [5.3 Flujo de declaración de producto (RAP)](#53-flujo-de-declaración-de-producto-rap)
- [6. 📊 Dashboards Analíticos](#6--dashboards-analíticos)
  - [6.1 Dashboard Home (Panel Principal)](#61-dashboard-home-panel-principal)
  - [6.2 Dashboards Logísticos](#62-dashboards-logísticos)
    - [6.2.1 Optimización Logística SCRAP](#621-optimización-logística-scrap)
    - [6.2.2 Monitorización Pública](#622-monitorización-pública)
    - [6.2.3 Panel Operativo](#623-panel-operativo)
  - [6.3 Dashboard Movilidad Urbana (UC3)](#63-dashboard-movilidad-urbana-uc3)
  - [6.4 Dashboards Mapas de Calor](#64-dashboards-mapas-de-calor)
  - [6.5 Dashboards Huella de Carbono](#65-dashboards-huella-de-carbono)
  - [6.6 Dashboards Análisis y Cumplimiento Normativo](#66-dashboards-análisis-y-cumplimiento-normativo)
  - [6.7 Dashboard de Declaraciones (RAP)](#67-dashboard-de-declaraciones-rap)
- [7. 🧮 Fórmulas y Cálculos Consolidados](#7--fórmulas-y-cálculos-consolidados)
- [8. 🔒 Seguridad, Perfiles y Filtrado de Datos](#8--seguridad-perfiles-y-filtrado-de-datos)
  - [8.1 Catálogo de perfiles](#81-catálogo-de-perfiles)
  - [8.2 Reglas de filtrado por perfil](#82-reglas-de-filtrado-por-perfil)
  - [8.3 Regla geográfica](#83-regla-geográfica)
  - [8.4 Autorización dinámica por pantalla](#84-autorización-dinámica-por-pantalla)
- [9. ⚙️ Configuración de Umbrales y Parámetros](#9-️-configuración-de-umbrales-y-parámetros)

---

# 1. Visión General del Sistema

## 1.1 Propósito y Alcance

**GreenTransit** es una aplicación web **multi-tenant (multi-empresa)** para la **gestión operativa, trazabilidad y control de residuos** en el ecosistema de la Responsabilidad Ampliada del Productor (RAP).

Cubre el ciclo completo del residuo:

```
Planificación → Ejecución → Pesaje → Tratamiento → Justificación Económica → Reporting Regulatorio
```

El sistema conecta todos los actores del ecosistema:

| Actor | Rol en el sistema |
|---|---|
| **Productores** | Generan residuos, declaran productos puestos en mercado |
| **Transportistas** | Ejecutan recogidas y traslados |
| **Gestores / SCRAP** | Supervisan operativa, gestionan acuerdos y liquidaciones |
| **Centros de Acopio (CAC)** | Reciben y clasifican residuos |
| **Plantas de tratamiento** | Tratan residuos (reciclaje, valorización, eliminación) |
| **Entidades Públicas** | Crean órdenes de servicio, revisan acuerdos municipales |
| **Coordinadores** | Lectura transversal del ámbito de sus acuerdos |
| **Oficina de Asignación** | Gestión logística integral, planificación de traslados |
| **Administración** | CRUD total, seguridad, configuración |

**Principios de diseño:**
- Portal transaccional, no sistema BI ni de optimización avanzada
- Multi-tenant con aislamiento estricto por `OwnerId`
- Trazabilidad auditable completa
- Base para cálculo económico y ambiental
- Preparado para data spaces EDC

## 1.2 Stack Tecnológico

| Componente | Tecnología |
|---|---|
| Runtime | .NET 10 |
| UI | Blazor Web App (Server) |
| ORM | EF Core |
| Base de datos | SQL Server (Azure) |
| Autenticación | OpenID Connect |
| Mediación | MediatR (CQRS) |
| Validación | FluentValidation |
| Logging | Serilog |
| Testing | xUnit |
| Gráficos | ApexCharts |
| Exportación | ClosedXML (XLSX) |

## 1.3 Arquitectura

El proyecto sigue **Clean Architecture** con 5 proyectos:

```
GreenTransit/
├── GreenTransit.Domain          ← Entidades, interfaces, value objects, PolicyConstants
├── GreenTransit.Application     ← CQRS (Queries/Commands/DTOs), Services, Validators
├── GreenTransit.Infrastructure  ← EF Core, DbContext, Repositorios, Servicios externos
├── GreenTransit.Web             ← Blazor Components, Pages, Layout, NavMenu
└── GreenTransit.Tests           ← xUnit, InMemory DB, tests de integración
```

**Módulos funcionalmente agrupados en el sistema de páginas:**

| Ruta base | Módulo |
|---|---|
| `/security`, `/users`, `/profiles` | Seguridad |
| `/traceability`, `/kpis`, `/documents` | Reporting |
| `/logistics/` | Dashboards Logísticos |
| `/incidents`, `/dum-zones`, `/emissions`, `/plant-energies` | Sostenibilidad |
| `/entities`, `/ler-codes`, `/residues`, `/treatment-operations` | Configuración |
| `/service-orders`, `/waste-moves`, `/entry-*`, `/treatment-plants` | Operaciones |
| `/agreements`, `/settlements`, `/market-shares` | Contratos y Liquidaciones |
| `/product-declarations` | Declaraciones de Producto |
| `/reporting/carbon-footprint/` | Huella de Carbono |
| `/reporting/regulatory-compliance/` | Cumplimiento Normativo |

## 1.4 Autenticación y Multi-Tenant

**Proveedor OIDC externo:**
- Authority: `https://pronet-identity-wst-app.azurewebsites.net/`
- Flujo: Authorization Code
- Sin almacenamiento de credenciales locales

**Mapeo de claims:**

| Claim OIDC | Campo interno | Uso |
|---|---|---|
| `sub` | `IdUser` | Identificador único del usuario en tablas operativas |
| `email` / `preferred_username` | `Users.Login` / `Users.Email` | Identificación visual |
| Claim organizativo (custom) | `Users.OwnerId` | Aislamiento multi-tenant |

**Flujo de autenticación:**

```
Usuario → Login → Servidor OIDC (Authority)
                      ↓ Authorization Code
         Intercambio por ID Token + Access Token
                      ↓
         ClaimsTransformation en backend:
           - sub → buscar Users.ID → IdUser
           - claim org → OwnerId
           - Users.IdProfile → cargar perfil y permisos
                      ↓
         CurrentUserService disponible en toda la app
```

**Comportamiento de sesión:**
- Sin credenciales locales; autenticación delegada al servidor OIDC
- Si una `Entity` se desactiva (`IsActive = 0`), el usuario vinculado queda bloqueado
- Todas las páginas requieren autenticación salvo la landing de login

## 1.5 Reglas Transversales

| Regla | Descripción |
|---|---|
| **Multi-tenant** | Todas las queries filtran por `OwnerId`. Sin acceso entre tenants. |
| **Auditoría** | Todas las tablas operativas tienen `CreatedAt`, `UpdatedAt`, `IdUser` |
| **PKs** | `uniqueidentifier` (GUID) en tablas operativas; `int IDENTITY` en catálogos |
| **Integridad** | Relaciones validadas en aplicación + FKs en BD |
| **Hash** | Usado en tablas críticas (`Agreements`, `WasteMoves`, etc.) para auditoría |
| **Geografía** | Siempre mostrar nombres (Province.Name, Municipality.Name), nunca códigos |
| **Filtros URL** | Filtros de dashboards persisten en query string para compartir enlaces |

---

# 2. 📋 Catálogo de Entidades del Modelo de Datos

La base de datos **greentransitdb** (SQL Server Azure) contiene **38 tablas** definidas en `Crear_BD_v4_1.sql` (versión 4.1).

## 2.1 Entities

**Propósito:** Tabla maestra de **todos los actores del ecosistema**. Punto de referencia transversal para todos los traslados, órdenes, acuerdos y liquidaciones.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único (NEWSEQUENTIALID) |
| `Name` | `nvarchar(256)` NOT NULL | Nombre o razón social |
| `NationalId` | `nvarchar(64)` | NIF / CIF / VAT |
| `CenterCode` | `nvarchar(256)` | Código NIMA u otro código de centro |
| `EntityRole` | `nvarchar(64)` NOT NULL | Rol: `Source`, `Destination`, `Carrier`, `OperatorTransfer`, `SCRAP`, `Producer`, `Plant`, `CAC`, `PublicEntity`, `Coordinator`, `Other` |
| `TypeThirdParty` | `nvarchar(256)` | Clasificación de tercero |
| `InscriptionType` | `nvarchar(64)` | Tipo de inscripción registral |
| `InscriptionNumber` | `nvarchar(256)` | Número de inscripción |
| `CountryCode` | `nvarchar(64)` | Código de país |
| `StateCode` | `nvarchar(64)` | Código CCAA |
| `ZipCode` | `nvarchar(64)` | Código postal |
| `ProvinceCode` | `nvarchar(256)` | Código de provincia |
| `MunicipalityCode` | `nvarchar(256)` | Código de municipio |
| `Address` | `nvarchar(512)` | Dirección completa |
| `Latitude` | `nvarchar(64)` | Latitud GPS |
| `Longitude` | `nvarchar(64)` | Longitud GPS |
| `PhoneNumber` | `nvarchar(64)` | Teléfono de contacto |
| `Email` | `nvarchar(256)` | Correo electrónico |
| `ContactPerson` | `nvarchar(256)` | Persona de contacto |
| `EconomicActivity` | `nvarchar(256)` | CNAE u otra clasificación |
| `EntityType` | `nvarchar(256)` | Tipo de entidad interno |
| `IsActive` | `bit` DEFAULT 1 | Activo/inactivo |
| `SourceSystem` | `nvarchar(64)` | Sistema origen (integraciones) |
| `CreatedAt` | `datetime2(0)` | Fecha creación (UTC) |
| `UpdatedAt` | `datetime2(0)` | Fecha última modificación (UTC) |
| `IdUser` | `int` | Usuario que creó/modificó |

**Índices:** `IX_Entities_NationalId`, `IX_Entities_EntityRole`, `IX_Entities_CenterCode`

**Regla clave:** No se duplican datos de actores en tablas operativas. Una misma empresa puede tener varios registros si desempeña distintos roles (`EntityRole`). Al crear una entidad, el sistema genera automáticamente un usuario vinculado con el perfil correspondiente.

---

## 2.2 LERCodes

**Propósito:** Catálogo oficial de **códigos LER** (Lista Europea de Residuos). Clasificación normativa de residuos.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `Code` | `nvarchar(16)` NOT NULL | Código LER (ej: `16 02 13*`) |
| `Description` | `nvarchar(512)` | Descripción oficial |
| `Chapter` | `nvarchar(8)` | Capítulo LER (2 dígitos) |
| `SubChapter` | `nvarchar(8)` | Subcapítulo LER (4 dígitos) |
| `IsDangerous` | `bit` | Residuo peligroso (marcado `*`) |
| `IsRAEE` | `bit` | Residuo de Aparatos Eléctricos y Electrónicos |
| `Name` | `nvarchar(256)` | Nombre corto |
| `Ref` | `nvarchar(128)` | Referencia interna |

**Referenciado por:** `Residues`, `ServiceOrders`, `ServiceOrderResidues`, `SettlementLines`, `WasteMoveResidues`

**Regla clave:** El código LER **no se repite como texto** en tablas operativas; siempre se referencia por FK.

---

## 2.3 Residues

**Propósito:** Tabla **central del modelo**. Catálogo maestro unificado de residuos, productos y fichas técnicas de ecodiseño.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant (multi-tenant) |
| `ResidueType` | `nvarchar(32)` NOT NULL | `Waste` / `Product` / `ProductSpec` |
| `Name` | `nvarchar(256)` | Nombre del residuo/producto |
| `Description` | `nvarchar(max)` | Descripción extendida |
| `IdLERCode` | `uniqueidentifier` | FK → `LERCodes` |
| `IsDangerous` | `bit` | Peligrosidad |
| `IsRAEE` | `bit` | ¿Es RAEE? |
| `IdProducer` | `uniqueidentifier` | FK → `Entities` (rol Producer) |
| `WeightPerUnitKg` | `decimal(18,4)` | Peso unitario en kg |
| `ReparabilityIndex` | `decimal(5,2)` | Índice de reparabilidad (0–10) |
| `RecycledContentPercent` | `decimal(5,2)` | % contenido reciclado |
| `MaterialsJson` | `nvarchar(max)` | JSON: composición de materiales |
| `DisassemblyEase` | `nvarchar(32)` | Facilidad de desmontaje |
| `ContainsHazardous` | `bit` | Contiene sustancias peligrosas |
| `PotentialLERCodesJson` | `nvarchar(max)` | JSON: LER potenciales al fin de vida |
| `ProductCategory` | `int` | FK → `dicProductDeclarationCategory` |
| `ProductUse` | `int` | FK → `dicProductDeclarationUse` |
| `SourceSystem` | `nvarchar(64)` | Sistema origen |
| `Version` | `int` DEFAULT 1 | Versión del registro |
| `Hash` | `nvarchar(128)` | Hash para auditoría |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha última modificación UTC |
| `IdUser` | `int` | Usuario creador/modificador |

**Índices:** `IX_Residues_ResidueType`, `IX_Residues_IdLERCode`, `IX_Residues_IdProducer`, `IX_Residues_ProductCat`, `IX_Residues_IsDangerous`

**Regla crítica:** Las tablas operativas **no describen residuos**, solo los referencian por FK `IdResidue`.

---

## 2.4 TreatmentOperations

**Propósito:** Catálogo normativo de **operaciones de tratamiento** (R/D según normativa europea).

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `Code` | `nvarchar(16)` | Código R/D (ej: `R3`, `D1`) |
| `Description` | `nvarchar(512)` | Descripción normativa |
| `Category` | `nvarchar(32)` | `Recovery` / `Disposal` / `Reuse` |
| `IsActive` | `bit` | Activo/inactivo |

**Referenciado por:** `WasteMoveResidues`, `TreatmentPlantResidues`, `EntryPlantResidues`

---

## 2.5 WasteMoves

**Propósito:** Entidad núcleo del **traslado de residuos**. Representa un traslado completo desde origen hasta destino.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `WasteMoveNumber` | `nvarchar(64)` NOT NULL | Número de traslado (único por tenant) |
| `WasteMoveReference` | `nvarchar(128)` | Referencia documental externa |
| `Status` | `nvarchar(32)` NOT NULL | Estado: `Draft`, `Planned`, `InTransit`, `Delivered`, `Completed`, `Cancelled` |
| `IdScrap` | `uniqueidentifier` | FK → `Entities` (rol SCRAP) |
| `IdScrap2` | `uniqueidentifier` | FK → `Entities` (segundo SCRAP) |
| `IdSource` | `uniqueidentifier` | FK → `Entities` (origen) |
| `IdDestination` | `uniqueidentifier` | FK → `Entities` (destino) |
| `IdOperatorTransfer` | `uniqueidentifier` | FK → `Entities` (gestor traslado) |
| `IdIssuedBy` | `uniqueidentifier` | FK → `Entities` (emisor) |
| `IdCarrier` | `uniqueidentifier` | FK → `Entities` (transportista) |
| `PlannedPickupStart` | `datetime2(0)` | Inicio recogida planificada |
| `PlannedPickupEnd` | `datetime2(0)` | Fin recogida planificada |
| `PlannedDeliveryStart` | `datetime2(0)` | Inicio entrega planificada |
| `PlannedDeliveryEnd` | `datetime2(0)` | Fin entrega planificada |
| `ActualPickupStart` | `datetime2(0)` | Inicio recogida real |
| `ActualPickupEnd` | `datetime2(0)` | Fin recogida real |
| `ActualDeliveryStart` | `datetime2(0)` | Inicio entrega real |
| `ActualDeliveryEnd` | `datetime2(0)` | Fin entrega real |
| `DINumber` | `nvarchar(64)` | Número Documento de Identificación |
| `NTNumber` | `nvarchar(64)` | Número Notificación de Traslado |
| `TicketScale` | `nvarchar(128)` | Ticket de báscula |
| `TransportDistanceKm` | `decimal(18,3)` | Distancia en km |
| `TransportDurationMin` | `int` | Duración en minutos |
| `VehicleRegistration` | `nvarchar(32)` | Matrícula vehículo |
| `VehicleType` | `nvarchar(32)` | Tipo de vehículo |
| `FuelType` | `nvarchar(32)` | Tipo de combustible |
| `EuroClass` | `nvarchar(16)` | Clase Euro del vehículo |
| `SourceSystem` | `nvarchar(64)` | Sistema origen |
| `Version` | `int` NOT NULL | Versión para control de concurrencia |
| `Hash` | `nvarchar(128)` | Hash de auditoría |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha última modificación UTC |
| `IdUser` | `int` | Usuario creador/modificador |

**Referenciado por:** `WasteMoveResidues`, `EntryPlants`, `EntryCACs`, `TreatmentPlants`, `Incidents`

---

## 2.6 WasteMoveResidues

**Propósito:** Tabla de **detalle por residuo trasladado**. Contiene datos de instancia del traslado, no del catálogo.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `IdWasteMove` | `uniqueidentifier` NOT NULL | FK → `WasteMoves` |
| `IdResidue` | `uniqueidentifier` | FK → `Residues` |
| `IdLERCode` | `uniqueidentifier` | FK → `LERCodes` |
| `IdTreatmentOperationDestiny` | `uniqueidentifier` | FK → `TreatmentOperations` (destino previsto) |
| `IdCarrier` | `uniqueidentifier` | FK → `Entities` (transportista real) |
| `IdEmissionFactorSet` | `uniqueidentifier` | FK → `EmissionFactorSets` |
| `WeightKg` | `decimal(18,3)` | Peso real en kg |
| `Units` | `int` | Número de unidades |
| `MeasureUnit` | `int` | Unidad de medida |
| `UnitPrice` | `decimal(18,4)` | Precio unitario |
| `TotalPrice` | `decimal(18,2)` | Precio total |
| `DINumber` | `nvarchar(64)` | DI del residuo |
| `NTNumber` | `nvarchar(64)` | NT del residuo |
| `DIPhase` | `nvarchar(32)` | Fase del DI |
| `EmissionKgCO2e` | `decimal(18,4)` | Emisiones calculadas (kg CO₂e) |
| `SortOrder` | `int` | Orden de línea |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha última modificación UTC |
| `IdUser` | `int` | Usuario creador/modificador |

---

## 2.7 ServiceOrders

**Propósito:** Ordena y planifica el servicio. Nexo entre planificación y ejecución logística.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `ServiceOrderNumber` | `nvarchar(64)` NOT NULL | Número de orden (único por tenant) |
| `Status` | `nvarchar(32)` NOT NULL | `Pending`, `Scheduled`, `InProgress`, `Completed`, `Cancelled` |
| `IdIssuedBy` | `uniqueidentifier` | FK → `Entities` (emisor de la SO) |
| `IdScrap` | `uniqueidentifier` | FK → `Entities` (SCRAP) |
| `Priority` | `nvarchar(16)` NOT NULL | Prioridad: `Low`, `Normal`, `High`, `Critical` |
| `WasteStream` | `nvarchar(32)` | Flujo de residuo |
| `SubStream` | `nvarchar(32)` | Sub-flujo |
| `ProductUse` | `int` | Uso del producto |
| `ProductCategory` | `int` | Categoría del producto |
| `IdLERCode` | `uniqueidentifier` | FK → `LERCodes` (clasificación principal) |
| `IdPickupPoint` | `uniqueidentifier` | FK → `Entities` (punto de recogida) |
| `PlannedPickupStart` | `datetime2(0)` | Inicio recogida planificada |
| `PlannedPickupEnd` | `datetime2(0)` | Fin recogida planificada |
| `PlannedDeliveryStart` | `datetime2(0)` | Inicio entrega planificada |
| `PlannedDeliveryEnd` | `datetime2(0)` | Fin entrega planificada |
| `EstimatedWeight` | `decimal(18,3)` | Peso estimado |
| `MeasureUnit` | `int` | Unidad de medida |
| `Units` | `int` | Número de unidades |
| `ContainersJson` | `nvarchar(max)` | JSON: contenedores |
| `IdCarrier` | `uniqueidentifier` | FK → `Entities` (transportista asignado) |
| `IdPlannedPlant` | `uniqueidentifier` | FK → `Entities` (planta planificada) |
| `WasteMoveReference` | `nvarchar(128)` | Referencia al traslado generado |
| `TicketScalePlanned` | `nvarchar(128)` | Ticket báscula planificado |
| `ActualPickupStart/End` | `datetime2(0)` | Ejecución real |
| `ActualDeliveryStart/End` | `datetime2(0)` | Entrega real |
| `TransportDistanceKm` | `decimal(18,3)` | Distancia km |
| `TransportDurationMin` | `int` | Duración min |
| `VehicleRegistration` | `nvarchar(32)` | Matrícula vehículo |
| `VehicleType` | `nvarchar(32)` | Tipo vehículo |
| `FuelType` | `nvarchar(32)` | Combustible |
| `EuroClass` | `nvarchar(16)` | Clase Euro |
| `SourceSystem` | `nvarchar(64)` | Sistema origen |
| `Version` | `int` NOT NULL | Versión |
| `Hash` | `nvarchar(128)` | Hash auditoría |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

**Estados válidos:** `Pending` → `Scheduled` → `InProgress` → `Completed` / `Cancelled`

**Regla:** Solo `Pending` y `Scheduled` permiten edición. La cabecera sincroniza sus campos de clasificación con la primera línea de `ServiceOrderResidues` (`SortOrder = 0`).

---

## 2.8 ServiceOrderResidues

**Propósito:** Tabla hija de `ServiceOrders`. Define **múltiples líneas de residuo** por orden.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `IdServiceOrder` | `uniqueidentifier` NOT NULL | FK → `ServiceOrders` |
| `SortOrder` | `int` | Orden de línea (0 = primera) |
| `IdLERCode` | `uniqueidentifier` | FK → `LERCodes` |
| `ProductUse` | `int` | Uso del producto |
| `ProductCategory` | `int` | Categoría del producto |
| `EstimatedWeight` | `decimal(18,3)` | Peso estimado |
| `MeasureUnit` | `int` | Unidad de medida |
| `Units` | `int` | Número de unidades |

**Regla:** Al actualizar una SO, las líneas se reemplazan íntegramente (`ExecuteDeleteAsync`) para evitar conflictos de concurrencia en Blazor Server.

---

## 2.9 Agreements

**Propósito:** Define **acuerdos marco** entre SCRAP, entidades públicas y coordinadores. Base para liquidaciones.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `AgreementNumber` | `nvarchar(64)` NOT NULL | Número de acuerdo |
| `Status` | `nvarchar(32)` NOT NULL | Estado del acuerdo |
| `EffectiveFrom` | `datetime2(0)` NOT NULL | Inicio vigencia |
| `EffectiveTo` | `datetime2(0)` | Fin vigencia (nullable) |
| `IdScrap` | `uniqueidentifier` | FK → `Entities` (SCRAP) |
| `IdPublicEntity` | `uniqueidentifier` | FK → `Entities` (entidad pública) |
| `IdCoordinator` | `uniqueidentifier` | FK → `Entities` (coordinador) |
| `WasteStream` | `nvarchar(32)` | Flujo de residuo cubierto |
| `SubStream` | `nvarchar(32)` | Sub-flujo |
| `AutonomousCommunity` | `nvarchar(64)` | CCAA de ámbito |
| `ProvinceCode` | `nvarchar(16)` | Provincia de ámbito |
| `MunicipalityCode` | `nvarchar(16)` | Municipio de ámbito |
| `CoveredMethodsJson` | `nvarchar(max)` | JSON: métodos de recogida cubiertos |
| `TariffModelType` | `nvarchar(64)` | Modelo tarifario |
| `Currency` | `nvarchar(8)` | Moneda |
| `TariffRulesJson` | `nvarchar(max)` | JSON: reglas tarifarias |
| `MinimumsJson` | `nvarchar(max)` | JSON: mínimos obligatorios |
| `ObligationsJson` | `nvarchar(max)` | JSON: obligaciones contractuales |
| `SourceSystem` | `nvarchar(64)` | Sistema origen |
| `Version` | `int` NOT NULL | Versión |
| `Hash` | `nvarchar(128)` | Hash auditoría |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.10 AgreementDocuments

**Propósito:** Documentos firmados asociados a un `Agreement`.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `AgreementId` | `uniqueidentifier` NOT NULL | FK → `Agreements` |
| `DocumentType` | `nvarchar(64)` NOT NULL | Tipo de documento |
| `DocumentId` | `nvarchar(128)` | Identificador externo del documento |
| `DocumentHash` | `nvarchar(128)` | Hash del documento |
| `SignedAt` | `datetime2(0)` | Fecha de firma |
| `SignatureProvider` | `nvarchar(64)` | Proveedor de firma |

---

## 2.11 Settlements

**Propósito:** **Liquidaciones económicas** por periodo. Vinculadas a un acuerdo.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `SettlementNumber` | `nvarchar(64)` NOT NULL | Número de liquidación |
| `Status` | `nvarchar(32)` NOT NULL | Estado de la liquidación |
| `AgreementId` | `uniqueidentifier` NOT NULL | FK → `Agreements` |
| `Year` | `int` NOT NULL | Año del periodo |
| `Month` | `int` | Mes del periodo (nullable = anual) |
| `IdScrap` | `uniqueidentifier` | FK → `Entities` (SCRAP) |
| `IdPublicEntity` | `uniqueidentifier` | FK → `Entities` (entidad pública) |
| `Currency` | `nvarchar(8)` NOT NULL | Moneda |
| `BaseAmount` | `decimal(18,2)` NOT NULL | Importe base |
| `AdjustmentsAmount` | `decimal(18,2)` NOT NULL | Ajustes |
| `TaxAmount` | `decimal(18,2)` NOT NULL | Impuestos |
| `TotalAmount` | `decimal(18,2)` NOT NULL | Total liquidado |
| `EvidenceRefsJson` | `nvarchar(max)` | JSON: referencias de evidencias |
| `Validator` | `nvarchar(64)` | Validador |
| `ValidationStatus` | `nvarchar(32)` | Estado de validación |
| `ValidatedAt` | `datetime2(0)` | Fecha de validación |
| `ValidationRef` | `nvarchar(128)` | Referencia de validación |
| `SourceSystem` | `nvarchar(64)` | Sistema origen |
| `Version` | `int` NOT NULL | Versión |
| `Hash` | `nvarchar(128)` | Hash auditoría |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.12 SettlementLines

**Propósito:** Desglose por línea de una liquidación (peso, LER, precio).

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `IdSettlement` | `uniqueidentifier` NOT NULL | FK → `Settlements` |
| `IdLERCode` | `uniqueidentifier` | FK → `LERCodes` |
| `WeightKg` | `decimal(18,3)` | Peso en kg |
| `Units` | `int` | Número de unidades |
| `UnitPrice` | `decimal(18,4)` | Precio unitario |
| `TotalPrice` | `decimal(18,2)` | Total de la línea |
| `WasteStream` | `nvarchar(32)` | Flujo de residuo |
| `SortOrder` | `int` | Orden de línea |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.13 EntryPlants / EntryPlantResidues

**Propósito:** Registro de **entrada en planta** y pesajes reales de residuos recibidos.

### EntryPlants (cabecera)

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `EntryNumber` | `nvarchar(64)` NOT NULL | Número de entrada |
| `Status` | `nvarchar(32)` NOT NULL | Estado |
| `IdWasteMove` | `uniqueidentifier` | FK → `WasteMoves` |
| `IdPlant` | `uniqueidentifier` | FK → `Entities` (planta) |
| `EntryDate` | `datetime2(0)` NOT NULL | Fecha de entrada |
| `TicketScale` | `nvarchar(128)` | Ticket de báscula |
| `TotalWeightKg` | `decimal(18,3)` | Peso total en kg |
| `Notes` | `nvarchar(max)` | Observaciones |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

### EntryPlantResidues (detalle)

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `IdEntryPlant` | `uniqueidentifier` NOT NULL | FK → `EntryPlants` |
| `IdResidue` | `uniqueidentifier` | FK → `Residues` |
| `IdLERCode` | `uniqueidentifier` | FK → `LERCodes` |
| `IdTreatmentOperation` | `uniqueidentifier` | FK → `TreatmentOperations` |
| `WeightKg` | `decimal(18,3)` | Peso real en kg |
| `Units` | `int` | Número de unidades |
| `SortOrder` | `int` | Orden de línea |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.14 TreatmentPlants / TreatmentPlantResidues

**Propósito:** Registro del **tratamiento aplicado en planta**. Modela fracciones reutilizadas, valorizadas y rechazos.

### TreatmentPlants (cabecera)

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `TreatmentNumber` | `nvarchar(64)` NOT NULL | Número de tratamiento |
| `Status` | `nvarchar(32)` NOT NULL | Estado |
| `IdEntryPlant` | `uniqueidentifier` | FK → `EntryPlants` |
| `IdPlant` | `uniqueidentifier` | FK → `Entities` (planta) |
| `TreatmentDate` | `datetime2(0)` NOT NULL | Fecha de tratamiento |
| `TotalInputWeightKg` | `decimal(18,3)` | Peso total de entrada |
| `Notes` | `nvarchar(max)` | Observaciones |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

### TreatmentPlantResidues (detalle por fracción)

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `IdTreatmentPlant` | `uniqueidentifier` NOT NULL | FK → `TreatmentPlants` |
| `IdResidue` | `uniqueidentifier` | FK → `Residues` (fracción tratada) |
| `IdLERCode` | `uniqueidentifier` | FK → `LERCodes` |
| `IdTreatmentOperation` | `uniqueidentifier` | FK → `TreatmentOperations` (operación aplicada) |
| `FractionType` | `nvarchar(32)` | `Reuse`, `Recycle`, `Valorize`, `Reject` |
| `WeightKg` | `decimal(18,3)` | Peso de la fracción en kg |
| `SortOrder` | `int` | Orden de línea |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.15 EntryCACs / EntryCACResidues

**Propósito:** Equivalente a planta para **Centros de Acopio (CAC)**. Registro de entradas y clasificación en CAC.

### EntryCACs (cabecera)

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `EntryNumber` | `nvarchar(64)` NOT NULL | Número de entrada |
| `Status` | `nvarchar(32)` NOT NULL | Estado |
| `IdWasteMove` | `uniqueidentifier` | FK → `WasteMoves` |
| `IdCAC` | `uniqueidentifier` | FK → `Entities` (CAC) |
| `EntryDate` | `datetime2(0)` NOT NULL | Fecha de entrada |
| `TicketScale` | `nvarchar(128)` | Ticket de báscula |
| `TotalWeightKg` | `decimal(18,3)` | Peso total |
| `Notes` | `nvarchar(max)` | Observaciones |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

### EntryCACResidues (detalle)

Misma estructura que `EntryPlantResidues` pero referenciada a `EntryCACs.Id`.

---

## 2.16 ProductDeclaration

**Propósito:** Cabecera de **declaraciones periódicas de producto** (RAP — Responsabilidad Ampliada del Productor).

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `Period` | `int` | Periodo de declaración |
| `Year` | `int` | Año de la declaración |
| `Month` | `int` | Mes (nullable = anual) |
| `Currency` | `nvarchar(256)` | Moneda |
| `State` | `nvarchar(64)` | Estado de la declaración |
| `DateCreate` | `datetime` | Fecha de creación |
| `DateEmit` | `datetime` | Fecha de emisión |
| `Reference` | `nvarchar(256)` | Referencia documental |
| `IdProducer` | `uniqueidentifier` | FK → `Entities` (productor) |
| `Amount` | `decimal(18,2)` | Importe declarado |
| `Type` | `nvarchar(256)` | Tipo de declaración |
| `DateCreateSys` | `datetime` | Fecha alta en sistema |
| `DateModifiedSys` | `datetime` | Fecha modificación en sistema |
| `IdUser` | `int` | Usuario |

---

## 2.17 Products

**Propósito:** Líneas de una declaración de producto. Datos de instancia de cada producto declarado.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `IdProductDeclaration` | `uniqueidentifier` NOT NULL | FK → `ProductDeclaration` |
| `IdResidue` | `uniqueidentifier` | FK → `Residues` (ResidueType='Product') |
| `Reference` | `nvarchar(512)` | Referencia interna |
| `Source` | `nvarchar(128)` | Origen |
| `ProductUse` | `nvarchar(128)` | Uso del producto |
| `ProductCategory` | `nvarchar(256)` | Categoría del producto |
| `Quantity` | `decimal(18,2)` | Cantidad |
| `MeasureUnit` | `nvarchar(64)` | Unidad de medida |
| `Units` | `int` | Número de unidades |
| `Price` | `decimal(18,0)` | Precio |

---

## 2.18 ProductSpecs

**Propósito:** Ficha técnica del producto. Información de ecodiseño y clasificación.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `ProductRef` | `nvarchar(128)` NOT NULL | Referencia única del producto |
| `IdResidue` | `uniqueidentifier` | FK → `Residues` (ResidueType='ProductSpec') |
| `ProductUse` | `int` | Uso del producto |
| `ProductCategory` | `int` | Categoría del producto |
| `CategoryRef` | `nvarchar(64)` | Referencia de categoría |
| `IdProducer` | `uniqueidentifier` | FK → `Entities` (productor) |
| `Notes` | `nvarchar(max)` | Notas adicionales |
| `Version` | `int` NOT NULL | Versión |
| `Hash` | `nvarchar(128)` | Hash auditoría |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.19 EmissionFactorSets / EmissionFactors

**Propósito:** Permite **versionar metodologías de emisiones** y recalcular el impacto ambiental de forma auditable.

### EmissionFactorSets

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `Name` | `nvarchar(128)` NOT NULL | Nombre del set de factores |
| `Version` | `nvarchar(32)` | Versión metodológica |
| `EffectiveFrom` | `datetime2(0)` | Inicio vigencia |
| `EffectiveTo` | `datetime2(0)` | Fin vigencia |
| `Source` | `nvarchar(256)` | Fuente metodológica |
| `IsActive` | `bit` | Activo/inactivo |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `IdUser` | `int` | Usuario |

### EmissionFactors

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `FactorSetId` | `uniqueidentifier` NOT NULL | FK → `EmissionFactorSets` |
| `FuelType` | `nvarchar(32)` | Tipo de combustible |
| `VehicleType` | `nvarchar(32)` | Tipo de vehículo |
| `EuroClass` | `nvarchar(16)` | Clase Euro |
| `kgCO2ePerKm` | `decimal(18,6)` | Factor de emisión (kg CO₂e/km) |
| `kgCO2ePerKg` | `decimal(18,6)` | Factor de emisión (kg CO₂e/kg) |
| `DistanceKm` | `decimal(18,3)` | Distancia referencia |

---

## 2.20 EcoModulationRuleSets / EcoModulationRules

**Propósito:** Modelo de **reglas de eco-modulación** para ajuste de tarifas según criterios ambientales.

### EcoModulationRuleSets

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `Name` | `nvarchar(128)` NOT NULL | Nombre del set de reglas |
| `Version` | `nvarchar(32)` | Versión normativa |
| `EffectiveFrom` | `datetime2(0)` | Inicio vigencia |
| `EffectiveTo` | `datetime2(0)` | Fin vigencia |
| `IsActive` | `bit` | Activo/inactivo |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `IdUser` | `int` | Usuario |

### EcoModulationRules

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `RuleSetId` | `uniqueidentifier` NOT NULL | FK → `EcoModulationRuleSets` |
| `CriteriaJson` | `nvarchar(max)` | JSON: criterios de aplicación |
| `EconomicImpact` | `decimal(18,4)` | Impacto económico (factor o importe) |
| `Description` | `nvarchar(512)` | Descripción de la regla |
| `SortOrder` | `int` | Orden de evaluación |

---

## 2.21 DUMZones / DUMRestrictionRules

**Propósito:** Zonas de **Distribución Urbana de Mercancías (DUM)** y sus reglas de restricción para análisis de movilidad urbana.

### DUMZones

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `Name` | `nvarchar(128)` NOT NULL | Nombre de la zona |
| `MunicipalityCode` | `nvarchar(16)` | FK geográfica → `Municipality` |
| `ProvinceCode` | `nvarchar(16)` | FK geográfica → `Province` |
| `ZoneType` | `nvarchar(32)` | Tipo de zona |
| `GeometryJson` | `nvarchar(max)` | JSON: geometría de la zona (polígono) |
| `IsActive` | `bit` | Activo/inactivo |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `IdUser` | `int` | Usuario |

### DUMRestrictionRules

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `ZoneId` | `uniqueidentifier` NOT NULL | FK → `DUMZones` |
| `VehicleType` | `nvarchar(32)` | Tipo de vehículo afectado |
| `MaxWeightKg` | `decimal(18,3)` | Peso máximo permitido |
| `TimeFrom` | `time` | Hora inicio restricción |
| `TimeTo` | `time` | Hora fin restricción |
| `DaysOfWeek` | `nvarchar(32)` | Días de la semana (JSON array) |
| `Description` | `nvarchar(512)` | Descripción de la restricción |

---

## 2.22 PlantEnergies

**Propósito:** Registro de **consumo energético** en plantas de tratamiento. Base para cálculo Scope 2 de huella de carbono.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `IdPlant` | `uniqueidentifier` NOT NULL | FK → `Entities` (planta) |
| `Year` | `int` NOT NULL | Año del consumo |
| `Month` | `int` NOT NULL | Mes del consumo |
| `ElectricityKwh` | `decimal(18,3)` | Consumo eléctrico en kWh |
| `GasM3` | `decimal(18,3)` | Consumo de gas en m³ |
| `OtherFuelLiters` | `decimal(18,3)` | Otros combustibles en litros |
| `Notes` | `nvarchar(max)` | Observaciones |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.23 Incidents

**Propósito:** Registro de **incidencias** durante traslados o en operaciones logísticas.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `IncidentNumber` | `nvarchar(64)` NOT NULL | Número de incidencia |
| `Status` | `nvarchar(32)` NOT NULL | Estado: `Open`, `InProgress`, `Resolved`, `Closed` |
| `IdWasteMove` | `uniqueidentifier` | FK → `WasteMoves` |
| `IdReportedBy` | `uniqueidentifier` | FK → `Entities` (entidad que reporta) |
| `IncidentType` | `nvarchar(64)` | Tipo de incidencia |
| `Severity` | `nvarchar(16)` | Severidad: `Low`, `Medium`, `High`, `Critical` |
| `Description` | `nvarchar(max)` | Descripción |
| `ResolutionNotes` | `nvarchar(max)` | Notas de resolución |
| `ReportedAt` | `datetime2(0)` NOT NULL | Fecha de reporte |
| `ResolvedAt` | `datetime2(0)` | Fecha de resolución |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.24 MarketShares

**Propósito:** Registro de **cuotas de mercado** por SCRAP, tipo de residuo y período. Base para cálculos de cumplimiento regulatorio RAP.

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `uniqueidentifier` PK | Identificador único |
| `OwnerId` | `uniqueidentifier` | Tenant |
| `IdScrap` | `uniqueidentifier` NOT NULL | FK → `Entities` (SCRAP) |
| `WasteStream` | `nvarchar(32)` NOT NULL | Flujo de residuo |
| `Year` | `int` NOT NULL | Año |
| `Month` | `int` | Mes (nullable = anual) |
| `MarketSharePercent` | `decimal(5,2)` NOT NULL | Cuota de mercado (%) |
| `MinRecyclingPercent` | `decimal(5,2)` | Mínimo de reciclaje requerido (%) |
| `MinReusePercent` | `decimal(5,2)` | Mínimo de reutilización requerido (%) |
| `MinValorizationPercent` | `decimal(5,2)` | Mínimo de valorización requerido (%) |
| `Notes` | `nvarchar(max)` | Notas |
| `CreatedAt` | `datetime2(0)` | Fecha creación UTC |
| `UpdatedAt` | `datetime2(0)` | Fecha modificación UTC |
| `IdUser` | `int` | Usuario |

---

## 2.25 Profiles y Users

### Profiles

| Campo | Tipo | Descripción |
|---|---|---|
| `ID` | `int IDENTITY` PK | Identificador único |
| `Reference` | `nvarchar(256)` NOT NULL UNIQUE | Código único del perfil |
| `Description` | `varchar(255)` | Descripción legible |
| `CreateDate` | `datetime` | Fecha de creación |

**Perfiles del sistema:**

| Reference | Descripción |
|---|---|
| `PRODUCER` | Productor / Generador de residuos |
| `CARRIER` | Transportista |
| `SCRAP` | Sistema Colectivo de Responsabilidad Ampliada |
| `PUBLIC_ENT` | Entidad Pública / Ayuntamiento |
| `CAC_OP` | Operador de Centro de Acopio |
| `PLANT_OP` | Operador de Planta de Tratamiento |
| `COORDINATOR` | Coordinador del acuerdo |
| `DISPATCH_OFFICE` | Oficina de Asignación / Gestor logístico |
| `ADMIN` | Administrador del sistema |

### Users

| Campo | Tipo | Descripción |
|---|---|---|
| `ID` | `int IDENTITY` PK | Identificador único |
| `Login` | `nvarchar(256)` NOT NULL UNIQUE | Login (índice UX_Users_Login) |
| `Email` | `nvarchar(256)` | Correo electrónico |
| `CompleteName` | `varchar(255)` | Nombre para mostrar |
| `IdProfile` | `int` NOT NULL | FK → `Profiles.ID` |
| `NationalId` | `int` | FK → `Country.id` |
| `GeographicalId` | `int` | FK → `TerritoryState.id` |
| `ZipCode` | `varchar(64)` | Código postal |
| `MunicipalityId` | `int` | FK → `Municipality.Id` |
| `Address` | `nvarchar(max)` | Dirección |
| `OwnerId` | `uniqueidentifier` | Tenant — filtro global |
| `PortalEDCProvider` | `nvarchar(max)` | Proveedor EDC |
| `LinkedEntityId` | `uniqueidentifier` | FK → `Entities.Id` (entidad vinculada al usuario) |
| `CreateDate` | `datetime` | Fecha de alta |

---

## 2.26 PageDefinitions / PagePermissions

**Propósito:** Sistema de **autorización dinámica por pantalla**, configurable desde la interfaz de administración sin cambios de código.

### PageDefinitions

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `int IDENTITY` PK | Identificador único |
| `Route` | `nvarchar(256)` NOT NULL UNIQUE | Ruta de la página (ej: `/service-orders`) |
| `PageName` | `nvarchar(128)` | Nombre legible en español |
| `Module` | `nvarchar(64)` | Módulo al que pertenece |
| `ComponentFullName` | `nvarchar(512)` | Nombre completo del componente Blazor |
| `CreatedAt` | `datetime2(0)` | Fecha de creación |

### PagePermissions

| Campo | Tipo | Descripción |
|---|---|---|
| `Id` | `int IDENTITY` PK | Identificador único |
| `PageDefinitionId` | `int` NOT NULL | FK → `PageDefinitions.Id` |
| `ProfileId` | `int` NOT NULL | FK → `Profiles.ID` |
| `AccessLevel` | `nvarchar(16)` NOT NULL | `Read`, `Write`, `Both` |
| `UpdatedAt` | `datetime2(0)` | Fecha de última modificación |
| `IdUser` | `int` | Usuario que configuró el permiso |

**Funcionamiento:** `PageDiscoveryService` escanea por reflexión todos los componentes Blazor con `[RouteAttribute]` al arrancar y sincroniza la tabla `PageDefinitions`. Los permisos se precargan con caché de 5 min en `IMemoryCache`.

---

## 2.27 Geografía (Country → ZipCodes)

Jerarquía geográfica completa:

```
Country → TerritoryState → Province → Municipality → MunicipalityZipCode
                                              ↓
                                    MunicipalityPopulation
```

| Tabla | PK | Campos clave |
|---|---|---|
| `Country` | `id int` | `code`, `name` |
| `TerritoryState` | `id int` | `idCountry`, `name`, `code` |
| `Province` | `id int` | `idState`, `name`, `code` |
| `Municipality` | `Id int` | `Id_Province`, `name`, `code` |
| `MunicipalityPopulation` | `Id int` | `IdMunicipality`, `Year`, `Population` |
| `MunicipalityZipCode` | `Id int` | `IdMunicipality`, `ZipCode` |

---

## 2.28 Diccionarios (dic*)

Tablas de catálogo inmutables con estructura común `(Id int, Ref varchar, description nvarchar)`:

| Tabla | Uso |
|---|---|
| `dicProductDeclarationCategory` | Categorías de declaración de producto |
| `dicProductDeclarationPeriods` | Períodos de declaración |
| `dicProductDeclarationProducts` | Productos declarables (con `CategoryId`) |
| `dicProductDeclarationSource` | Fuentes de declaración |
| `dicProductDeclarationType` | Tipos de declaración |
| `dicProductDeclarationUse` | Usos de producto declarado |
| `DocStates` | Estados de documento (`id`, `id_ref`, `name`) |

---

## 2.29 UserEDCConnector

**Propósito:** Almacena la configuración del conector EDC (Eclipse Dataspace Components) asociado a cada usuario. Relación 1:1 con `Users`.

| Campo | Tipo | Descripción |
|---|---|---|
| `ID` | `INT IDENTITY` | PK |
| `UserId` | `INT NOT NULL` | FK → `Users.ID` (índice único — un usuario tiene máx. un conector) |
| `EDCServerName` | `NVARCHAR(255)` | Nombre/URL base del servidor EDC (ej: `ecoucofiasignacion.ecodatanetconn3.dataspace.wastenode.com`) |
| `EDCConnectorId` | `NVARCHAR(255)` | Identificador único del conector dentro del servidor EDC |
| `ApiKey` | `NVARCHAR(255)` | API Key para la Management API (header `X-Api-Key`) |

**Relaciones:** `Users.ID` (1:1, cascade delete).

**Construcción de URLs:** Management API = `https://mgmt.{EDCServerName}/management`; Protocol API = `https://proto.{EDCServerName}/protocol`.

## 2.30 ProfileEDCConsumer

**Propósito:** Define qué perfiles pueden consumir datos de qué otros perfiles en el espacio de datos EcoDataNet. Relación N:M entre `Profiles`.

| Campo | Tipo | Descripción |
|---|---|---|
| `ID` | `INT IDENTITY` | PK |
| `ProfileId` | `INT NOT NULL` | FK → `Profiles.ID` — perfil que consume |
| `ConsumedProfileId` | `INT NOT NULL` | FK → `Profiles.ID` — perfil cuyos datos se consumen |

**Relaciones:** `Profiles.ID` × 2 (cascade en `ProfileId`, restrict en `ConsumedProfileId` para evitar ciclo). Índice único compuesto (`ProfileId`, `ConsumedProfileId`). Sin `OwnerId` (perfiles son globales).

---

# 3. 🔗 Relaciones entre Entidades

## 3.1 Diagrama conceptual de relaciones

```
                    ┌─────────────────┐
                    │    Entities     │ ◄── Todos los actores del ecosistema
                    └────────┬────────┘
                             │ (rol)
          ┌──────────────────┼──────────────────────┐
          │                  │                      │
    ┌─────▼─────┐    ┌───────▼──────┐    ┌─────────▼────────┐
    │WasteMoves │    │ServiceOrders │    │  Agreements       │
    └─────┬─────┘    └───────┬──────┘    └─────────┬────────┘
          │                  │                      │
    ┌─────▼──────────┐       │               ┌──────▼───────┐
    │WasteMoveResidues│      │               │  Settlements │
    └─────┬──────────┘       │               └──────┬───────┘
          │           ┌──────▼─────────┐            │
          │           │ServiceOrderRes.│    ┌────────▼───────┐
          │           └────────────────┘    │SettlementLines │
          │                                 └────────────────┘
    ┌─────▼─────┐
    │EntryPlants│ ──► EntryPlantResidues
    └─────┬─────┘
          │
    ┌─────▼────────┐
    │TreatmentPlants│ ──► TreatmentPlantResidues
    └──────────────┘

    EntryCACs ──► EntryCACResidues

    ProductDeclaration ──► Products ──► Residues (ResidueType='Product')
                          ProductSpecs ──► Residues (ResidueType='ProductSpec')

    Residues ──► LERCodes
    WasteMoveResidues ──► EmissionFactorSets ──► EmissionFactors

    DUMZones ──► DUMRestrictionRules
    EcoModulationRuleSets ──► EcoModulationRules

    Users ──► Profiles
    Users ──► Entities (LinkedEntityId)
    PageDefinitions ◄──► PagePermissions ──► Profiles
```

## 3.2 Claves foráneas principales

| Tabla origen | Campo FK | Tabla destino | Descripción |
|---|---|---|---|
| `WasteMoves` | `IdScrap`, `IdScrap2` | `Entities` | SCRAP responsable |
| `WasteMoves` | `IdSource` | `Entities` | Origen del traslado |
| `WasteMoves` | `IdDestination` | `Entities` | Destino del traslado |
| `WasteMoves` | `IdCarrier` | `Entities` | Transportista |
| `WasteMoves` | `IdOperatorTransfer` | `Entities` | Gestor del traslado |
| `WasteMoves` | `IdIssuedBy` | `Entities` | Emisor del traslado |
| `WasteMoveResidues` | `IdWasteMove` | `WasteMoves` | Traslado padre |
| `WasteMoveResidues` | `IdResidue` | `Residues` | Residuo del catálogo |
| `WasteMoveResidues` | `IdLERCode` | `LERCodes` | Código LER |
| `WasteMoveResidues` | `IdTreatmentOperationDestiny` | `TreatmentOperations` | Destino previsto |
| `WasteMoveResidues` | `IdEmissionFactorSet` | `EmissionFactorSets` | Factores de emisión |
| `ServiceOrders` | `IdIssuedBy` | `Entities` | Emisor de la SO |
| `ServiceOrders` | `IdPickupPoint` | `Entities` | Punto de recogida |
| `ServiceOrders` | `IdCarrier` | `Entities` | Transportista asignado |
| `ServiceOrders` | `IdPlannedPlant` | `Entities` | Planta planificada |
| `ServiceOrders` | `IdLERCode` | `LERCodes` | Clasificación LER |
| `ServiceOrderResidues` | `IdServiceOrder` | `ServiceOrders` | SO padre |
| `ServiceOrderResidues` | `IdLERCode` | `LERCodes` | Código LER de la línea |
| `Agreements` | `IdScrap` | `Entities` | SCRAP firmante |
| `Agreements` | `IdPublicEntity` | `Entities` | Entidad pública firmante |
| `Agreements` | `IdCoordinator` | `Entities` | Coordinador |
| `Settlements` | `AgreementId` | `Agreements` | Acuerdo asociado |
| `Settlements` | `IdScrap`, `IdPublicEntity` | `Entities` | Partes liquidación |
| `SettlementLines` | `IdSettlement` | `Settlements` | Liquidación padre |
| `SettlementLines` | `IdLERCode` | `LERCodes` | LER de la línea |
| `EntryPlants` | `IdWasteMove` | `WasteMoves` | Traslado origen |
| `EntryPlants` | `IdPlant` | `Entities` | Planta receptora |
| `EntryPlantResidues` | `IdEntryPlant` | `EntryPlants` | Entrada padre |
| `EntryPlantResidues` | `IdResidue` | `Residues` | Residuo recibido |
| `EntryPlantResidues` | `IdTreatmentOperation` | `TreatmentOperations` | Operación aplicada |
| `TreatmentPlants` | `IdEntryPlant` | `EntryPlants` | Entrada origen |
| `TreatmentPlantResidues` | `IdTreatmentPlant` | `TreatmentPlants` | Tratamiento padre |
| `TreatmentPlantResidues` | `IdResidue` | `Residues` | Fracción tratada |
| `TreatmentPlantResidues` | `IdTreatmentOperation` | `TreatmentOperations` | Operación aplicada |
| `EntryCACs` | `IdWasteMove` | `WasteMoves` | Traslado origen |
| `EntryCACs` | `IdCAC` | `Entities` | CAC receptor |
| `ProductDeclaration` | `IdProducer` | `Entities` | Productor declarante |
| `Products` | `IdProductDeclaration` | `ProductDeclaration` | Declaración padre |
| `Products` | `IdResidue` | `Residues` | Producto del catálogo |
| `ProductSpecs` | `IdResidue` | `Residues` | Ficha técnica |
| `ProductSpecs` | `IdProducer` | `Entities` | Productor |
| `Residues` | `IdLERCode` | `LERCodes` | Código LER |
| `Residues` | `IdProducer` | `Entities` | Productor |
| `EmissionFactors` | `FactorSetId` | `EmissionFactorSets` | Set de factores |
| `EcoModulationRules` | `RuleSetId` | `EcoModulationRuleSets` | Set de reglas |
| `DUMRestrictionRules` | `ZoneId` | `DUMZones` | Zona DUM |
| `Incidents` | `IdWasteMove` | `WasteMoves` | Traslado incidente |
| `Incidents` | `IdReportedBy` | `Entities` | Entidad reportante |
| `MarketShares` | `IdScrap` | `Entities` | SCRAP |
| `PlantEnergies` | `IdPlant` | `Entities` | Planta |
| `Users` | `IdProfile` | `Profiles` | Perfil del usuario |
| `Users` | `LinkedEntityId` | `Entities` | Entidad vinculada |
| `Users` | `MunicipalityId` | `Municipality` | Municipio del usuario |
| `PagePermissions` | `PageDefinitionId` | `PageDefinitions` | Pantalla |
| `PagePermissions` | `ProfileId` | `Profiles` | Perfil con acceso |
| `UserEDCConnector` | `UserId` | `Users` | Usuario propietario del conector (1:1, unique) |
| `ProfileEDCConsumer` | `ProfileId` | `Profiles` | Perfil que consume |
| `ProfileEDCConsumer` | `ConsumedProfileId` | `Profiles` | Perfil cuyos datos se consumen |
| `Municipality` | `Id_Province` | `Province` | Provincia |
| `Province` | `idState` | `TerritoryState` | CCAA |
| `TerritoryState` | `idCountry` | `Country` | País |
| `dicProductDeclarationProducts` | `CategoryId` | `dicProductDeclarationCategory` | Categoría |
| `UserSharePointCredentials` | `UserID` | `Users` | Usuario |

---

# 4. 🗂️ Módulos Funcionales

## 4.1 Módulo de Configuración y Maestros

**Ruta base:** `/entities`, `/ler-codes`, `/residues`, `/treatment-operations`
**Módulo:** Configuración

### 4.1.1 Gestión de Entidades

- **Propósito:** CRUD centralizado de todos los actores del ecosistema
- **Entidades:** `Entities`, `Users`, `Profiles`
- **Campos clave de filtrado:** `EntityRole`, `NationalId` (NIF/CIF/VAT), `CenterCode` (NIMA), `MunicipalityCode`, `IsActive`
- **Roles:** `ADMIN`, `DISPATCH_OFFICE` (alta restringida), `SCRAP` (alta restringida)
- **Provisión automática:** Al crear una `Entity`, el sistema crea automáticamente un `Users` vinculado con el perfil correspondiente:

| `EntityRole` | `Profiles.Reference` asignado |
|---|---|
| `SCRAP` | `SCRAP` |
| `Producer` | `PRODUCER` |
| `Carrier` / `OperatorTransfer` | `CARRIER` |
| `Plant` | `PLANT_OP` |
| `CAC` | `CAC_OP` |
| `PublicEntity` | `PUBLIC_ENT` |
| `Coordinator` | `COORDINATOR` |

- **Desactivación:** Si `IsActive = 0`, el usuario vinculado queda bloqueado (no eliminado)
- **Validaciones:** `EntityRole` determina en qué selectores aparece la entidad

### 4.1.2 Gestión de Códigos LER

- **Propósito:** Consulta y mantenimiento del catálogo oficial de residuos
- **Entidades:** `LERCodes`
- **Filtros:** `IsDangerous`, `IsRAEE`, `Chapter`, `SubChapter`
- **Roles:** `ADMIN`

### 4.1.3 Gestión de Residuos (Catálogo)

- **Propósito:** CRUD del catálogo maestro de residuos, productos y fichas de ecodiseño
- **Entidades:** `Residues`
- **Discriminador `ResidueType`:**
  - `Waste` → residuos operativos
  - `Product` → productos puestos en mercado (declaraciones RAP)
  - `ProductSpec` → fichas técnicas de ecodiseño
- **Campos de ecodiseño:** `ReparabilityIndex`, `RecycledContentPercent`, `MaterialsJson`, `DisassemblyEase`, `ContainsHazardous`, `PotentialLERCodesJson`
- **Roles:** `ADMIN`, `DISPATCH_OFFICE`, `PRODUCER` (limitado)

### 4.1.4 Gestión de Operaciones de Tratamiento

- **Propósito:** Catálogo normativo R/D para clasificación de tratamientos
- **Entidades:** `TreatmentOperations`
- **Categorías:** `Recovery` (R), `Disposal` (D), `Reuse`
- **Roles:** `ADMIN`

---

## 4.2 Módulo de Contratos, Liquidaciones y Cuotas

**Ruta base:** `/agreements`, `/settlements`, `/market-shares`
**Módulo:** Contratos y Liquidaciones

### 4.2.1 Gestión de Acuerdos

- **Propósito:** CRUD de acuerdos marco entre SCRAPs, entidades públicas y coordinadores
- **Entidades:** `Agreements`, `AgreementDocuments`
- **Ámbito territorial:** `AutonomousCommunity`, `ProvinceCode`, `MunicipalityCode`
- **Modelos tarifarios:** `TariffModelType` con reglas en `TariffRulesJson`
- **Versionado:** Campo `Version` + `Hash` para auditoría
- **Roles:** `SCRAP`, `COORDINATOR` (lectura), `ADMIN`
- **Filtrado por perfil:** `SCRAP` solo ve acuerdos donde figura como `IdScrap`; `COORDINATOR` solo los de `IdCoordinator`

### 4.2.2 Gestión de Liquidaciones

- **Propósito:** Registro y validación de liquidaciones económicas por período
- **Entidades:** `Settlements`, `SettlementLines`
- **Ciclo:** BaseAmount + AdjustmentsAmount + TaxAmount = TotalAmount
- **Validación:** campo `ValidationStatus`, `Validator`, `ValidatedAt`, `ValidationRef`
- **Roles:** `SCRAP`, `PUBLIC_ENT` (revisión), `COORDINATOR` (lectura), `ADMIN`

### 4.2.3 Gestión de Cuotas de Mercado

- **Propósito:** Registro de cuotas de mercado por SCRAP/año/flujo. Base para KPIs regulatorios RAP
- **Entidades:** `MarketShares`
- **Campos clave:** `MarketSharePercent`, `MinRecyclingPercent`, `MinReusePercent`, `MinValorizationPercent`
- **Roles:** `SCRAP`, `ADMIN`

---

## 4.3 Módulo Operativo — Traslados y Logística

**Ruta base:** `/service-orders`, `/waste-moves`
**Módulo:** Operaciones

### 4.3.1 Gestión de Órdenes de Servicio

- **Propósito:** Planificación y registro de servicios de recogida
- **Entidades:** `ServiceOrders`, `ServiceOrderResidues`
- **Estados válidos:** `Pending` → `Scheduled` → `InProgress` → `Completed` / `Cancelled`
- **Edición:** Solo posible en `Pending` y `Scheduled`
- **Líneas múltiples:** `ServiceOrderResidues` con líneas por residuo (`SortOrder`)
- **Sincronización:** La cabecera sincroniza su LER con la primera línea (`SortOrder = 0`)
- **Roles:** `PRODUCER`, `PUBLIC_ENT`, `DISPATCH_OFFICE`, `ADMIN`

### 4.3.2 Gestión de Traslados (WasteMoves)

Ciclo de vida completo del traslado:

| Paso | Estado | Actor | Acción |
|---|---|---|---|
| 1 | `SOLICITADO` | Gestor logístico | Crear WasteMove agrupando SOs |
| 2 | `PLANIFICADO` | Gestor logístico | Asignar transportista, vehículo, horarios; validar DUM |
| 3 | `RECOGIDO` | Transportista | Confirmar carga, registrar DI/NT, disparar cálculo CO₂ |
| 4 | `EN CAC` *(opcional)* | Operador CAC | Registrar entrada en centro de acopio |
| 5 | `EN PLANTA` | Operador planta | Pesar en báscula; NetWeight = GrossWeight − TareWeight |
| 6 | `TRATADO` | Operador planta | Registrar fracciones: Reuse/Recycle/Valorize/Reject |
| 7 | `COMPLETADO` | Sistema | Cierre del traslado, generación de evidencias |

**Cálculo de emisiones (Scope 1):**
```
CO₂e (kg) = TransportDistanceKm × EmissionFactor.kgCO₂ePerKm
```
- `EmissionFactor` seleccionado del `EmissionFactorSet` activo cruzando `VehicleType` + `FuelType` + `EuroClass`
- Resultado almacenado en `WasteMoveResidues.EmissionKgCO2e`

**Filtrado por perfil:**
- `SCRAP`: solo traslados donde `IdScrap = LinkedEntityId` OR `IdScrap2 = LinkedEntityId`
- `PRODUCER`: solo traslados de SOs emitidas por su entidad (`IdIssuedBy`)
- `CARRIER`: solo traslados donde es transportista (`IdCarrier`)
- `PLANT_OP`: solo traslados destinados a su planta (`IdPlannedPlant` o `IdDestination`)
- `CAC_OP`: solo traslados destinados a su CAC

---

## 4.4 Módulo de Incidencias

**Ruta base:** `/incidents`
**Módulo:** Sostenibilidad

- **Propósito:** Registro y gestión de incidencias durante traslados
- **Entidades:** `Incidents`
- **Estados:** `Open` → `InProgress` → `Resolved` → `Closed`
- **Severidad:** `Low`, `Medium`, `High`, `Critical`
- **Disparo automático:** Descuadre de peso en planta (>X%) genera incidencia automática
- **Roles:** Todos los perfiles pueden ver incidencias de su ámbito; `DISPATCH_OFFICE` y `ADMIN` pueden resolver

---

## 4.5 Módulo de Entradas a Planta y CAC

**Ruta base:** `/entry-plants`, `/entry-cacs`, `/treatment-plants`
**Módulo:** Operaciones

### 4.5.1 Entradas en Planta (EntryPlants)

- **Propósito:** Registro de recepción y pesaje en planta de tratamiento
- **Entidades:** `EntryPlants`, `EntryPlantResidues`
- **Peso oficial:** `NetWeight = GrossWeight − TareWeight` (calculado en backend, no editable)
- **Alertas:** Descuadre ≥ umbral vs. `WasteMoveResidues.WeightKg` → incidencia automática
- **Roles:** `PLANT_OP`, `ADMIN`

### 4.5.2 Tratamiento en Planta (TreatmentPlants)

- **Propósito:** Registro del tratamiento aplicado a cada fracción de residuo
- **Entidades:** `TreatmentPlants`, `TreatmentPlantResidues`
- **Fracciones:** `Reuse`, `Recycle`, `Valorize`, `Reject`
- **KPIs generados:** Tasa de reciclaje, valorización, eliminación
- **Roles:** `PLANT_OP`, `ADMIN`

### 4.5.3 Entradas en CAC (EntryCACs)

- **Propósito:** Equivalente a planta para Centros de Acopio
- **Entidades:** `EntryCACs`, `EntryCACResidues`
- **Roles:** `CAC_OP`, `ADMIN`

---

## 4.6 Módulo de Declaraciones de Producto

**Ruta base:** `/product-declarations`
**Módulo:** Declaraciones de Producto

- **Propósito:** Gestión del ciclo completo de declaraciones RAP del productor
- **Entidades:** `ProductDeclaration`, `Products`, `ProductSpecs`, `Residues` (ResidueType=Product/ProductSpec)
- **Ciclo:**
  1. Productor crea `ProductDeclaration` (cabecera por período/año)
  2. Añade líneas `Products` referenciando el catálogo `Residues`
  3. Las fichas técnicas se mantienen en `ProductSpecs` → `Residues` (ResidueType=ProductSpec)
- **Estados:** gestionados mediante `dicProductDeclarationType` y `DocStates`
- **Roles:** `PRODUCER`, `ADMIN`
- **Eco-modulación:** `EcoModulationRuleSets` / `EcoModulationRules` aplican ajustes tarifarios según criterios de ecodiseño del producto

---

## 4.7 Módulo de Sostenibilidad y DUM

**Ruta base:** `/dum-zones`, `/emissions`, `/plant-energies`
**Módulo:** Sostenibilidad

### 4.7.1 Zonas DUM

- **Propósito:** Definición de zonas de Distribución Urbana de Mercancías con restricciones
- **Entidades:** `DUMZones`, `DUMRestrictionRules`
- **Geometría:** `GeometryJson` define el polígono de la zona
- **Validación:** Al planificar un traslado, el sistema cruza el punto de recogida contra las zonas DUM activas
- **Roles:** `ADMIN`, `DISPATCH_OFFICE`

### 4.7.2 Factores de Emisión

- **Propósito:** Gestión de sets de factores de emisión versionados
- **Entidades:** `EmissionFactorSets`, `EmissionFactors`
- **Criterios de selección:** `VehicleType` + `FuelType` + `EuroClass` + set activo más reciente
- **Roles:** `ADMIN`

### 4.7.3 Consumo Energético en Planta

- **Propósito:** Registro mensual de consumo eléctrico, gas y otros combustibles en plantas
- **Entidades:** `PlantEnergies`
- **Base para:** Cálculo Scope 2 de huella de carbono (electricidad)
- **Roles:** `PLANT_OP`, `ADMIN`

### 4.7.4 Reglas de Ecomodulación ✅ IMPLEMENTADO

**Ruta:** `/ecomodulation-rule-sets`
**Módulo:** Sostenibilidad

#### ¿Qué es la ecomodulación?

Mecanismo por el que las tarifas RAP (**Responsabilidad Ampliada del Productor**) se **ajustan al alza o a la baja** según criterios de ecodiseño del producto. La normativa obliga a los SCRAP a aplicar este ajuste al calcular las liquidaciones de sus productores adheridos. Un producto con mejor diseño ambiental paga menos; uno con peor diseño paga más.

#### Entidades implicadas

```
EcoModulationRuleSets          ← cabecera del conjunto (versionado, vigencia, estado)
    └── EcoModulationRules     ← reglas individuales (criterio JSON + impacto económico)
           ↑
     se evalúan contra
           ↓
ProductDeclarations → Products ← productos declarados por el Productor
           ↓
       Settlements             ← AdjustmentsAmount recoge el ajuste resultante
```

| Entidad | Campos clave | Descripción |
|---|---|---|
| `EcoModulationRuleSet` | `Id`, `OwnerId`, `RuleSetName`, `Version`, `Status` (`Active`/`Inactive`), `ValidFrom`, `ValidTo`, `PublisherName`, `PublisherNationalId`, `PublisherCenterCode`, `Hash` | Contenedor versionado de reglas. Solo puede haber **uno activo** simultáneamente; activar uno desactiva el resto automáticamente |
| `EcoModulationRule` | `Id`, `RuleSetId`, `RuleCode`, `ProductCategory` (int), `CriteriaJson`, `FeeImpactType` (`None`/`Reduction`/`Surcharge`), `FeeImpactValue` (decimal) | Regla individual. El `CriteriaJson` define los criterios de aplicación |

#### ¿Quién publica los conjuntos de reglas?

El **SCRAP** es el actor que administra y publica los conjuntos de reglas. En la aplicación, solo el perfil **ADMIN** puede crear, editar y activar conjuntos. El campo `PublisherName / PublisherNationalId / PublisherCenterCode` identifica la entidad publicadora.

#### Tipos de impacto económico

| `FeeImpactType` | Efecto | Ejemplo |
|---|---|---|
| `None` | Sin ajuste | 0 % → tarifa base |
| `Reduction` | Bonificación (descuento) | −10 % → producto bien diseñado |
| `Surcharge` | Recargo | +15 % → producto difícil de reciclar |

#### Criterios de aplicación (`CriteriaJson`)

Cada regla almacena sus criterios en JSON libre. Ejemplo mínimo:

```json
{ "productCategory": "RAEE", "minWeightKg": 100 }
```

Los criterios se evalúan en la capa Application al calcular la liquidación. La estructura es extensible sin cambios de esquema.

#### Flujo de integración completo

```
1. ADMIN       →  crea y activa un EcoModulationRuleSet en /ecomodulation-rule-sets
                     (activar uno desactiva automáticamente los demás)

2. PRODUCER    →  crea una ProductDeclaration con líneas de Products
                     (cada línea tiene ProductCategory como clave de matching)

3. Sistema     →  evalúa las EcoModulationRules del set activo
                     matching: ProductCategory + criterios del CriteriaJson

4. Sistema     →  calcula AdjustmentsAmount
                     Fórmula: BaseAmount × FeeImpactValue / 100

5. SCRAP       →  valida la declaración y genera Settlement
                     TotalAmount = BaseAmount + AdjustmentsAmount + TaxAmount

6. PRODUCER    →  recibe la liquidación con el ajuste de ecomodulación reflejado
```

#### Archivos implementados

| Archivo | Descripción |
|---|---|
| `Application/Features/Ecomodulation/DTOs/EcoModulationRuleSetDtos.cs` | DTOs: `EcoModulationRuleSetDto` (lista), `EcoModulationRuleSetDetailDto` (detalle con reglas), `EcoModulationRuleDto`, `EcoModulationRuleLineDto` (formulario) |
| `Application/Features/Ecomodulation/Queries/GetEcoModulationRuleSetsQuery.cs` | Listado paginado filtrado por estado; `GetEcoModulationRuleSetByIdQuery` para detalle |
| `Application/Features/Ecomodulation/Commands/EcoModulationRuleSetCommands.cs` | `CreateEcoModulationRuleSetCommand`, `UpdateEcoModulationRuleSetCommand`, `ActivateEcoModulationRuleSetCommand` (desactiva el resto), `DeleteEcoModulationRuleSetCommand` (solo si no está activo) |
| `Web/Components/Pages/Ecomodulation/EcoModulationRuleSetList.razor` | Página master-detail: lista de conjuntos a la izquierda, reglas del conjunto seleccionado a la derecha. Modal de creación/edición con líneas de reglas editables en tabla |
| `Infrastructure/Services/PageDiscoveryService.cs` | Ruta `/ecomodulation-rule-sets` mapeada al módulo "Sostenibilidad"; nombre humanizado "Reglas de Ecomodulación" |

#### Modelo de autorización

- `@attribute [Authorize]` — requiere autenticación; acceso controlado desde `/security/page-permissions`
- `IPagePermissionService.CanWriteRouteAsync("/ecomodulation-rule-sets")` — controla visibilidad de botones de escritura
- Igual que el resto de módulos: sin policies hardcodeadas, gestionado dinámicamente por la ventana de permisos por pantalla

#### Aparición en dashboards

| Dashboard | Widget | Descripción |
|---|---|---|
| **RAP** (`/product-declarations`) | Aplicación de eco-modulación | Reglas aplicadas y ajuste económico resultante por declaración |
| **Normativo** | Panel timeline cambios normativos | Cambios recientes en `EcoModulationRuleSets` |
| **Panel SCRAP** | Vista económica agregada | Impacto económico acumulado por categoría de producto |

- **Roles:** lectura → `SCRAP`, `ADMIN`; escritura → `ADMIN`

---

## 4.8 Módulo de Trazabilidad y KPIs

**Ruta base:** `/traceability`, `/kpis`, `/documents`
**Módulo:** Reporting

### 4.8.1 Trazabilidad

- **Propósito:** Búsqueda y consulta del historial completo de un residuo o traslado
- **Búsqueda global:** `ServiceOrderNumber`, `WasteMoveReference`, `TicketScale`, `DINumber`, `NTNumber`, `AgreementNumber`, `Entities.Name` / `NationalId` / `CenterCode`
- **Implementado:** `GlobalSearchQuery.cs` + `GlobalSearchBar.razor` en `MainLayout.razor` (debounce 300ms)

### 4.8.2 KPIs Regulatorios

- **Propósito:** Cálculo de indicadores de cumplimiento normativo RAP
- **KPIs principales:**
  - Tasa de reciclaje por SCRAP/período/flujo
  - Tasa de valorización
  - Tasa de eliminación
  - Cumplimiento de cuotas `MarketShares`
- **Entidades:** `TreatmentPlantResidues`, `MarketShares`, `WasteMoveResidues`

---

## 4.9 Módulo de Seguridad

**Ruta base:** `/security`, `/users`, `/profiles`
**Módulo:** Seguridad

### 4.9.1 Gestión de Usuarios

- **Propósito:** CRUD de usuarios del sistema con aislamiento multi-tenant
- **Entidades:** `Users`
- **Vinculación:** `Users.LinkedEntityId` → `Entities.Id`
- **Roles:** `ADMIN`

### 4.9.2 Gestión de Perfiles

- **Propósito:** Consulta del catálogo de perfiles del sistema
- **Entidades:** `Profiles`
- **Nota:** Catálogo compartido entre tenants (sin `OwnerId`)
- **Roles:** `ADMIN`

### 4.9.3 Autorización de Páginas

- **Ruta:** `/security/page-permissions`
- **Propósito:** Configuración dinámica de qué perfiles acceden a qué páginas con qué nivel (Read/Write/Both)
- **Entidades:** `PageDefinitions`, `PagePermissions`
- **Funcionamiento:** `PageDiscoveryService` descubre automáticamente páginas en startup vía reflexión; el admin asigna permisos visualmente
- **Páginas sin permisos:** Aparecen destacadas en amarillo como recordatorio de configuración pendiente
- **Roles:** `ADMIN`

---

## 4.10 Módulo EcoDataNet — Espacio de Datos

**Ruta base:** `/ecodatanet`
**Módulo:** EcoDataNet

Integración de GreenTransit con el data space EcoDataNet mediante conectores EDC (Eclipse Dataspace Components v3). Permite descubrimiento de catálogos DCAT/ODRL, negociación de contratos, transferencia y descarga de datos entre conectores del ecosistema.

### 4.10.1 Configuración conector EDC

- **Ruta:** `/ecodatanet/connector-config`
- **Propósito:** Permite a cada usuario configurar los datos de conexión de su conector EDC (servidor, ID del conector). ADMIN puede configurar cualquier usuario del tenant; el resto solo su propio conector.
- **Entidades:** `UserEDCConnector`, `Users`
- **Comportamiento ADMIN:** Tabla paginada de usuarios del tenant con búsqueda y badge "Tiene conector". Al seleccionar un usuario, formulario de configuración.
- **Comportamiento NO ADMIN:** Formulario directo con datos del usuario logueado.
- **Campos:** Nombre de usuario (solo lectura), Nombre del servidor EDC, Identificador del conector.
- **CQRS:** `GetUsersForEDCListQuery`, `GetUserEDCConnectorQuery`, `UpsertUserEDCConnectorCommand`
- **Roles:** Todos los perfiles autenticados (policy `CanAccessEDCConnectorConfig`)

### 4.10.2 Consumir datos — Descubrimiento de catálogo

- **Ruta:** `/ecodatanet/consume-data`
- **Propósito:** El usuario selecciona un perfil cuyos datos quiere consumir (regulado por `ProfileEDCConsumer`). El sistema identifica proveedores del tenant con ese perfil, lee sus conectores y ejecuta `POST /v3/catalog/request` contra la Management API del conector consumidor para cada proveedor.
- **Entidades:** `ProfileEDCConsumer`, `UserEDCConnector`, `Users`, `Profiles`
- **Flujo:** Selección de perfil → desplegable de perfiles consumibles → botón "Consumir catálogo" → solicitudes paralelas (SemaphoreSlim, máx. 5 concurrentes) → resumen por proveedor (OK/Error/Sin conector/Timeout)
- **Servicio HTTP:** `IEdcManagementClient` → `EdcManagementClient` (Infrastructure). Body JSON-LD: `CatalogRequest` con `counterPartyAddress` y `protocol: dataspace-protocol-http`.
- **CQRS:** `RequestEdcCatalogCommand`, `GetConsumableProfilesQuery`, `GetProfilesForConsumptionListQuery`
- **Roles:** Todos los perfiles autenticados (policy `CanAccessEDCConsumeData`), acceso real regulado por `ProfileEDCConsumer`

### 4.10.3 Visualización del catálogo DCAT/ODRL

- **Propósito:** Parseo de los JSON DCAT/ODRL a DTOs tipados y presentación en vista marketplace: datasets por proveedor con nombre, versión, tipo y badge de oferta. Detalle de cada dataset con oferta ODRL humanizada (permisos, prohibiciones, obligaciones sin prefijos técnicos). Selección de oferta para negociación.
- **Servicio de parsing:** `IEdcCatalogParser` → `EdcCatalogParser` (Infrastructure). Soporta JSON-LD con prefijos compactos e IRIs completas, normalización de arrays/objetos únicos.
- **DTOs:** `EdcCatalogDto`, `EdcDatasetDto`, `EdcOfferDto`, `EdcPermissionDto`, `EdcConstraintDto`, `EdcProhibitionDto`, `EdcObligationDto`, `EdcDistributionDto`, `EdcProviderParsedCatalogDto`, `EdcNegotiationSelection`
- **CQRS:** `ParseEdcCatalogsQuery`

### 4.10.4 Negociación de contrato EDC v3

- **Propósito:** Ejecuta `POST /v3/contractnegotiations/` con la offer ODRL original del catálogo. Polling de estado via `GET /v3/contractnegotiations/{id}` hasta `FINALIZED` (extrae `contractAgreementId`). Stepper visual de 6 pasos.
- **Máquina de estados:** INITIAL → REQUESTING → REQUESTED → OFFERED → ACCEPTING → ACCEPTED → AGREEING → AGREED → VERIFYING → VERIFIED → FINALIZING → FINALIZED (o TERMINATED)
- **CQRS:** `StartContractNegotiationCommand`, `GetNegotiationStateQuery`
- **DTOs:** `EdcNegotiationResponse`, `EdcNegotiationStateResponse`

### 4.10.5 Transferencia de datos y descarga

- **Propósito:** Tras negociación finalizada, `POST /v3/transferprocesses` (TransferRequestDto: contractId, assetId, HttpData-PULL). Polling hasta STARTED/COMPLETED. Obtención de EDR (`GET /v3/edrs/{id}/dataaddress`) con endpoint y token temporal. Descarga desde data plane con `Authorization: Bearer {token}`. Exportación a fichero.
- **Máquina de estados:** INITIAL → PROVISIONING → PROVISIONED → REQUESTING → REQUESTED → STARTING → STARTED → COMPLETING → COMPLETED (o TERMINATED)
- **CQRS:** `StartTransferProcessCommand`, `GetTransferStateQuery`, `GetEndpointDataReferenceQuery`, `DownloadTransferDataCommand`
- **DTOs:** `EdcTransferResponse`, `EdcTransferStateResponse`, `EdcEndpointDataReferenceResponse`, `EdcDataDownloadResponse`
- **UI:** Stepper visual reutilizable (`EdcProcessStepper.razor`), botón "Descargar datos", "Exportar a fichero", botones "Reintentar" en caso de error.

### 4.10.6 Configuración (`appsettings.json`)

```json
"EcoDataNet": {
  "Edc": {
    "MaxConcurrentRequests": 5,
    "RequestTimeoutSeconds": 30,
    "ManagementApiKey": "",
    "NegotiationPollingIntervalSeconds": 3,
    "TransferPollingIntervalSeconds": 3,
    "NegotiationPollingMaxAttempts": 120,
    "TransferPollingMaxAttempts": 60
  }
}
```

---

# 5. 🔄 Flujo Global de Operaciones

## 5.1 Ciclo de vida de un traslado

```
┌─────────────────────────────────────────────────────────────────┐
│                    CICLO DE VIDA — WasteMove                    │
└─────────────────────────────────────────────────────────────────┘

  [ServiceOrders]       [WasteMoves]         [EntryPlants]    [TreatmentPlants]
  Pending/Scheduled  →  SOLICITADO        →  EN PLANTA     →  TRATADO
                     →  PLANIFICADO       ↗ (vía EntryCACs
                     →  RECOGIDO         ↗   EN CAC)
                     →  COMPLETADO


Documentos normativos generados:
  Estado RECOGIDO → DI (Documento de Identificación), NT (Notificación de Traslado)

Cálculos disparados:
  Estado RECOGIDO → Emisiones CO₂ Scope 1 (WasteMoveResidues.EmissionKgCO2e)
  Estado TRATADO  → KPIs: tasa reciclaje, valorización, eliminación
```

### Validaciones clave del flujo

| Transición | Validación |
|---|---|
| SO → WasteMove | `IdSource.EntityRole ∈ {Producer, CAC, PublicEntity, OperatorTransfer}` |
| SO → WasteMove | `IdDestination.EntityRole ∈ {Plant, CAC, SCRAP}` |
| Planificar | `Carrier.InscriptionNumber` no vacío |
| Planificar | Punto de recogida no en zona DUM restringida para esa ventana horaria |
| Planificar | Vigencia del `Agreement` en la fecha planificada |
| Recoger | Si `IsDangerous=1`: `NTNumber` + `DINumber` + `DIPhase` obligatorios |
| Pesar planta | `NetWeight = GrossWeight − TareWeight` (calculado en backend) |
| Pesar planta | Descuadre vs `WasteMoveResidues.WeightKg` → incidencia automática |

## 5.2 Ciclo de vida de una orden de servicio

```
Pending ──► Scheduled ──► InProgress ──► Completed
   └──────────────────────────────────► Cancelled

Edición permitida solo en: Pending, Scheduled
```

- La **cabecera** de la SO sincroniza `IdLERCode`, `ProductUse`, `ProductCategory` con la primera línea (`SortOrder = 0`) de `ServiceOrderResidues`
- Al actualizar, las líneas se reemplazan íntegramente (`ExecuteDeleteAsync`) para evitar conflictos de concurrencia en Blazor Server

## 5.3 Flujo de declaración de producto (RAP)

```
Productor crea ProductDeclaration (cabecera: Año/Período/Tipo)
         ↓
Añade líneas Products → referencia Residues (ResidueType='Product')
         ↓
Sistema calcula eco-modulación según EcoModulationRuleSets activos
         ↓
Declaración emitida (DateEmit) → Estado declarado
         ↓
Validación por SCRAP → Liquidación en Settlements
```

---

# 6. 📊 Dashboards Analíticos

## 6.1 Dashboard Home (Panel Principal)

**Ruta:** `/` (raíz)
**Acceso:** Todos los perfiles autenticados (adapta widgets según perfil)

### Widgets principales

| Widget | Fuente | Descripción |
|---|---|---|
| **KPIs globales** | `WasteMoves`, `WasteMoveResidues` | Total traslados, peso gestionado, incidencias abiertas |
| **Estado de traslados** | `WasteMoves.Status` | Distribución por estado (donut chart) |
| **Próximas recogidas** | `ServiceOrders.PlannedPickupStart` (próximos 7 días) | Lista de servicios planificados próximos |
| **Mapa interactivo** | `Entities.Latitude/Longitude` + `DUMZones.GeometryJson` | Entidades + puntos recogida + zonas DUM |
| **Alertas activas** | `Incidents` (severity ≥ High) | Incidencias críticas sin resolver |
| **Buscador global** | `ServiceOrders`, `WasteMoves`, `Entities` | Búsqueda transversal con debounce 300ms |

### Adaptación por perfil

- `PRODUCER`: muestra solo sus SOs y traslados
- `CARRIER`: muestra traslados asignados y próximas rutas
- `PLANT_OP`: muestra entradas pendientes en su planta
- `SCRAP`: muestra vista global de sus acuerdos y operativa
- `ADMIN` / `DISPATCH_OFFICE`: vista completa del tenant

---

## 6.2 Dashboards Logísticos

### 6.2.1 Optimización Logística SCRAP

**Ruta:** `/logistics/optimization`
**Policy:** `CanViewLogisticsOptimization`
**Perfiles:** `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Mapa de recogidas** | `ServiceOrders` + `Entities.Lat/Long` | Geolocalización de puntos de recogida pendientes |
| **KPIs por SCRAP** | `WasteMoves` agrupado por `IdScrap` | Traslados totales, peso, eficiencia logística |
| **Rutas planificadas** | `ServiceOrders` (Scheduled) | Listado de rutas para próximos 7 días |
| **Conflictos DUM** | `DUMZones` + `ServiceOrders` | Recogidas que colisionan con zonas DUM en horario punta |
| **Incidencias abiertas** | `Incidents` (Open/InProgress) | Por severidad y tipo |
| **Cuota de mercado** | `MarketShares` | % cuota real vs objetivo por SCRAP/flujo |

### 6.2.2 Monitorización Pública

**Ruta:** `/logistics/public-monitoring`
**Policy:** `CanViewPublicMonitoring`
**Perfiles:** `PUBLIC_ENT`, `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Resumen municipal** | `WasteMoves` filtrado por `MunicipalityCode` | Traslados, peso y tasa de reciclaje del municipio |
| **Acuerdos vigentes** | `Agreements` filtrado por `MunicipalityCode` | Convenios activos con entidades públicas |
| **Liquidaciones recientes** | `Settlements` | Últimas liquidaciones del municipio |
| **Mapa de densidad** | `ServiceOrders` + geografía | Mapa coroplético de servicios por municipio |
| **Tendencia mensual** | `WasteMoves` (últimos 12 meses) | Serie temporal de recogidas por municipio |

### 6.2.3 Panel Operativo

**Ruta:** `/logistics/operations`
**Policy:** `CanViewOperationalDashboard`
**Perfiles:** `DISPATCH_OFFICE`, `PLANT_OP`, `CAC_OP`, `CARRIER`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Traslados en curso** | `WasteMoves` (status ≠ Completed, Cancelled) | Kanban de traslados activos |
| **Alertas de operación** | `Incidents` + `WasteMoves` vencidos | Incidencias y traslados con retraso |
| **Báscula y pesajes** | `EntryPlants` (última 24h) | Entradas a planta recientes |
| **Capacidad CAC** | `EntryCACs` agrupado | Nivel de ocupación por CAC |
| **Próximas entregas** | `ServiceOrders` planificadas (48h) | Lista de entregas próximas con prioridad |

---

## 6.3 Dashboard Movilidad Urbana (UC3)

**Módulo:** Movilidad Urbana — Impacto RAEE en la ciudad

### UC3-C — Datos de Impacto RAEE en Movilidad (Oficina de Asignación)

**Ruta:** `/mobility/dispatch-data`
**Policy:** `CanViewMobilityDispatchData`
**Perfiles:** `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente de datos | Descripción |
|---|---|---|
| **Dataset exportable** | `WasteMoves` + `WasteMoveResidues` + `Entities` | Tabla detallada: distancia, duración, CO₂e, hora punta, DUM |
| **Resumen por SCRAP** | `WasteMoves` agrupado | `TotalPickups`, `PeakHourPercent`, `DumCompliancePercent`, `OpenIncidents` |
| **Planificación semanal** | `ServiceOrders` próximos 7 días (Pending/Scheduled) | Semáforo Red/Green por hora punta |
| **Serie mensual** | `WasteMoves` últimos 12 meses | `PeakHourPercent`, `DumCompliancePercent`, `AvgConflictIndex` por `YYYY-MM` |

**Filtros:** `Year` (obligatorio), `Month?`, `IdScrap?`, `ProvinceCode?`, `MunicipalityCode?`

**Exportación:** Botón XLSX → `ExportMobilityDataToExcelQuery` (ClosedXML)

**Cálculo hora punta** (configurable `appsettings.json` → `MobilitySettings`):

| Parámetro | Default | Descripción |
|---|---|---|
| `PeakHourStart1` | 7.5 | Inicio franja matutina (07:30) |
| `PeakHourEnd1` | 9.5 | Fin franja matutina (09:30) |
| `PeakHourStart2` | 17.5 | Inicio franja vespertina (17:30) |
| `PeakHourEnd2` | 19.5 | Fin franja vespertina (19:30) |

Una recogida es **en hora punta** si `hora_decimal ∈ [Start1, End1) ∪ [Start2, End2)` donde `hora_decimal = Hour + Minute/60`.

**Índice de conflicto de movilidad:**

```
ConflictIndex = (0.40 × PeakHourFactor) + (0.30 × DUMFactor) + (0.20 × IncidentFactor) + (0.10 × VolumeFactor)
```

| Factor | Peso | Fuente |
|---|---|---|
| `PeakHourFactor` | 40% | `WasteMoves.ActualPickupStart` en franja punta |
| `DUMFactor` | 30% | Colisión con `DUMZones` activas |
| `IncidentFactor` | 20% | `Incidents` relacionadas con el traslado |
| `VolumeFactor` | 10% | `WasteMoveResidues.WeightKg` vs. capacidad normal |

### UC3-P — Vista Pública de Movilidad (Entidad Pública)

**Ruta:** `/mobility/public-view`
**Policy:** `CanViewMobilityPublicView`
**Perfiles:** `PUBLIC_ENT`, `COORDINATOR`, `ADMIN`

Equivalente a UC3-C pero filtrada al ámbito municipal de la entidad pública (`MunicipalityCode`).

### UC3-M — Métricas Globales de Movilidad (SCRAP)

**Ruta:** `/mobility/scrap-metrics`
**Policy:** `CanViewMobilityScrapMetrics`
**Perfiles:** `SCRAP`, `ADMIN`

Métricas agregadas por SCRAP con comparativa entre períodos.

---

## 6.4 Dashboards Mapas de Calor

**Módulo:** Mapas de Calor — Densidad y Patrones de Residuos

Visualización geoespacial de densidad de residuos por municipio/provincia. Permite identificar zonas de acumulación y planificar operaciones preventivas.

### HM-A — Mapa de Calor por Densidad (SCRAP)

**Ruta:** `/heatmaps/density`
**Policy:** `CanViewHeatMapDensity`
**Perfiles:** `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Mapa coroplético** | `ServiceOrders` + `WasteMoves` agrupado por `MunicipalityCode` | Densidad de recogidas por municipio (color por intensidad) |
| **Top N municipios** | Tabla ordenada por peso | Municipios con mayor volumen de residuos |
| **Evolución temporal** | `WasteMoves` por fecha | Slider de tiempo para ver evolución |
| **Filtros LER** | `LERCodes` | Filtrar mapa por tipo de residuo |
| **Alertas de acumulación** | Umbrales configurables en `appsettings.json` | Municipios que superan umbral → alerta visual |

**Filtros:** `Year`, `Month?`, `WasteStream?`, `IdLERCode?`, `ProvinceCode?`

### HM-B — Mapa de Calor Operativo (Dispatch Office)

**Ruta:** `/heatmaps/operational`
**Policy:** `CanViewHeatMapOperational`
**Perfiles:** `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Puntos de recogida activos** | `ServiceOrders` (Pending/Scheduled) | Mapa de calor de recogidas pendientes |
| **Cobertura de transportistas** | `WasteMoves` + `Entities` (Carrier) | Zonas sin cobertura de transportistas |
| **Tiempos medios** | `WasteMoves` (ActualPickup − PlannedPickup) | Desvíos logísticos por zona geográfica |

### HM-C — Vista Pública de Densidad (Entidad Pública)

**Ruta:** `/heatmaps/public-density`
**Policy:** `CanViewHeatMapPublicDensity`
**Perfiles:** `PUBLIC_ENT`, `COORDINATOR`, `ADMIN`

Vista filtrada al ámbito municipal de la entidad pública.

---

## 6.5 Dashboards Huella de Carbono

**Módulo:** Huella de Carbono — Emisiones CO₂ en Gestión de Residuos Industriales

Cinco dashboards (HC-A a HC-E) que cubren todas las fuentes de emisión:
- **Scope 1:** Combustión directa en transporte
- **Scope 2:** Consumo eléctrico en plantas (kWh → kgCO₂e)
- **Scope 3:** Emisiones indirectas de la cadena de valor

### HC-A — Visión General de Emisiones (SCRAP / Admin)

**Ruta:** `/reporting/carbon-footprint/overview`
**Policy:** `CanViewCarbonFootprintOverview`
**Perfiles:** `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Total CO₂e Scope 1** | `WasteMoveResidues.EmissionKgCO2e` | Emisiones acumuladas por transporte |
| **Total CO₂e Scope 2** | `PlantEnergies.ElectricityKwh × Scope2Factor` | Emisiones indirectas por energía |
| **Intensidad por kg** | `∑EmissionKgCO2e / ∑WeightKg` | Indicador de eficiencia ambiental |
| **Comparativa SCRAP** | Agrupado por `IdScrap` | Ranking de SCRAPs por huella |
| **Serie mensual** | Últimos 12 meses | Evolución temporal de emisiones |
| **Top 5 rutas emisoras** | `WasteMoves` ordenado por `EmissionKgCO2e` | Rutas con mayor impacto ambiental |
| **Exportar XLSX** | `WasteMoveResidues` + `PlantEnergies` | Exportación completa (ClosedXML) |
| **Recomendaciones** | `CarbonFootprintCalculationService` | Motor de recomendaciones backend |

**Filtros:** `Year` (obligatorio), `Month?`, `IdScrap?`, `ProvinceCode?`, `WasteStream?`

### HC-B — Emisiones de Transporte por Vehículo/Ruta (Transportista / SCRAP)

**Ruta:** `/reporting/carbon-footprint/transport`
**Policy:** `CanViewCarbonFootprintTransport`
**Perfiles:** `CARRIER`, `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Desglose por vehículo** | `WasteMoveResidues` agrupado por `VehicleType` + `FuelType` + `EuroClass` | Emisiones por tipo de vehículo |
| **Mapa de rutas** | `WasteMoves` con `TransportDistanceKm` | Rutas geográficas coloreadas por emisión |
| **Factor aplicado** | `EmissionFactors.kgCO₂ePerKm` | Valor del factor utilizado en cada traslado |
| **Comparativa Euro** | Agrupado por `EuroClass` | Emisiones por clase Euro del vehículo |

### HC-C — Consumo Energético en Planta (Operador Planta)

**Ruta:** `/reporting/carbon-footprint/plant-energy`
**Policy:** `CanViewCarbonFootprintPlantEnergy`
**Perfiles:** `PLANT_OP`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Consumo mensual** | `PlantEnergies` por `Year/Month/IdPlant` | kWh, m³ gas, otros combustibles |
| **Emisiones Scope 2** | `ElectricityKwh × Scope2ConversionFactor` | kgCO₂e por mes (factor configurable) |
| **Comparativa anual** | Últimos 3 años | Tendencia de consumo por planta |
| **Intensidad energética** | `kWh / TotalInputWeightKg` (desde TreatmentPlants) | Eficiencia energética por tonelada tratada |
| **Exportar XLSX** | `PlantEnergies` | Exportación detallada (ClosedXML) |

**Factor Scope 2:** configurable en `appsettings.json` → `CarbonFootprint.Scope2ConversionFactor` (kWh → kgCO₂e, mix eléctrico español)

### HC-D — Huella de Producto por Productor

**Ruta:** `/reporting/carbon-footprint/producer`
**Policy:** `CanViewCarbonFootprintProducer`
**Perfiles:** `PRODUCER`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Emisiones por SO** | `WasteMoveResidues` filtrado por `IdIssuedBy` | Huella de los traslados de los residuos del productor |
| **Perfil LER** | Agrupado por `LERCodes` | Distribución de emisiones por tipo de residuo |
| **Objetivo 55** | Comparativa contra referencia normativa | Distancia al objetivo de reducción 55% |

### HC-E — Huella Municipal (Entidad Pública)

**Ruta:** `/reporting/carbon-footprint/municipal`
**Policy:** `CanViewCarbonFootprintMunicipal`
**Perfiles:** `PUBLIC_ENT`, `ADMIN`

Vista filtrada al ámbito municipal (`Entities.MunicipalityCode` de la entidad pública) con desglose por zona y LER.

---

## 6.6 Dashboards Análisis y Cumplimiento Normativo

**Módulo:** Análisis y Cumplimiento Normativo — Responsabilidad Ampliada del Productor (RAP)

Cinco dashboards (CN-A a CN-E) para auditar el cumplimiento de objetivos de reciclaje, cuotas de mercado y obligaciones contractuales de los acuerdos.

### CN-A — Resumen de Cumplimiento SCRAP

**Ruta:** `/reporting/regulatory-compliance/scrap-overview`
**Policy:** `CanViewScrapComplianceOverview`
**Perfiles:** `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Tasa de reciclaje actual** | `TreatmentPlantResidues` (FractionType=Recycle) vs. `EntryPlantResidues` | % reciclaje vs objetivo mínimo |
| **Tasa de valorización** | `TreatmentPlantResidues` (FractionType=Valorize) | % valorización vs objetivo |
| **Tasa de reutilización** | `TreatmentPlantResidues` (FractionType=Reuse) | % reutilización vs objetivo |
| **Cuota de mercado** | `MarketShares.MarketSharePercent` | % cuota actual por SCRAP |
| **Estado de alerta** | `ComplianceMonitoringService` | Verde/Amarillo/Rojo por cada KPI |
| **Tendencia anual** | Histórico anual | Serie de cumplimiento por año |
| **Desviación por flujo** | Agrupado por `WasteStream` | Flujos con mayor desviación del objetivo |
| **Exportar XLSX** | Datos de cumplimiento | Exportación completa (ClosedXML) |

**Alertas configurables:**
- `MinRecyclingPercent` (default 55%): umbral mínimo de reciclaje
- `MinReusePercent` (default 5%): umbral mínimo de reutilización
- `MinValorizationPercent` (default 65%): umbral mínimo de valorización
- `MarketShareRiskThresholdPercent` (default 80%): umbral de riesgo de cuota

### CN-B — Auditoría de Cuotas de Mercado

**Ruta:** `/reporting/regulatory-compliance/market-share-audit`
**Policy:** `CanViewMarketShareAudit`
**Perfiles:** `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Tabla de cuotas** | `MarketShares` | Cuota oficial vs. operativa real por SCRAP/año/flujo |
| **Comparativa inter-SCRAP** | `MarketShares` agrupado | Ranking de SCRAPs por cuota de mercado |
| **Evolución temporal** | Serie anual/mensual | Histórico de cuotas y desviaciones |
| **Alertas de concentración** | `MarketShareRiskThresholdPercent` | SCRAPs próximos al umbral de cuota máxima |
| **Exportar XLSX** | `MarketShares` | Exportación auditoría (ClosedXML) |

### CN-C — Seguimiento de Convenios (Acuerdos)

**Ruta:** `/reporting/regulatory-compliance/agreement-monitoring`
**Policy:** `CanViewAgreementComplianceMonitoring`
**Perfiles:** `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`

| Widget | Fuente | Descripción |
|---|---|---|
| **Acuerdos vigentes** | `Agreements` (Status=Active) | Lista con fecha vencimiento y ámbito territorial |
| **Alertas de vencimiento** | `Agreements.EffectiveTo` | Acuerdos a vencer en 90/30 días (warning/critical) |
| **Cumplimiento obligaciones** | `ObligationsJson` vs. `WasteMoves` | % cumplimiento de servicios mínimos pactados |
| **Mapa territorial** | `Agreements` + geografía | Cobertura geográfica de acuerdos por CCAA/provincia |
| **Liquidaciones pendientes** | `Settlements` (Status=Pending) | Liquidaciones sin validar por acuerdo |

**Alertas de vencimiento configurables:**
- `AgreementExpiryWarningDays` (default 90 días): aviso amarillo
- `AgreementExpiryCriticalDays` (default 30 días): alerta roja

### CN-D — Vista de Cumplimiento para Entidad Pública

**Ruta:** `/reporting/regulatory-compliance/public-entity-view`
**Policy:** `CanViewPublicEntityComplianceView`
**Perfiles:** `PUBLIC_ENT`, `COORDINATOR`, `ADMIN`

Vista filtrada al ámbito de la entidad pública con:
- Resumen de cumplimiento del municipio/provincia
- Liquidaciones recibidas y estados
- Acuerdos vigentes con el SCRAP
- KPIs de reciclaje de la zona

### CN-E — Datos de Cumplimiento para Dispatch Office

**Ruta:** `/reporting/regulatory-compliance/dispatch-data`
**Policy:** `CanViewDispatchOfficeComplianceData`
**Perfiles:** `DISPATCH_OFFICE`, `ADMIN`

Vista transversal de todo el tenant:
- KPIs de cumplimiento por SCRAP/zona
- Alertas críticas de cumplimiento pendientes
- Acuerdos próximos a vencer
- Estado de cuotas de mercado

---

## 6.7 Dashboard de Declaraciones (RAP)

**Ruta:** `/product-declarations`
**Módulo:** Declaraciones de Producto

| Widget | Fuente | Descripción |
|---|---|---|
| **Declaraciones por período** | `ProductDeclaration` agrupado | Estado de declaraciones por año/mes |
| **Productos declarados** | `Products` + `Residues` | Lista de productos con cantidad y precio |
| **Aplicación de eco-modulación** | `EcoModulationRules` | Reglas aplicadas y ajuste económico resultante |
| **Seguimiento estados** | `DocStates` | Flujo de estado de cada declaración |
| **Productor de referencia** | `Entities` (IdProducer) | Productor vinculado a cada declaración |

---

# 7. 🧮 Fórmulas y Cálculos Consolidados

| ID | Nombre | Fórmula | Entidades implicadas | Cuándo se calcula |
|---|---|---|---|---|
| **F-01** | Emisiones Scope 1 (transporte) | `CO₂e (kg) = TransportDistanceKm × kgCO₂ePerKm` | `WasteMoves`, `WasteMoveResidues`, `EmissionFactors` | Al confirmar recogida (estado → RECOGIDO) |
| **F-02** | Factor de emisión aplicado | `EF = EmissionFactors WHERE VehicleType + FuelType + EuroClass AND FactorSet.IsActive=1 ORDER BY EffectiveFrom DESC` | `EmissionFactorSets`, `EmissionFactors` | Selección en F-01 |
| **F-03** | Peso neto en planta | `NetWeight = GrossWeight − TareWeight` | `EntryPlants` | Al registrar entrada en planta (backend, no editable) |
| **F-04** | Tasa de reciclaje | `RecyclingRate = ∑WeightKg(FractionType='Recycle') / ∑TotalInputWeightKg × 100` | `TreatmentPlantResidues`, `TreatmentPlants` | Al registrar tratamiento + KPIs periódicos |
| **F-05** | Tasa de valorización | `ValorizationRate = ∑WeightKg(FractionType='Valorize') / ∑TotalInputWeightKg × 100` | `TreatmentPlantResidues`, `TreatmentPlants` | Al registrar tratamiento + KPIs periódicos |
| **F-06** | Tasa de reutilización | `ReuseRate = ∑WeightKg(FractionType='Reuse') / ∑TotalInputWeightKg × 100` | `TreatmentPlantResidues`, `TreatmentPlants` | Al registrar tratamiento + KPIs periódicos |
| **F-07** | Tasa de eliminación | `DisposalRate = ∑WeightKg(FractionType='Reject') / ∑TotalInputWeightKg × 100` | `TreatmentPlantResidues`, `TreatmentPlants` | Al registrar tratamiento |
| **F-08** | Emisiones Scope 2 (planta) | `Scope2CO₂e = ElectricityKwh × Scope2ConversionFactor` | `PlantEnergies` | Al reportar consumo mensual |
| **F-09** | Intensidad de emisiones | `EmissionIntensity = ∑EmissionKgCO2e / ∑WeightKg` | `WasteMoveResidues` | Dashboards HC-A, HC-D |
| **F-10** | Intensidad energética planta | `EnergyIntensity = ElectricityKwh / TotalInputWeightKg` | `PlantEnergies`, `TreatmentPlants` | Dashboard HC-C |
| **F-11** | Hora decimal de recogida | `hora_decimal = Hour(ActualPickupStart) + Minute(ActualPickupStart)/60` | `WasteMoves` | Clasificación hora punta |
| **F-12** | Es hora punta | `IsPeakHour = hora_decimal ∈ [7.5, 9.5) ∪ [17.5, 19.5)` | `WasteMoves` | Dashboard UC3 movilidad |
| **F-13** | Índice de conflicto movilidad | `ConflictIndex = 0.40×PeakHour + 0.30×DUM + 0.20×Incidents + 0.10×Volume` | `WasteMoves`, `DUMZones`, `Incidents` | Dashboard UC3 movilidad |
| **F-14** | Importe total liquidación | `TotalAmount = BaseAmount + AdjustmentsAmount + TaxAmount` | `Settlements` | Al calcular o validar liquidación |
| **F-15** | Desviación de peso | `WeightDeviation = ABS(NetWeight − WasteMoveResidues.WeightKg) / WasteMoveResidues.WeightKg × 100` | `EntryPlants`, `WasteMoveResidues` | Al registrar pesaje; si > umbral → incidencia |
| **F-16** | Cuota de mercado real | `ActualMarketShare = (∑WasteMove.TotalWeight[SCRAP_i] / ∑WasteMove.TotalWeight[ALL_SCRAP]) × 100` | `WasteMoves`, `WasteMoveResidues`, `MarketShares` | KPIs regulatorios periódicos |

### Nota sobre factores configurables en `appsettings.json`

| Fórmula | Parámetro configurable |
|---|---|
| F-08 (Scope 2) | `CarbonFootprint.Scope2ConversionFactor` |
| F-12 (Hora punta) | `Mobility.PeakHourRanges` |
| F-13 (Conflicto) | `Mobility.ConflictIndexWeights` |
| F-04/05/06 (Tasas) | `RegulatoryCompliance.Defaults.*` |

---

# 8. 🔒 Seguridad, Perfiles y Filtrado de Datos

## 8.1 Catálogo de perfiles

| `Profiles.Reference` | Descripción | `EntityRole` asociado | Responsabilidad principal |
|---|---|---|---|
| `PRODUCER` | Productor / Generador de residuos | `Producer` | Crear SOs, declarar productos, gestionar sus residuos |
| `CARRIER` | Transportista | `Carrier` / `OperatorTransfer` | Ejecutar recogidas, confirmar cargas |
| `SCRAP` | Sistema Colectivo RAP | `SCRAP` | Gestionar acuerdos, validar liquidaciones, supervisar operativa |
| `PUBLIC_ENT` | Entidad Pública / Ayuntamiento | `PublicEntity` | Crear SOs, revisar acuerdos y liquidaciones, reporting municipal |
| `CAC_OP` | Operador de Centro de Acopio | `CAC` | Registrar entradas en CAC |
| `PLANT_OP` | Operador de Planta de Tratamiento | `Plant` | Registrar entradas, pesaje, tratamiento, declarar energía |
| `COORDINATOR` | Coordinador del acuerdo | `Coordinator` | Lectura transversal del ámbito de sus acuerdos |
| `DISPATCH_OFFICE` | Oficina de Asignación / Gestor logístico | *(funcional, sin EntityRole directo)* | Crear traslados, planificar logística, gestionar maestros operativos |
| `REGULATOR` | Regulador — Autoridad de supervisión normativa | `Regulator` | Lectura transversal de KPIs, cumplimiento normativo e indicadores. Solo lectura |
| `CERTIFIER` | Certificador / Auditor — Validación y coherencia | `Certifier` | Lectura de evidencias, huella de carbono, reporting y KPIs. Solo lectura (ej. AENOR) |
| `ADMIN` | Administrador del sistema | *(superusuario del tenant)* | CRUD total, seguridad, configuración |

## 8.2 Reglas de filtrado por perfil

| Perfil | Scope de datos | Campo de filtrado |
|---|---|---|
| `PRODUCER` | Solo traslados de sus SOs | `ServiceOrders.IdIssuedBy = LinkedEntityId` |
| `CARRIER` | Solo traslados donde es transportista | `WasteMoveResidues.IdCarrier = LinkedEntityId` |
| `SCRAP` | Solo traslados donde figura como SCRAP | `WasteMoves.IdScrap = LinkedEntityId OR IdScrap2 = LinkedEntityId` |
| `CAC_OP` | Solo entradas de su CAC | `EntryCACs.IdCAC = LinkedEntityId` |
| `PLANT_OP` | Solo entradas/tratamientos de su planta | `EntryPlants.IdPlant = LinkedEntityId` + `PlantEnergies.IdPlant = LinkedEntityId` |
| `PUBLIC_ENT` | Solo traslados de su municipio o sus SOs | `Entities.MunicipalityCode OR ServiceOrders.IdIssuedBy = LinkedEntityId` |
| `COORDINATOR` | Solo acuerdos de su ámbito | `Agreements.IdCoordinator = LinkedEntityId` |
| `DISPATCH_OFFICE` | Todo el tenant | Solo `OwnerId` |
| `REGULATOR` | Todo el tenant | Solo `OwnerId` — lectura de KPIs e indicadores |
| `CERTIFIER` | Todo el tenant | Solo `OwnerId` — lectura de evidencias y reporting |
| `ADMIN` | Todo el tenant | Solo `OwnerId` |

**Implementación:** `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` + `IDataScopeService.ApplyScope()`

## 8.3 Regla geográfica

En **todas** las pantallas, tablas, filtros y exportaciones:

- `ProvinceCode` → resolver a `Province.Name` (JOIN con tabla `Province`)
- `MunicipalityCode` → resolver a `Municipality.Name` (JOIN con tabla `Municipality`)
- `StateCode` → resolver a `TerritoryState.Name`
- En selectores: mostrar `Name` como label, `Code` como value interno
- En exportaciones XLSX: columnas con **nombre**, nunca código

Aplica sin excepciones a: tablas, filtros, selectores, exports XLSX.

## 8.4 Autorización dinámica por pantalla

**Patrón de doble capa:**

```
[Authorize(Policy = PolicyConstants.XXX)]   ← Capa 1: mínimo estático en código
         ↓
PagePermissionService (BD: PagePermissions) ← Capa 2: control fino configurable por admin
         ↓
RouteAccessGuard                            ← Capa 3: protección de acceso directo por URL
```

**Tipos de autorización en componentes Blazor:**

| Patrón | Atributo | Cuándo usarlo |
|---|---|---|
| A | `[Authorize]` | Todos los autenticados ven la página |
| B | `[Authorize(Policy = PolicyConstants.XXX)]` | Solo perfiles concretos |
| C | `[Authorize]` + `ProfileAuthorizeView` interno | Página visible para todos, botones de acción por perfil |

**PageDiscoveryService — InferModuleName():**

| Namespace / Ruta | Módulo asignado |
|---|---|
| `Security` · `/users` · `/profiles` · `/security` | Seguridad |
| `Reporting` · `/traceability` · `/kpis` · `/documents` | Reporting |
| `Logistics` · `/logistics/` | Dashboards Logísticos |
| `Sustainability` · `/incidents` · `/dum-zones` · `/emissions` · `/plant-energies` | Sostenibilidad |
| `/entities` · `/ler-codes` · `/residues` · `/treatment-operations` | Configuración |
| `/service-orders` · `/waste-moves` · `/entry-*` · `/treatment-plants` | Operaciones |
| `/agreements` · `/settlements` · `/market-shares` | Contratos y Liquidaciones |
| `/product-declarations` | Declaraciones de Producto |
| `/reporting/carbon-footprint/` | Reporting (Huella de Carbono) |
| `/reporting/regulatory-compliance/` | Reporting (Cumplimiento Normativo) |
| `/ecodatanet/` | EcoDataNet |

**Checklist al crear una nueva página:**

- [ ] `@page "/mi-nueva-ruta"` definida
- [ ] `@attribute [Authorize...]` con policy adecuada
- [ ] Si policy nueva → añadida en `PolicyConstants.cs` + `Program.cs`
- [ ] Namespace coherente con el módulo
- [ ] Si ruta/namespace no mapea → actualizar `InferModuleName()`
- [ ] Si nombre no descriptivo → actualizar `HumanizeName()`
- [ ] Entrada en `NavMenu.razor` con `AuthorizeView`

---

# 9. ⚙️ Configuración de Umbrales y Parámetros

Todos los valores que afectan a cálculos y dashboards son **configurables en `appsettings.json`**. Nunca hardcodeados en código.

```json
{
  "CarbonFootprint": {
    "Scope2ConversionFactor": 0.233,
    "RecommendationThresholds": {
      "HighEmissionIntensityKgPerKg": 0.05,
      "ElectricVehicleSuggestionPeakHourPercent": 30
    },
    "Objetivo55ReferenceYear": 2019
  },

  "Mobility": {
    "PeakHourRanges": [
      { "Start": 7.5, "End": 9.5 },
      { "Start": 17.5, "End": 19.5 }
    ],
    "ConflictIndexWeights": {
      "PeakHourFactor": 0.40,
      "DUMFactor": 0.30,
      "IncidentFactor": 0.20,
      "VolumeFactor": 0.10
    }
  },

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
  },

  "HeatMaps": {
    "AccumulationAlertThresholds": {
      "WeightKgPerMunicipality": 50000,
      "PickupsPerMonth": 100
    }
  },

  "WeightDeviation": {
    "AutoIncidentThresholdPercent": 10
  },

  "EcoDataNet": {
    "Edc": {
      "MaxConcurrentRequests": 5,
      "RequestTimeoutSeconds": 30,
      "ManagementApiKey": "",
      "NegotiationPollingIntervalSeconds": 3,
      "TransferPollingIntervalSeconds": 3,
      "NegotiationPollingMaxAttempts": 120,
      "TransferPollingMaxAttempts": 60
    }
  }
}
```

### Descripción de cada parámetro

| Sección | Parámetro | Valor default | Descripción |
|---|---|---|---|
| `CarbonFootprint` | `Scope2ConversionFactor` | `0.233` | Factor de conversión kWh → kgCO₂e (mix eléctrico español IDAE) |
| `CarbonFootprint` | `HighEmissionIntensityKgPerKg` | `0.05` | Umbral de alerta: intensidad de emisión alta |
| `CarbonFootprint` | `ElectricVehicleSuggestionPeakHourPercent` | `30` | % hora punta a partir del cual se sugiere vehículo eléctrico |
| `CarbonFootprint` | `Objetivo55ReferenceYear` | `2019` | Año de referencia para objetivo de reducción 55% |
| `Mobility` | `PeakHourStart1` / `PeakHourEnd1` | `7.5` / `9.5` | Franja hora punta matutina (07:30–09:30) |
| `Mobility` | `PeakHourStart2` / `PeakHourEnd2` | `17.5` / `19.5` | Franja hora punta vespertina (17:30–19:30) |
| `Mobility` | `PeakHourFactor` | `0.40` | Peso del factor hora punta en el índice de conflicto |
| `Mobility` | `DUMFactor` | `0.30` | Peso del factor DUM en el índice de conflicto |
| `Mobility` | `IncidentFactor` | `0.20` | Peso del factor incidencias en el índice de conflicto |
| `Mobility` | `VolumeFactor` | `0.10` | Peso del factor volumen en el índice de conflicto |
| `RegulatoryCompliance.Alerts` | `MarketShareRiskThresholdPercent` | `80` | % de cuota de mercado que activa alerta de concentración |
| `RegulatoryCompliance.Alerts` | `AgreementExpiryWarningDays` | `90` | Días antes del vencimiento para aviso amarillo |
| `RegulatoryCompliance.Alerts` | `AgreementExpiryCriticalDays` | `30` | Días antes del vencimiento para alerta roja |
| `RegulatoryCompliance.Alerts` | `MinServicesThresholdPercent` | `70` | % mínimo de cumplimiento de servicios pactados |
| `RegulatoryCompliance.Defaults` | `DefaultMinRecyclingPercent` | `55` | % mínimo de reciclaje requerido por normativa |
| `RegulatoryCompliance.Defaults` | `DefaultMinReusePercent` | `5` | % mínimo de reutilización requerido por normativa |
| `RegulatoryCompliance.Defaults` | `DefaultMinValorizationPercent` | `65` | % mínimo de valorización requerido por normativa |
| `HeatMaps` | `WeightKgPerMunicipality` | `50000` | Umbral de peso (kg) para alerta de acumulación por municipio |
| `HeatMaps` | `PickupsPerMonth` | `100` | Umbral de recogidas/mes para alerta de acumulación |
| `WeightDeviation` | `AutoIncidentThresholdPercent` | `10` | % de desviación de peso que genera incidencia automática |
| `EcoDataNet.Edc` | `MaxConcurrentRequests` | `5` | Máximo de solicitudes de catálogo simultáneas por consumo |
| `EcoDataNet.Edc` | `RequestTimeoutSeconds` | `30` | Timeout por solicitud individual al conector EDC |
| `EcoDataNet.Edc` | `ManagementApiKey` | `""` | API Key para Management API (header X-Api-Key). Vacío = no se envía |
| `EcoDataNet.Edc` | `NegotiationPollingIntervalSeconds` | `3` | Intervalo de polling del estado de negociación |
| `EcoDataNet.Edc` | `TransferPollingIntervalSeconds` | `3` | Intervalo de polling del estado de transferencia |
| `EcoDataNet.Edc` | `NegotiationPollingMaxAttempts` | `120` | Máximo de intentos de polling de negociación (120 × 3s = 6 min) |
| `EcoDataNet.Edc` | `TransferPollingMaxAttempts` | `60` | Máximo de intentos de polling de transferencia (60 × 3s = 3 min) |

---

> **Documento generado a partir de las fuentes de verdad:**
> - `docs/Mapa_Funcionalidades.md`
> - `Crear_BD_v4_1.sql` (v4.1 — 38 tablas)
> - `docs/COPILOT_CONTEXT.md`
> - `docs/README.md`
>
> Última generación: automática por GitHub Copilot Workspace.
> Para actualizar, regenerar ejecutando el prompt `Prompt_Documentacion_Completa_GreenTransit.md`.
