# 🗺️ Mapa de Funcionalidades — Sistema de Trazabilidad **GreenTransit**

> Plataforma web **multi-rol**, **multi-tenant** (`OwnerId`) y preparada para **data spaces (EDC)** que cubre el ciclo completo del residuo: planificación → ejecución → pesaje → tratamiento → justificación económica → reporting regulatorio.
>
> Basado en el modelo de datos técnico **v4.1** (SQL Server Azure). Cada funcionalidad detalla **entidades implicadas**, **campos clave** y **transiciones de estado** del traslado.
>
> **Documento unificado** que integra: Mapa de Funcionalidades, Mapa de Autenticación y Autorización, Modelo de Datos, Patrón de Autorización en Páginas, Dashboard UC2 (Optimización RAEE), Dashboard UC3 (Movilidad Urbana), Dashboard Mapas de Calor (Densidad y Patrones de Residuos), Dashboard Huella de Carbono (Emisiones CO₂ en Gestión de Residuos Industriales) y Dashboard Análisis y Cumplimiento Normativo (RAP — Responsabilidad Ampliada del Productor).

---

# Parte I — Modelo de Datos


Este documento describe la **estructura lógica del modelo de datos**
definida en `Crear_BD_v4_1.sql`.

Es una **fuente de verdad** para entender:
- entidades de dominio
- tablas maestras
- tablas operativas
- relaciones clave
- decisiones de modelado relevantes

No sustituye al SQL, lo interpreta.

---

## 1. Visión general

La base de datos **GreenTransitDB** soporta:
- trazabilidad de residuos
- gestión de traslados
- declaraciones de producto
- acuerdos, liquidaciones y ecodiseño
- cálculo de impactos ambientales

El diseño sigue estos principios:
- **catálogos maestros normalizados**
- **datos de instancia separados de datos descriptivos**
- **versionado y auditoría en tablas críticas**
- **uso extensivo de FK para trazabilidad**

---

## 2. Entidades maestras principales

### 2.1 Entities

Tabla maestra de **actores del sistema**.

Roles soportados (campo `EntityRole`):
- Source
- Destination
- Carrier
- OperatorTransfer
- SCRAP
- Producer
- Plant
- CAC
- PublicEntity
- Coordinator
- Other

Se usa como referencia transversal en:
- traslados
- órdenes de servicio
- acuerdos
- liquidaciones
- residuos (productor)

👉 **Regla clave**: no se duplican datos de actores en tablas operativas.

---

### 2.2 LERCodes

Catálogo oficial de **códigos LER**.

Responsabilidades:
- clasificación normativa del residuo
- peligrosidad (`IsDangerous`)
- marcado RAEE
- jerarquía capítulo / subcapítulo

Es referenciado por:
- Residues
- ServiceOrders
- SettlementLines

👉 El código LER **no se repite como texto** en tablas operativas.

---

### 2.3 Residues (catálogo maestro)

Tabla **central del modelo**.

Unifica lo que antes estaba duplicado en múltiples tablas.

Campo clave: `ResidueType`
- `Waste` → residuo operativo
- `Product` → producto puesto en mercado
- `ProductSpec` → ficha técnica / ecodiseño

Incluye:
- descripción normalizada
- código LER
- peligrosidad
- atributos RAEE
- ecodiseño (reparabilidad, reciclado, materiales)
- productor asociado

👉 **Regla crítica**  
Las tablas operativas **no describen residuos**, solo los referencian.

---

### 2.4 TreatmentOperations

Catálogo normativo de **operaciones de tratamiento** según normativa (R/D).

Usado en:
- destino previsto del residuo
- tratamiento real en planta
- cálculo de KPIs (reciclaje, valorización, eliminación)

Evita valores libres o inconsistentes.

---

## 3. Flujo operativo de residuos

### 3.1 WasteMoves

Entidad núcleo del **traslado de residuos**.

Representa:
- un traslado completo
- origen, destino y operadores
- planificación vs ejecución
- referencia documental

Referenciado por:
- WasteMoveResidues
- EntryPlants
- EntryCACs
- TreatmentPlants
- Incidents

---

### 3.2 WasteMoveResidues

Tabla de **detalle por residuo trasladado**.

Contiene **datos de instancia**, no de catálogo:
- peso real
- unidades
- precios
- NT / DI / fase DI
- transportista real
- operación de tratamiento prevista

Referencias clave:
- `IdResidue` → Residues
- `IdTreatmentOperationDestiny` → TreatmentOperations
- `IdCarrier` → Entities

👉 Misma ficha de residuo puede aparecer en múltiples traslados
con documentos y pesos distintos.

---

## 4. Entradas y tratamiento

### 4.1 EntryPlants / EntryPlantResidues

Registro de **entrada en planta** y pesajes reales.

- EntryPlants → cabecera
- EntryPlantResidues → detalle por residuo

Siempre referencian:
- WasteMoves
- Residues

---

### 4.2 TreatmentPlants / TreatmentPlantResidues

Registro del **tratamiento aplicado en planta**.

Permite modelar:
- residuo de entrada
- fracciones reutilizadas
- fracciones valorizadas
- rechazos

Cada flujo apunta a Residues, eliminando duplicaciones y ambigüedades.

---

### 4.3 EntryCACs / EntryCACResidues

Equivalente a planta, pero para **centros de acopio (CAC)**.

---

## 5. Órdenes, acuerdos y liquidaciones

### 5.1 ServiceOrders

Ordena y planifica el servicio:
- qué se recoge
- dónde
- cuándo
- con qué transportista
- con qué clasificación LER

Sirve como nexo entre planificación y ejecución.

Estados válidos: `Pending`, `Scheduled`, `InProgress`, `Completed`, `Cancelled`.
Solo `Pending` y `Scheduled` permiten edición.

---

### 5.1.1 ServiceOrderResidues

Tabla hija de `ServiceOrders`. Permite definir **múltiples líneas de residuo** por orden de servicio.

Campos clave:
- `Id` (GUID PK)
- `IdServiceOrder` (FK → `ServiceOrders`)
- `SortOrder` (orden de línea)
- `IdLERCode` (FK → `LERCodes`)
- `ProductUse`, `ProductCategory`
- `EstimatedWeight`, `MeasureUnit`, `Units`

Reglas:
- La cabecera de la SO sincroniza sus campos de clasificación con la primera línea (`SortOrder = 0`).
- Al actualizar una SO, las líneas se reemplazan íntegramente (`ExecuteDeleteAsync`) para evitar conflictos de concurrencia en Blazor Server.

---

### 5.2 Agreements

Define acuerdos marco:
- SCRAP
- entidades públicas
- coordinadores
- ámbito territorial
- reglas económicas

Se versiona y se referencia desde liquidaciones.

---

### 5.3 Settlements / SettlementLines

Modelo económico:
- liquidación por periodo
- desglose por línea (peso, LER, precio)

Pensado para:
- auditoría
- justificación documental
- trazabilidad regulatoria

---

## 6. Declaración de producto y ecodiseño

### 6.1 ProductDeclaration

Cabecera de declaraciones periódicas de productor.

---

### 6.2 Products

Líneas declaradas.
Cada producto apunta a:
- Residues (ResidueType = Product)

La descripción técnica **no está aquí**, está en el catálogo.

---

### 6.3 ProductSpecs

Ficha técnica del producto.
Residues contiene:
- ecodiseño
- composición
- reciclado
- peligrosidad

ProductSpecs añade:
- referencias de negocio
- clasificación interna
- versionado

---

## 7. Impacto ambiental

### 7.1 EmissionFactorSets / EmissionFactors

Permite:
- versionar metodologías
- recalcular emisiones
- auditar resultados históricos

Referenciado desde WasteMoveResidues.

---

### 7.2 EcoModulationRuleSets / EcoModulationRules

Modelo de reglas de **eco-modulación**:
- criterios en JSON
- impacto económico
- versionado normativo

---

## 8. Diccionarios y geografía

Incluye:
- países, CCAA, provincias, municipios
- categorías y usos de producto
- estados de documentos
- perfiles y usuarios

Son tablas **estables**, no operativas.

### 8.1 Seguridad — `Profiles` y `Users`

#### `Profiles`
Catálogo de perfiles del sistema. Sin OwnerId (compartido entre tenants).
Campos: `ID`, `Reference` (único), `Description`, `CreateDate`.

#### `Users`
Usuarios del sistema con aislamiento multi-tenant por `OwnerId`.

| Campo | Tipo | Descripción |
|---|---|---|
| `ID` | `int IDENTITY` | PK |
| `Login` | `nvarchar(256)` | Único globalmente (índice UX_Users_Login) |
| `Email` | `nvarchar(256)` | Dirección de correo |
| `CompleteName` | `nvarchar(512)` | Nombre para mostrar |
| `IdProfile` | `int` | FK → `Profiles.ID` |
| `NationalId` | `int?` | FK → `Country.ID` |
| `GeographicalId` | `int?` | FK → `TerritoryState.ID` |
| `MunicipalityId` | `int?` | FK → `Municipality.ID` |
| `OwnerId` | `uniqueidentifier?` | Tenant — filtro global |
| `PortalEDCProvider` | `nvarchar(512)?` | Endpoint EDC como proveedor |
| `PortalEDCConsumer` | `nvarchar(512)?` | Endpoint EDC como consumidor |
| `IsActive` | `bit NOT NULL DEFAULT 1` | Control de acceso — ⚠️ campo añadido (migración `AddUserIsActive`) |
| `CreateDate` | `datetime?` | Fecha de alta |

> **Regla**: `ClientSecret` de `UserSharePointCredentials` **nunca** se expone en queries ni DTOs de la capa Application. Solo se escribe.

---

## 9. Reglas de uso del modelo

1. No duplicar descripciones de residuos fuera de `Residues`
2. No usar strings libres donde hay catálogos
3. Las tablas operativas solo guardan datos de instancia
4. Cambios estructurales exigen actualización de este documento

---

## 10. Relación con el SQL

- El SQL es la implementación
- Este documento es la **interpretación funcional**
- Copilot debe usar este archivo para comprender el modelo antes de generar código

---

# Parte II — Convenciones Generales del Sistema

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


---

# Parte III — Autenticación y Autorización

## 1. Autenticación — OpenID Connect

### 1.1. Proveedor de identidad

| Parámetro | Valor |
|---|---|
| Protocolo | OpenID Connect (OIDC) |
| Authority | `https://pronet-identity-wst-app.azurewebsites.net/` |
| Flujo | Authorization Code + PKCE |
| Tokens | ID Token + Access Token |
| Almacenamiento de credenciales | Ninguno — el sistema NO almacena contraseñas |

### 1.2. Mapeo de claims

| Claim OIDC | Campo interno | Uso |
|---|---|---|
| `sub` | `Users.ID` (mapeo) → `IdUser` | Identificador único del usuario en todas las tablas operativas |
| `email` o `preferred_username` | `Users.Login` / `Users.Email` | Identificación visual y notificaciones |
| Claim organizativo (custom) | `Users.OwnerId` | Aislamiento multi-tenant — filtra TODOS los datos operativos |

### 1.3. Flujo de autenticación

```
Usuario → Login → Servidor OIDC (Authority)
                      ↓
              Authorization Code
                      ↓
         Intercambio por ID Token + Access Token
                      ↓
         ClaimsTransformation en backend:
           - sub → buscar Users.ID
           - claim org → OwnerId
           - Users.IdProfile → cargar perfil y permisos
                      ↓
         CurrentUserService disponible en toda la app
```

### 1.4. Comportamiento de sesión

- **Sin credenciales locales**: toda la autenticación es delegada al servidor OIDC.
- **2FA opcional**: configurable en el servidor de identidad.
- **Bloqueo por inactividad**: si una `Entity` se desactiva (`IsActive = 0`), el usuario vinculado se bloquea (no se elimina).
- **Protección Blazor**: todas las páginas requieren autenticación salvo la landing de login.

---

## 2. Perfiles de usuario

### 2.1. Catálogo de perfiles (`Profiles`)

Cada usuario tiene exactamente un perfil (`Users.IdProfile → Profiles.ID`). El perfil determina qué pantallas ve y qué operaciones puede realizar.

| `Profiles.Reference` | Descripción | `EntityRole` asociado | Responsabilidad principal |
|---|---|---|---|
| `PRODUCER` | Productor / Generador de residuos | `Producer` | Crear órdenes de servicio, declarar productos, gestionar sus residuos |
| `CARRIER` | Transportista | `Carrier` / `OperatorTransfer` | Ejecutar recogidas, confirmar cargas, app móvil |
| `SCRAP` | Sistema Colectivo de Responsabilidad Ampliada | `SCRAP` | Gestionar acuerdos, validar liquidaciones, supervisar operativa, alta restringida de entidades |
| `PUBLIC_ENT` | Entidad Pública / Ayuntamiento | `PublicEntity` | Crear órdenes de servicio, revisar acuerdos y liquidaciones, reporting municipal |
| `CAC_OP` | Operador de Centro de Acopio | `CAC` | Registrar entradas en CAC, gestionar acopio |
| `PLANT_OP` | Operador de Planta de Tratamiento | `Plant` | Registrar entradas en planta, pesaje, clasificación, tratamiento, declarar energía |
| `COORDINATOR` | Coordinador del acuerdo | `Coordinator` | Lectura transversal del ámbito de los acuerdos |
| `DISPATCH_OFFICE` | Oficina de Asignación / Gestor logístico | *(perfil funcional, sin EntityRole directo)* | Crear traslados, planificar logística, asignar transportistas, gestionar maestros operativos |
| `ADMIN` | Administrador del sistema | *(superusuario del tenant)* | CRUD total, gestión de usuarios/perfiles, catálogos normativos, configuración |

### 2.2. Nuevo perfil: Oficina de Asignación (`DISPATCH_OFFICE`)

Este perfil cubre al **Gestor logístico** referenciado en el Mapa de Funcionalidades. Es el rol que:

- **Crea traslados** (`WasteMoves`) a partir de órdenes de servicio.
- **Planifica la logística**: asigna transportista, vehículo, ventanas horarias.
- **Gestiona maestros operativos**: CRUD en Entidades, Residuos y Operaciones R/D.
- **Supervisa incidencias** y puede resolverlas.
- **No tiene acceso** a la gestión de usuarios/perfiles (eso es del ADMIN).

Debe añadirse a la tabla `Profiles`:

```sql
INSERT INTO Profiles (Reference, Description)
VALUES ('DISPATCH_OFFICE', 'Oficina de Asignación — Gestor logístico');
```

### 2.3. Provisión automática de usuario al crear entidad

Al dar de alta una `Entity`, el sistema crea automáticamente un `Users` vinculado según este mapeo:

| `EntityRole` | `Profiles.Reference` asignado |
|---|---|
| `SCRAP` | `SCRAP` |
| `Producer` | `PRODUCER` |
| `Carrier` | `CARRIER` |
| `OperatorTransfer` | `CARRIER` |
| `Plant` | `PLANT_OP` |
| `CAC` | `CAC_OP` |
| `PublicEntity` | `PUBLIC_ENT` |
| `Coordinator` | `COORDINATOR` |
| `Source` / `Destination` / `Other` | *(no se crea usuario automáticamente)* |

> **Nota**: `DISPATCH_OFFICE` y `ADMIN` se crean manualmente por un administrador, ya que no corresponden a una entidad del ecosistema sino a roles funcionales internos.

---

## 3. Reglas transversales de filtrado de datos

### 3.1. Multi-tenant (`OwnerId`)

Todas las consultas operativas filtran por `Users.OwnerId`. Un usuario NUNCA ve datos de otro tenant. Excepción: catálogos compartidos (`LERCodes`, `TreatmentOperations`, geografía).

### 3.2. Filtrado por entidad vinculada ("Propios")

Cuando un permiso indica **"Propios"**, el filtro adicional depende del perfil:

| Perfil | Campo de filtro | Lógica |
|---|---|---|
| `PRODUCER` | `ServiceOrders.IdIssuedBy`, `Residues.IdProducer` | Solo ve SOs que emitió y residuos tipo Product/ProductSpec que declaró |
| `CARRIER` | `WasteMoveResidues.IdCarrier` | Solo ve traslados donde es el transportista asignado |
| `SCRAP` | `Agreements.IdScrap`, `WasteMoves.IdScrap` | Ve operativa vinculada a sus acuerdos |
| `PUBLIC_ENT` | `Agreements.IdPublicEntity`, `ServiceOrders.IdIssuedBy` | Ve acuerdos de su municipio y SOs que emitió |
| `PLANT_OP` | `EntryPlants` / `TreatmentPlants` de su entidad | Solo su planta |
| `CAC_OP` | `EntryCACs` de su entidad | Solo su CAC |
| `COORDINATOR` | `Agreements` donde figura como `IdCoordinator` | Lectura transversal del ámbito del acuerdo |

#### ✅ Implementación del filtrado en `ServiceOrders`

El filtro se aplica en **servidor** a través de `GetServiceOrdersQuery.IdIssuedBy`:

- **`ServiceOrderList.razor`**: al cargar la lista, si el perfil es `PRODUCER` o `PUBLIC_ENT`, se pasa automáticamente `IdIssuedBy = CurrentUser.LinkedEntityId`. El usuario no puede anularlo desde la UI.
- **`ServiceOrderForm.razor`**: al crear una nueva SO, el campo **Emisor** se autocompleta con `CurrentUser.LinkedEntityId` y se muestra como campo de solo lectura. En edición, el valor ya existe en BD y no se modifica.
- **Componentes implicados**: `ServiceOrderList.razor`, `ServiceOrderForm.razor`, `GetServiceOrdersQuery.cs`.
- **Servicio de contexto**: `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)`.

#### ✅ Implementación del filtrado en `Incidents`

- **`GetIncidentsQuery.cs`**: si el perfil es `PRODUCER`, se filtra `i.ServiceOrderId != null && i.ServiceOrder.IdIssuedBy == LinkedEntityId`. Solo ve incidencias cuya SO fue emitida por su entidad.
- **`IncidentForm.razor`**: el campo de traslado vinculado se convierte en un `<select>` cargado con `GetWasteMovesQuery(ServiceOrderIssuedBy: LinkedEntityId)`. El productor solo puede vincular incidencias a traslados propios.
- **Componentes implicados**: `IncidentList.razor`, `IncidentForm.razor`, `GetIncidentsQuery.cs`, `GetWasteMovesQuery.cs`.

#### ✅ Implementación del filtrado en `Dashboard`

El `GetDashboardSummaryQuery` adapta todos sus KPIs al perfil `PRODUCER`:

| KPI | Filtro adicional para PRODUCER |
|---|---|
| WasteMoves by status | `WasteMove.ServiceOrder.IdIssuedBy == LinkedEntityId` |
| Kg recogidos (mes) | WasteMoveResidues de los traslados anteriores |
| CO₂ (mes actual y anterior) | WasteMoveResidues de los traslados anteriores |
| Incidencias abiertas | `Incident.ServiceOrder.IdIssuedBy == LinkedEntityId` |
| Próximas recogidas | `ServiceOrders.IdIssuedBy == LinkedEntityId` |
| Kg recogidos vs tratados (6 meses) | WasteMoveResidues de los traslados anteriores |

### 3.3. Regla de creación

**Toda pantalla que muestra datos debe tener al menos un perfil con capacidad de creación.** Esta regla garantiza que no existan pantallas "huérfanas" donde nadie puede generar registros.

### 3.4. Sistema dinámico de permisos por pantalla (`PageDefinitions` / `PagePermissions`)

El acceso a cada pantalla se gestiona **dinámicamente desde la UI de administración** (`/security/page-permissions`), no mediante valores hardcodeados en código. El sistema funciona así:

1. **Auto-descubrimiento**: `IPageDiscoveryService` escanea en cada arranque todos los componentes `.razor` con `@page` y los registra en la tabla `PageDefinitions`.
2. **Configuración por admin**: desde `/security/page-permissions`, el administrador asigna a cada perfil un nivel de acceso por pantalla: **Lectura**, **Escritura**, **Ambos** o **Sin acceso**.
3. **Aplicación en runtime**: `IPagePermissionService.CanAccessRouteAsync()` consulta `PagePermissions` (con caché de 5 min) para determinar si un perfil puede acceder a una ruta.
4. **Triple capa**: `ProfileAuthorizeView` (estático) → `PagePermissionService` (dinámico BD) → `RouteAccessGuard` (protección URL directa).

Las matrices de permisos documentadas en este documento son la **configuración recomendada por defecto** que el admin debe aplicar tras el despliegue inicial. El código Blazor usa `[Authorize(Policy = ...)]` como mínimo de seguridad, pero el control fino se delega al sistema dinámico.

---

## 7. Implementación técnica en .NET

### 7.1. Policies de autorización (mínimo de seguridad en código)

> Las policies definen el **mínimo de seguridad estático**. El acceso efectivo se controla desde `PagePermissions` en BD. Un perfil incluido en una policy puede ser excluido desde la UI de admin, pero un perfil NO incluido en la policy nunca tendrá acceso, independientemente de la configuración de `PagePermissions`.

```
Policy                          Perfiles permitidos
────────────────────────────── ──────────────────────────────────────────
CanManageMasters                DISPATCH_OFFICE, ADMIN
CanCreateServiceOrders          PRODUCER, PUBLIC_ENT, DISPATCH_OFFICE, ADMIN
CanManageWasteMoves             DISPATCH_OFFICE, ADMIN
CanUpdateAssignedMoves          CARRIER (solo los suyos)
CanManageEntryPlants            PLANT_OP, ADMIN
CanManageEntryCACs              CAC_OP, ADMIN
CanManageTreatments             PLANT_OP, ADMIN
CanCreateIncidents              Todos los perfiles autenticados
CanResolveIncidents             DISPATCH_OFFICE, ADMIN
CanManageDUMZones               ADMIN
CanManagePlantEnergy            PLANT_OP, ADMIN
CanManageEmissionFactors        ADMIN
CanManageUsers                  ADMIN
CanManageProfiles               ADMIN
CanViewKPIs                     SCRAP, PUBLIC_ENT, PLANT_OP, COORDINATOR, DISPATCH_OFFICE, ADMIN
CanViewReporting                Todos (con filtrado por datos propios)
CanManageEntities               DISPATCH_OFFICE, ADMIN
CanCreateEntitiesRestricted     SCRAP (alta limitada a su ámbito)
CanViewLogisticsOptimization    SCRAP, COORDINATOR, ADMIN          ← Dashboard 1
CanViewPublicMonitoring         PUBLIC_ENT, ADMIN                  ← Dashboard 2
CanViewOperationalDashboard     DISPATCH_OFFICE, CAC_OP, PLANT_OP, ADMIN  ← Dashboard 3
CanViewMarketShares             SCRAP, ADMIN
CanManageMarketShares           ADMIN
CanManageAgreements             SCRAP, ADMIN
CanManageSettlements            SCRAP, ADMIN
CanViewProductDeclarations      ADMIN, PRODUCER, SCRAP, COORDINATOR
CanManageProductDeclarations    PRODUCER, ADMIN
CanValidateProductDeclarations  ADMIN
CanManageDeclarationDicts       ADMIN
CanViewHeatMapWasteDensity      SCRAP, DISPATCH_OFFICE, ADMIN          ← Mapa Calor HM-A
CanViewHeatMapPatternAnalysis   SCRAP, DISPATCH_OFFICE, ADMIN          ← Mapa Calor HM-B
CanViewHeatMapPublicView        PUBLIC_ENT, DISPATCH_OFFICE, ADMIN     ← Mapa Calor HM-C
AdminOnly                       ADMIN
```

### 7.2. Filtro multi-tenant (middleware)

```
Todas las queries operativas → WHERE OwnerId = @currentUserOwnerId
Excepción: LERCodes, TreatmentOperations, tablas geográficas (compartidas)
```

### 7.3. Filtro por datos propios (query filter)

```
Si Policy = "Propios" (CRUD-P, R-P, U-P, C+R-P):
  → Aplicar filtro adicional por entidad vinculada del usuario
  → Ejemplo CARRIER: WHERE WasteMoveResidues.IdCarrier = @currentUserEntityId
  → Ejemplo PRODUCER: WHERE ServiceOrders.IdIssuedBy = @currentUserEntityId
```

### 7.4. Seed de perfiles

```sql
INSERT INTO Profiles (Reference, Description) VALUES
('ADMIN', 'Administrador del sistema'),
('SCRAP', 'Sistema Colectivo de Responsabilidad Ampliada'),
('PRODUCER', 'Productor / Generador de residuos'),
('CARRIER', 'Transportista'),
('PLANT_OP', 'Operador de Planta de Tratamiento'),
('CAC_OP', 'Operador de Centro de Acopio'),
('PUBLIC_ENT', 'Entidad Pública / Ayuntamiento'),
('COORDINATOR', 'Coordinador del acuerdo'),
('DISPATCH_OFFICE', 'Oficina de Asignación — Gestor logístico');
```

---

## 8. Notas de diseño

### 8.1. ¿Por qué DISPATCH_OFFICE y no expandir ADMIN?

El `ADMIN` es un superusuario con acceso a configuración de seguridad (usuarios, perfiles). La Oficina de Asignación es un rol **operativo** que no debería poder gestionar usuarios ni perfiles. Separar ambos sigue el principio de mínimo privilegio.

### 8.2. ¿Por qué CARRIER no crea traslados?

El transportista **ejecuta** traslados, no los planifica. La creación del `WasteMove` (agrupación de SOs, asignación de origen/destino/SCRAP) es responsabilidad de la Oficina de Asignación. El transportista recibe el traslado ya planificado y solo actualiza datos de ejecución.

### 8.3. ¿Por qué SCRAP puede dar de alta entidades?

El SCRAP necesita registrar nuevos productores adheridos a su sistema colectivo. Esta alta está **restringida a su ámbito** (solo puede crear entidades vinculadas a sus acuerdos, no entidades de otros SCRAPs).

### 8.4. ¿Por qué KPIs no está visible para PRODUCER y CARRIER?

Los KPIs muestran datos agregados de cumplimiento normativo (tasas de reciclaje, valorización, cumplimiento de cuotas de mercado). Son relevantes para perfiles con responsabilidad de supervisión (`SCRAP`, `PUBLIC_ENT`, `COORDINATOR`) o de gestión operativa (`DISPATCH_OFFICE`, `PLANT_OP`, `ADMIN`), pero no aportan valor a un productor o transportista individual.

---

# Parte IV — Módulos Funcionales

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

### 4.5. Reglas de Ecomodulación (`EcoModulationRuleSets` / `EcoModulationRules`) ✅ IMPLEMENTADO

#### ¿Qué es la ecomodulación?

Mecanismo por el que las tarifas RAP (**Responsabilidad Ampliada del Productor**) se **ajustan al alza o a la baja** según criterios de ecodiseño del producto. Un producto con mejor diseño ambiental paga menos; uno con peor diseño paga más. La normativa obliga a los SCRAP a aplicar este ajuste al calcular las liquidaciones de sus productores adheridos.

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
| `EcoModulationRule` | `Id`, `RuleSetId`, `RuleCode`, `ProductCategory` (int), `CriteriaJson`, `FeeImpactType` (`None`/`Reduction`/`Surcharge`), `FeeImpactValue` (decimal) | Regla individual. El `CriteriaJson` define los criterios de aplicación (categoría de producto, peso mínimo, etc.) |

#### ¿Quién publica los conjuntos de reglas?

El **SCRAP** es el actor que administra y publica los conjuntos de reglas. En la aplicación, solo el perfil **ADMIN** puede crear, editar y activar conjuntos a través del módulo de gestión. El campo `PublisherName / PublisherNationalId / PublisherCenterCode` identifica la entidad publicadora.

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

Los criterios se evalúan en la capa Application al calcular la liquidación. La estructura del JSON es extensible sin cambios de esquema.

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

#### Módulo de gestión — Ruta `/ecomodulation-rule-sets`

- **Ruta**: `/ecomodulation-rule-sets`
- **Módulo**: Sostenibilidad
- **Archivos implementados**:
  - `Application/Features/Ecomodulation/DTOs/EcoModulationRuleSetDtos.cs` — DTOs: `EcoModulationRuleSetDto` (lista), `EcoModulationRuleSetDetailDto` (detalle con reglas), `EcoModulationRuleDto`, `EcoModulationRuleLineDto` (formulario).
  - `Application/Features/Ecomodulation/Queries/GetEcoModulationRuleSetsQuery.cs` — listado paginado filtrado por estado; `GetEcoModulationRuleSetByIdQuery` para detalle.
  - `Application/Features/Ecomodulation/Commands/EcoModulationRuleSetCommands.cs` — `CreateEcoModulationRuleSetCommand`, `UpdateEcoModulationRuleSetCommand`, `ActivateEcoModulationRuleSetCommand` (desactiva el resto), `DeleteEcoModulationRuleSetCommand` (solo si no está activo).
  - `Web/Components/Pages/Ecomodulation/EcoModulationRuleSetList.razor` — página master-detail: lista de conjuntos a la izquierda, reglas del conjunto seleccionado a la derecha. Modal de creación/edición con líneas de reglas editables en tabla.
  - `Infrastructure/Services/PageDiscoveryService.cs` — ruta mapeada al módulo "Sostenibilidad"; nombre humanizado "Reglas de Ecomodulación".
- **UI**: layout master-detail. Botones de Activar / Editar / Eliminar condicionados por `_canWrite` (permisos por pantalla dinámicos, igual que el resto de módulos).
- **Autorización**: `@attribute [Authorize]` + `IPagePermissionService.CanWriteRouteAsync("/ecomodulation-rule-sets")`. Acceso y permisos de escritura gestionados desde `/security/page-permissions`.
- **Roles**: lectura → **SCRAP**, **ADMIN**; escritura (crear/editar/activar/eliminar) → **ADMIN**.

#### Aparición en dashboards

| Dashboard | Widget | Descripción |
|---|---|---|
| **RAP** (`/product-declarations`) | Aplicación de eco-modulación | Reglas aplicadas y ajuste económico resultante por declaración |
| **Normativo** | Panel timeline cambios normativos | Cambios recientes en `EcoModulationRuleSets` |
| **Panel SCRAP** | Vista económica agregada | Impacto económico acumulado por categoría de producto |

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

### 5.7. Módulo de Huella de Carbono — Emisiones CO₂ en la Gestión de Residuos Industriales (UC-HC)

> Agrupa las vistas orientadas a medir, analizar y reportar la **huella de carbono** generada por la gestión integral de residuos industriales. Cubre Scope 1 (transporte) y Scope 2 (energía de plantas). Se alinea con el **«Objetivo 55» de la Ley Europea del Clima** y posibilita que los participantes del ecosistema evalúen y minimicen el impacto ambiental de sus operaciones.
>
> Los datos explotados incluyen: datos operativos de residuos (tipos, métodos de tratamiento, destinos finales), datos de uso de combustibles (`FuelType`, `EuroClass`), datos logísticos (`VehicleType`, km recorridos, duración), eficiencia energética de las instalaciones de reciclaje (`PlantEnergies`) y emisiones calculadas (`TransportInfo_TransportCarbonEmissions`).
>
> **No se crean nuevas entidades de dominio.** Todo se implementa con las entidades del modelo v4.1 existente.

#### 5.7.1. HC-A — Panel de Visión Consolidada de Huella de Carbono

- **Ruta**: `/reporting/carbon-footprint/overview`
- **Perfil objetivo**: `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`.
- **Policy**: `CanViewCarbonFootprintOverview`.
- **Lógica**: visión estratégica consolidada de la huella de carbono total (Scope 1 + Scope 2) generada por la gestión de residuos, con desglose por múltiples dimensiones.

**Archivos a crear**:
- `Application/Features/Reporting/CarbonFootprint/Queries/GetCarbonFootprintOverviewQuery.cs` — handler principal con filtros: `Year`, `Month?`/`Quarter?`, `IdScrap?`, `ProvinceCode?`, `MunicipalityCode?`, `VehicleType?`, `FuelType?`, `WasteStream?`, `LERCode?`.
- `Application/Features/Reporting/CarbonFootprint/DTOs/CarbonFootprintOverviewDto.cs` — DTOs: `CarbonFootprintOverviewDto`, `EmissionsTrendDto`, `EmissionsByVehicleTypeDto`, `EmissionsByFuelTypeDto`, `EmissionsByGeographyDto`, `EmissionsByScrapDto`, `EmissionsByEuroClassDto`, `CarbonFootprintExportRowDto`.
- `Application/Features/Reporting/CarbonFootprint/Queries/ExportCarbonFootprintToExcelQuery.cs` — genera XLSX en memoria (ClosedXML).
- `Application/Features/Reporting/CarbonFootprint/Services/CarbonFootprintCalculationService.cs` — servicio de agregación: cálculo de Scope 2 (kWh × factor mix eléctrico), intensidades derivadas, motor de recomendaciones.
- `Web/Components/Pages/Reporting/CarbonFootprint/CarbonFootprintOverview.razor` — página `/reporting/carbon-footprint/overview`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Resumen ejecutivo (cards KPI) | `WasteMoveResidues` + `PlantEnergies` | Scope 1 total (kgCO₂e), Scope 2 total, huella combinada, intensidad (kgCO₂e/t), toneladas gestionadas, variación % vs periodo anterior |
| Evolución temporal (line chart dual axis) | `WasteMoveResidues` agrupado mes/trimestre | Series: Scope 1, Scope 2, intensidad. Línea de referencia «Objetivo 55» configurable |
| Desglose por tipo de vehículo (bar chart / donut) | `WasteMoveResidues.VehicleType` | Emisiones, nº traslados, distancia, intensidad por kgCO₂e/km |
| Desglose por tipo de combustible (pie chart) | `WasteMoveResidues.FuelType` | % del total y kgCO₂e absolutos por combustible |
| Ranking por zona geográfica (tabla + mapa) | `WasteMoveResidues` + `Entities` + `Province` + `Municipality` | Emisiones, traslados, intensidad por **nombre** de provincia/municipio (nunca código). Mapa con gradiente |
| Ranking por SCRAP (tabla + sparklines) | `WasteMoves.IdScrap` + `WasteMoveResidues` | Emisiones, toneladas, intensidad, variación %. Sparkline de evolución mensual |
| Distribución por clase Euro (stacked bar) | `WasteMoveResidues.EuroClass` | Evidenciar impacto de renovación de flota |
| Tabla exportable (XLSX) | Join completo operativo | Dataset plano con todos los campos relevantes para análisis externo y justificación regulatoria |

**Cálculo de Scope 2**: `PlantEnergies.KwhTotal` × factor de mix eléctrico configurable en `appsettings.json` → sección `CarbonFootprint.Scope2.DefaultGridMixFactor_kgCO2e_per_kWh` (por defecto 0.22 kgCO₂e/kWh, factor REE España).

---

#### 5.7.2. HC-B — Panel de Análisis de Emisiones del Transporte

- **Ruta**: `/reporting/carbon-footprint/transport-emissions`
- **Perfil objetivo**: `SCRAP`, `COORDINATOR`, `DISPATCH_OFFICE`, `CARRIER`, `ADMIN`.
- **Policy**: `CanViewTransportEmissionsAnalysis`.
- **Lógica**: análisis detallado de las emisiones Scope 1, con herramientas de comparación para identificar oportunidades de optimización de flota y rutas.

**Archivos a crear**:
- `Application/Features/Reporting/CarbonFootprint/Queries/GetTransportEmissionsAnalysisQuery.cs` — filtros: `Year`, `Month?`, `IdScrap?`, `IdCarrier?`, `VehicleType?`, `FuelType?`, `EuroClass?`, `ProvinceCode?`, `MunicipalityCode?`.
- `Application/Features/Reporting/CarbonFootprint/DTOs/TransportEmissionsAnalysisDto.cs` — DTOs: `FleetEfficiencyDto`, `PeriodComparisonDto`, `TransporterRankingDto`, `EmissionsHeatmapCellDto`, `EmissionFactorDetailDto`, `CarbonRecommendationDto`.
- `Web/Components/Pages/Reporting/CarbonFootprint/TransportEmissionsAnalysis.razor` — página `/reporting/carbon-footprint/transport-emissions`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Eficiencia de flota (cards KPI) | `WasteMoveResidues` | kgCO₂e/recogida, km/recogida, kgCO₂e/km, kgCO₂e/t·km, % Euro 6+, variación % |
| Comparativa pre/post (bar chart) | `WasteMoveResidues` (dos periodos) | km/recogida, kgCO₂e/recogida, kgCO₂e/t, % Euro 6+ — selección de mes/trimestre A vs B |
| Mapa de rutas con emisiones | `Entities` (origen + destino) + `WasteMoveResidues` | Líneas origen-destino con grosor proporcional a emisiones. Popup con detalle |
| Ranking por transportista (tabla) | `WasteMoveResidues.IdCarrier` → `Entities.Name` | Distancia, emisiones, intensidad/km, intensidad/t, clase Euro predominante. Semáforo vs media |
| Heatmap horario de emisiones (7×24) | `WasteMoves.ActualPickupStart` × `WasteMoveResidues.TransportInfo_TransportCarbonEmissions` | Identificar franjas de alta emisión para redistribución |
| Detalle factores de emisión (tabla) | `EmissionFactorSets` + `EmissionFactors` (Active) | Transparencia sobre factores aplicados. Solo lectura |
| Recomendaciones automáticas (panel) | Calculado en `CarbonFootprintCalculationService.cs` | Motor de reglas: % flota antigua > 30%, intensidad > 110% media, transportista > 150% media, distancia +15% |

---

#### 5.7.3. HC-C — Panel de Huella Energética de Plantas de Tratamiento

- **Ruta**: `/reporting/carbon-footprint/plant-energy`
- **Perfil objetivo**: `PLANT_OP`, `SCRAP`, `DISPATCH_OFFICE`, `ADMIN`.
- **Policy**: `CanViewPlantEnergyFootprint`.
- **Lógica**: análisis de emisiones Scope 2 derivadas del consumo energético de las instalaciones de reciclaje y tratamiento.

**Archivos a crear**:
- `Application/Features/Reporting/CarbonFootprint/Queries/GetPlantEnergyFootprintQuery.cs` — filtros: `Year`, `Month?`, `PlantName?`, `Source?`, `ProvinceCode?`.
- `Application/Features/Reporting/CarbonFootprint/DTOs/PlantEnergyFootprintDto.cs` — DTOs: `PlantEnergySummaryDto`, `PlantComparisonDto`, `EnergyBySourceDto`, `TreatmentEfficiencyDto`.
- `Web/Components/Pages/Reporting/CarbonFootprint/PlantEnergyFootprint.razor` — página `/reporting/carbon-footprint/plant-energy`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Resumen consumo (cards KPI) | `PlantEnergies` + `EntryPlants` | kWh totales, kgCO₂e Scope 2, kgCO₂e/t tratada, variación % |
| Comparativa entre plantas (bar chart) | `PlantEnergies` por `PlantName` | kWh y kgCO₂e/t por planta. Línea de intensidad superpuesta |
| Evolución mensual (area chart) | `PlantEnergies` por `Year`/`Month` | Series por planta, eje derecho opcional con toneladas tratadas |
| Desglose por fuente energética (pie/donut) | `PlantEnergies.Source` | Mix energético: red, solar, eólica, gas, etc. |
| Eficiencia por operación (tabla) | `TreatmentPlants` + `TreatmentOperations` + `PlantEnergies` | Prorrateo por volumen si `AllocationMethod` no disponible |
| Exportación XLSX | Dataset consolidado | Planta, año, mes, kWh, fuente, mix, método, toneladas, kgCO₂e, intensidad |

---

#### 5.7.4. HC-D — Reporte de Huella de Carbono para Productores

- **Ruta**: `/reporting/carbon-footprint/producer-report`
- **Perfil objetivo**: `PRODUCER`, `ADMIN`.
- **Policy**: `CanViewProducerCarbonReport`.
- **Lógica**: permite que los fabricantes de residuos industriales evalúen el impacto ambiental de la gestión de sus residuos, en línea con el «Objetivo 55».

**Archivos a crear**:
- `Application/Features/Reporting/CarbonFootprint/Queries/GetProducerCarbonReportQuery.cs` — filtros: `Year`, `Month?`/`Quarter?`, `LERCode?`, `WasteStream?`.
- `Application/Features/Reporting/CarbonFootprint/DTOs/ProducerCarbonReportDto.cs` — DTOs: `ProducerEmissionsSummaryDto`, `EmissionsByResidueTypeDto`, `EmissionsByDestinationDto`, `ProducerVsEcosystemDto`.
- `Web/Components/Pages/Reporting/CarbonFootprint/ProducerCarbonReport.razor` — página `/reporting/carbon-footprint/producer-report`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Resumen huella productor (cards KPI) | `WasteMoveResidues` WHERE `ServiceOrders.IdIssuedBy = LinkedEntityId` | Emisiones totales, toneladas, intensidad, nº traslados, variación % |
| Evolución mensual (line chart) | Mismo filtro, agrupado mes | Emisiones y intensidad: tendencia interanual para auditorías |
| Desglose por tipo de residuo (bar chart) | `WasteMoveResidues` + `Residues` + `LERCodes` | Emisiones, peso, intensidad, nº traslados por código LER + descripción |
| Desglose por destino (tabla) | `WasteMoves.IdDestination` → `Entities.Name` | Planta, distancia promedio, emisiones, intensidad — evaluar cambio de planta |
| Comparativa vs media ecosistema (gauge) | Intensidad productor vs media del tenant | Zonas: verde (< media), amarillo (= media), rojo (> media) |
| Certificado descargable (XLSX) | Resumen del periodo con KPIs | Documento para memorias de sostenibilidad y declaraciones ambientales |

---

#### 5.7.5. HC-E — Panel de Emisiones para Entidades Públicas

- **Ruta**: `/reporting/carbon-footprint/public-view`
- **Perfil objetivo**: `PUBLIC_ENT`, `ADMIN`.
- **Policy**: `CanViewPublicEntityCarbonView`.
- **Lógica**: los ayuntamientos monitorizan las emisiones de CO₂ generadas por la gestión de residuos en su ámbito territorial.

**Archivos a crear**:
- `Application/Features/Reporting/CarbonFootprint/Queries/GetPublicEntityCarbonViewQuery.cs` — filtros: `Year`, `Month?`, `IdScrap?`, `WasteStream?`, `LERCode?`.
- `Application/Features/Reporting/CarbonFootprint/DTOs/PublicEntityCarbonViewDto.cs` — DTOs: `MunicipalEmissionsSummaryDto`, `EmissionsByScrapInMunicipalityDto`, `FuelMixEvolutionDto`, `EmissionAlertDto`.
- `Web/Components/Pages/Reporting/CarbonFootprint/PublicEntityCarbonView.razor` — página `/reporting/carbon-footprint/public-view`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Resumen emisiones municipio (cards KPI) | `WasteMoveResidues` filtrado por `MunicipalityCode` del punto de recogida o `IdIssuedBy` | Emisiones totales, toneladas, intensidad, nº traslados, variación % |
| Evolución mensual (line chart) | Mismo filtro, agrupado mes | Análogo a HC-D pero para ámbito territorial |
| Desglose por SCRAP (tabla) | `WasteMoves.IdScrap` → `Entities.Name`, filtrado municipio | Emisiones, toneladas, intensidad, distancia promedio. Semáforo por intensidad |
| Comparativa mensual por combustible (stacked bar) | `WasteMoveResidues.FuelType` agrupado mes, filtrado municipio | Evolución del mix de combustibles en el territorio |
| Notificaciones de umbrales (inbox) | Calculado en backend | Alertas: intensidad > media tenant, emisiones mensuales +Y% vs anterior |

---

#### Estructura de archivos del módulo

**Capa Web (Blazor)**:

```
Web/Components/Pages/Reporting/CarbonFootprint/
├── CarbonFootprintOverview.razor            → /reporting/carbon-footprint/overview
├── TransportEmissionsAnalysis.razor         → /reporting/carbon-footprint/transport-emissions
├── PlantEnergyFootprint.razor               → /reporting/carbon-footprint/plant-energy
├── ProducerCarbonReport.razor               → /reporting/carbon-footprint/producer-report
└── PublicEntityCarbonView.razor             → /reporting/carbon-footprint/public-view
```

**Capa Application (CQRS)**:

```
Application/Features/Reporting/CarbonFootprint/
├── Queries/
│   ├── GetCarbonFootprintOverviewQuery.cs
│   ├── GetTransportEmissionsAnalysisQuery.cs
│   ├── GetPlantEnergyFootprintQuery.cs
│   ├── GetProducerCarbonReportQuery.cs
│   ├── GetPublicEntityCarbonViewQuery.cs
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
    └── CarbonFootprintCalculationService.cs         → Agregación Scope 2, recomendaciones, umbrales
```

**Componentes reutilizables**:

```
Web/Components/Shared/CarbonFootprint/
├── EmissionsSummaryCards.razor               → Cards KPI de emisiones reutilizables
├── EmissionsTrendChart.razor                 → Gráfico de evolución temporal
├── VehicleTypeEmissionsDonut.razor           → Donut por tipo de vehículo
├── FuelTypePieChart.razor                    → Pie por tipo de combustible
├── CarbonIntensityGauge.razor               → Gauge de intensidad CO₂
└── PlantEnergyComparisonBar.razor           → Bar chart comparativa de consumo entre plantas
```

> **Reutilizar** de los dashboards existentes: `EmissionsCard.razor`, `WasteVolumeMap.razor` (para mapa con capa de emisiones).

---

#### Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas.** Todo el módulo de Huella de Carbono se alimenta de las tablas existentes del modelo v4.1.

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
| `Province` | `id`, `idState`, `Ref`, `Code`, **`Name`** |
| `Municipality` | `Id`, `Id_Province`, `Code`, **`Name`** |
| `TerritoryState` | `id`, `idCountry`, `Ref`, `Code`, **`Name`** |

---

#### Reglas de autorización y filtrado de datos

> **IMPORTANTE**: El acceso a estos dashboards se gestiona mediante el **sistema de autorización por pantalla configurable desde la interfaz de administración** (`/security/page-permissions`) utilizando las tablas `PageDefinitions` y `PagePermissions`. **No se hardcodea el acceso en código.** Las policies en código actúan como mínimo de seguridad estático; el control fino se delega al sistema dinámico de BD.

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | HC-A, HC-B | Solo ve datos donde `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. |
| `PRODUCER` | HC-D | Solo ve traslados cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`). |
| `CARRIER` | HC-B | Solo ve traslados donde está asignado (`WasteMoveResidues.IdCarrier = LinkedEntityId`). |
| `PLANT_OP` | HC-C | Solo ve `PlantEnergies` de su planta y `EntryPlants` de su planta. |
| `PUBLIC_ENT` | HC-E | Solo ve traslados cuyo punto de recogida pertenece a su municipio (`Entities.MunicipalityCode`), o cuya SO fue emitida por su entidad. |
| `COORDINATOR` | HC-A | Ve transversalmente los SCRAPs vinculados a sus acuerdos (`Agreements.IdCoordinator = LinkedEntityId`). |
| `DISPATCH_OFFICE` | HC-A, HC-B, HC-C | Ve todos los datos del tenant (`OwnerId`). Visión completa equivalente a ADMIN. |
| `ADMIN` | HC-A, HC-B, HC-C, HC-D, HC-E | Sin restricciones dentro del tenant. |

**Patrón de filtrado**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` + `IDataScopeService.ApplyScope()` (ya implementado).

---

#### Regla obligatoria: Datos geográficos siempre como nombre

En **todas** las pantallas, tablas, filtros y exportaciones de este módulo:
- `ProvinceCode` → resolver siempre a `Province.Name` (JOIN con tabla `Province`).
- `MunicipalityCode` → resolver siempre a `Municipality.Name` (JOIN con tabla `Municipality`).
- `StateCode` → resolver siempre a `TerritoryState.Name`.
- En selectores/filtros: mostrar `Name` como label, `Code` como value interno.
- En exportaciones XLSX: columnas con **nombre**, no código.

---

#### Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. El acceso a cada dashboard **no está hardcodeado en código**. Se gestiona mediante el sistema de autorización por pantalla (`PageDefinitions`/`PagePermissions`) configurable desde `/security/page-permissions`.
3. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
4. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en HC-A y HC-C como mínimo (patrón ClosedXML).
7. Responsive mobile-first.
8. Modo oscuro/claro (consistente con `MainLayout.razor`).
9. El **factor de conversión kWh → kgCO₂e** para Scope 2 es configurable (en `appsettings.json`), no hardcodeado.
10. Los **umbrales de recomendaciones y alertas** son configurables (en `appsettings.json`).
11. Las recomendaciones se generan en el backend (`CarbonFootprintCalculationService.cs`), no en el cliente.
12. **No se crean nuevas entidades de dominio.** Todo se implementa con las entidades del modelo v4.1.
13. Cada usuario solo ve datos de las entidades asignadas a él o creadas por él, **a excepción de `ADMIN` y `DISPATCH_OFFICE`** que ven todos los datos del tenant.
14. Datos geográficos (provincia, municipio) se muestran siempre como **nombre**, nunca como código.

---

#### Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de emisiones totales e intensidad pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **Trazabilidad (§5.1)**: desde cualquier traslado en los dashboards HC, enlace directo a `/traceability?term={WasteMoveReference}`.
- **KPIs regulatorios (§5.2)**: el KPI de "intensidad CO₂ por tonelada" ya existe en `/kpis`; aquí se desglosa por vehículo, combustible, ruta, zona, SCRAP, transportista.
- **Dashboard Optimización Logística (§5.4.1)**: comparte fuentes de datos. `EmissionsCard.razor` reutilizable.
- **Módulo de Movilidad Urbana (§5.6)**: enlace cruzado desde HC-A al UC3-A para correlacionar emisiones con impacto en movilidad.
- **Mapas de Calor (§5.7 HeatMaps)**: enlace cruzado desde HC-A para correlacionar zonas de alta densidad con alta emisión.
- **Incidencias (§4.3)**: averías de vehículos y retrasos se correlacionan con picos de emisiones.
- **Consumo energético de plantas (§4.2)**: HC-C extiende la vista existente de `PlantEnergies` añadiendo la perspectiva Scope 2.

---

### 5.8. Módulo de Análisis y Cumplimiento Normativo — RAP (UC-CN)

> Agrupa las vistas orientadas a **monitorizar, certificar y auditar el cumplimiento de la Responsabilidad Ampliada del Productor (RAP)** según la Ley 22/2011 de residuos y suelos contaminados. El sistema centraliza la información normativa de diferentes SCRAPs, monitorea su cumplimiento, identifica áreas de riesgo e incumplimiento, y facilita decisiones correctivas.
>
> Los datos explotados incluyen: cumplimiento de cada SCRAP con regulaciones locales y nacionales (tasas de reciclaje, certificados de tratamiento, disposición final), datos de convenios con comunidades autónomas (características del servicio, facturas de compensación), y cuotas de mercado con verificación del principio de proporcionalidad.
>
> **Participantes clave**: AENOR Confía (verificación y certificación), Oficina de Coordinación de SCRAP RAEE / OFIRAEE (datos de convenios y objetivos de recogida por flujos), SCRAP Cartón Circular (envases comerciales e industriales), ECOEMBES (envases domésticos — adherencia futura), entes locales (toneladas por metodologías de recolección).
>
> **No se crean nuevas entidades de dominio.** Todo se implementa con las entidades del modelo v4.1 existente.

#### 5.8.1. CN-A — Panel de Cumplimiento Normativo — Visión SCRAP

- **Ruta**: `/reporting/regulatory-compliance/scrap-overview`
- **Perfil objetivo**: `SCRAP`, `ADMIN`.
- **Policy**: `CanViewScrapComplianceOverview`.
- **Lógica**: cada SCRAP visualiza su propio nivel de cumplimiento normativo: tasas de reciclaje alcanzadas, progreso de objetivos de recogida, estado de sus convenios y alertas de riesgo de incumplimiento.

**Archivos a crear**:
- `Application/Features/Reporting/RegulatoryCompliance/Queries/GetScrapComplianceOverviewQuery.cs` — handler principal con filtros: `Year`, `Quarter?`/`Month?`, `AutonomousCommunity?`, `Category?`, `FlowType?`.
- `Application/Features/Reporting/RegulatoryCompliance/DTOs/ScrapComplianceOverviewDto.cs` — DTOs: `ScrapComplianceOverviewDto`, `ComplianceKpiDto`, `QuarterlyComplianceTrendDto`, `MarketShareComplianceRowDto`, `AgreementStatusRowDto`, `SettlementSummaryDto`, `ComplianceAlertDto`.
- `Application/Features/Reporting/RegulatoryCompliance/Services/ComplianceMonitoringService.cs` — motor de alertas, cálculo de desviaciones y umbrales.
- `Web/Components/Pages/Reporting/RegulatoryCompliance/ScrapComplianceOverview.razor` — página `/reporting/regulatory-compliance/scrap-overview`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Resumen ejecutivo (cards KPI) | `TreatmentPlantResidues` + `TreatmentOperations` + `EntryPlantResidues` + `MarketShares` + `Agreements` | Tasa reciclaje, tasa valorización, tasa reutilización, % cumplimiento cuotas, nº convenios activos, variación %. Semáforo vs `RegulatoryTargets` |
| Evolución trimestral de tasas (line chart) | Agregación trimestral `TreatmentPlantResidues` + `TreatmentOperations` | Series: reciclaje, valorización, reutilización. Línea de referencia = objetivo regulatorio |
| Cumplimiento cuotas por categoría y CCAA (tabla + barras progreso) | `MarketShares` + `EntryPlantResidues` | Categoría, comunidad autónoma (**nombre**), flujo, objetivo kg, real kg, %, estado. Sparkline mensual |
| Estado de convenios (tabla + timeline) | `Agreements WHERE IdScrap = LinkedEntityId` + `Entities` | Nº acuerdo, ent. pública (**nombre**), CCAA, provincia (**nombre**), municipio (**nombre**), flujo, estado, vigencia, días vencimiento. Semáforo vencimiento |
| Liquidaciones (bar chart apilado + tabla) | `Settlements WHERE IdScrap = LinkedEntityId` | Importe por mes apilado por `ValidationStatus`. Tabla: nº, año, mes, ent. pública, total, estado |
| Alertas de incumplimiento (panel inbox) | Calculado en `ComplianceMonitoringService.cs` | Tasa bajo objetivo, cuota < 80%, convenio < 30 días, liquidación rechazada |

---

#### 5.8.2. CN-B — Panel de Auditoría de Cuotas de Mercado — Reparto entre SCRAPs

- **Ruta**: `/reporting/regulatory-compliance/market-share-audit`
- **Perfil objetivo**: `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`.
- **Policy**: `CanViewMarketShareAudit`.
- **Lógica**: la Oficina de Coordinación y los coordinadores auditan el reparto proporcional de la responsabilidad entre SCRAPs, verificando que cada uno asume su cuota de mercado correspondiente (principio de proporcionalidad por cuota de mercado según Ley 22/2011).

**Archivos a crear**:
- `Application/Features/Reporting/RegulatoryCompliance/Queries/GetMarketShareAuditQuery.cs` — filtros: `Year`, `AutonomousCommunity?`, `FlowType?`, `Category?`, `IdScrap?`.
- `Application/Features/Reporting/RegulatoryCompliance/DTOs/MarketShareAuditDto.cs` — DTOs: `MarketShareAuditDto`, `ScrapProportionalityDto`, `ComplianceHeatmapCellDto`, `DeviationIndexDto`, `AuditExportRowDto`.
- `Application/Features/Reporting/RegulatoryCompliance/Queries/ExportComplianceDataToExcelQuery.cs` — exportación XLSX (patrón ClosedXML).
- `Web/Components/Pages/Reporting/RegulatoryCompliance/MarketShareAudit.razor` — página `/reporting/regulatory-compliance/market-share-audit`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Proporcionalidad global (donut + tabla resumen) | `MarketShares` agrupado por `IdScrap` | Donut: peso objetivo por SCRAP como % del total. Tabla: SCRAP (**nombre**), objetivo, real, %, desviación |
| Comparativa SCRAP × categoría (heatmap tabla) | `MarketShares` + `EntryPlantResidues` | Filas: SCRAP. Columnas: categorías. Celdas: % cumplimiento con color (verde/naranja/rojo) |
| Evolución mensual reparto real vs objetivo (stacked area) | `EntryPlantResidues.Weight` agrupado mes × SCRAP | Áreas por SCRAP. Líneas referencia: objetivo mensual prorrateado |
| Desglose por CCAA y flujo (tabla expandible) | `MarketShares` filtrada | Filas agrupables: CCAA (**nombre**) → SCRAP → categoría. Columnas: objetivo, real, %, estado |
| Índice de desviación por SCRAP (bar chart horizontal) | Cálculo: `(Real - Objetivo) / Objetivo × 100` | Barras verdes (superan) y rojas (por debajo). Umbral ±15% configurable |
| Exportación XLSX | Dataset completo de auditoría | SCRAP, categoría, CCAA, flujo, objetivo, real, desviación, % |

---

#### 5.8.3. CN-C — Panel de Monitorización de Convenios — Coordinador

- **Ruta**: `/reporting/regulatory-compliance/agreement-monitoring`
- **Perfil objetivo**: `COORDINATOR`, `DISPATCH_OFFICE`, `ADMIN`.
- **Policy**: `CanViewAgreementComplianceMonitoring`.
- **Lógica**: monitorizar el estado y cumplimiento de los convenios entre SCRAPs y entidades públicas, verificar las tarifas de compensación, y detectar desviaciones en los servicios pactados.

**Archivos a crear**:
- `Application/Features/Reporting/RegulatoryCompliance/Queries/GetAgreementComplianceMonitoringQuery.cs` — filtros: `Year`, `Month?`, `IdScrap?`, `AutonomousCommunity?`, `ProvinceCode?`, `MunicipalityCode?`, `WasteStream?`, `SubStream?`, `AgreementStatus?`.
- `Application/Features/Reporting/RegulatoryCompliance/DTOs/AgreementComplianceMonitoringDto.cs` — DTOs: `AgreementComplianceMonitoringDto`, `AgreementKpiDto`, `AgreementRowDto`, `SettlementTrackingDto`, `ServiceVsCommitmentDto`.
- `Web/Components/Pages/Reporting/RegulatoryCompliance/AgreementComplianceMonitoring.razor` — página `/reporting/regulatory-compliance/agreement-monitoring`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Resumen ejecutivo (cards KPI) | `Agreements` + `Settlements` | Convenios activos, próximos a vencer (< 90 días), liquidaciones pendientes, importe liquidado (año), variación % |
| Cobertura de convenios por CCAA (bar chart agrupado) | `Agreements` agrupado por `AutonomousCommunity` (**nombre**) | Barras por SCRAP dentro de cada CCAA. Línea: toneladas gestionadas |
| Estado de convenios (tabla interactiva) | `Agreements` + `Entities` (JOINs) | SCRAP, ent. pública, CCAA, provincia, municipio (**nombres**), flujo, subflujo, estado, vigencia, tarifa. Semáforo |
| Seguimiento liquidaciones (line chart + tabla) | `Settlements` últimos 12 meses | Evolución mensual importe por SCRAP. Tabla: nº, SCRAP, ent. pública, importes, estado |
| Servicios prestados vs compromisos (tabla) | `WasteMoves` + `ServiceOrders` + `Agreements` | Por convenio: servicios realizados, toneladas vs mínimos (`MinimumsJson`). Semáforo |
| Alertas de convenios (panel inbox) | `ComplianceMonitoringService.cs` | Vencimiento < 30 días, liquidación rechazada, servicios bajo mínimos |

---

#### 5.8.4. CN-D — Panel de Cumplimiento Normativo — Entidad Pública

- **Ruta**: `/reporting/regulatory-compliance/public-view`
- **Perfil objetivo**: `PUBLIC_ENT`, `ADMIN`.
- **Policy**: `CanViewPublicEntityComplianceView`.
- **Lógica**: los ayuntamientos y entidades públicas monitorizan que los SCRAPs con los que tienen convenios cumplen con sus obligaciones: toneladas recogidas, servicios prestados, liquidaciones de compensación y cumplimiento de la legislación en su ámbito territorial.

**Archivos a crear**:
- `Application/Features/Reporting/RegulatoryCompliance/Queries/GetPublicEntityComplianceViewQuery.cs` — filtros: `Year`, `Month?`, `IdScrap?`, `WasteStream?`, `Category?`.
- `Application/Features/Reporting/RegulatoryCompliance/DTOs/PublicEntityComplianceViewDto.cs` — DTOs: `PublicEntityComplianceViewDto`, `ServiceReceivedKpiDto`, `ScrapComplianceInTerritoryDto`, `SettlementDetailDto`, `CollectionMethodDistributionDto`.
- `Web/Components/Pages/Reporting/RegulatoryCompliance/PublicEntityComplianceView.razor` — página `/reporting/regulatory-compliance/public-view`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Resumen servicios recibidos (cards KPI) | `EntryPlantResidues` + `WasteMoves` + `Settlements` filtrado municipio | Toneladas, nº servicios, nº SCRAPs operando, importe compensado, variación % |
| Evolución mensual por SCRAP (bar chart apilado) | `EntryPlantResidues.Weight` mes × SCRAP | Barras: mes. Segmentos: SCRAP. Línea: objetivo prorrateado `MarketShares` |
| Cumplimiento por SCRAP en territorio (tabla + semáforo) | `MarketShares` + `EntryPlantResidues` filtrado CCAA | SCRAP, objetivo, real, %, categoría, flujo. `ProgressBar.razor` |
| Liquidaciones de compensación (tabla) | `Settlements WHERE IdPublicEntity = LinkedEntityId` | SCRAP, nº, año, mes, importes, estado, validación. Totales acumulados |
| Toneladas por método recolección (donut) | `Agreements.CoveredMethodsJson` + `ServiceOrders` + `WasteMoves` | Distribución por método cubierto en convenios |
| Incidencias y reclamaciones (tabla + badge) | `Incidents WHERE ClosedAt IS NULL` ámbito municipio | Tipo, severidad, traslado, SCRAP, fecha, días abierta. `IncidentsBadge.razor` |

---

#### 5.8.5. CN-E — Panel de Datos de Cumplimiento — Oficina de Asignación

- **Ruta**: `/reporting/regulatory-compliance/dispatch-data`
- **Perfil objetivo**: `DISPATCH_OFFICE`, `ADMIN`.
- **Policy**: `CanViewDispatchOfficeComplianceData`.
- **Lógica**: la Oficina de Asignación y Coordinación consolida todos los datos de cumplimiento normativo del ecosistema. Provee datasets exportables para auditorías externas (AENOR Confía) y análisis regulatorio.

**Archivos a crear**:
- `Application/Features/Reporting/RegulatoryCompliance/Queries/GetDispatchOfficeComplianceDataQuery.cs` — filtros: `Year`, `IdScrap?`, `AutonomousCommunity?`, `FlowType?`, `Category?`.
- `Application/Features/Reporting/RegulatoryCompliance/DTOs/DispatchOfficeComplianceDataDto.cs` — DTOs: `DispatchOfficeComplianceDataDto`, `EcosystemKpiDto`, `ScrapRankingDto`, `GeographicComplianceHeatmapDto`, `RegulatoryChangeDto`.
- `Web/Components/Pages/Reporting/RegulatoryCompliance/DispatchOfficeComplianceData.razor` — página `/reporting/regulatory-compliance/dispatch-data`.

**Widgets principales**:

| Widget | Fuente de datos | Descripción |
|---|---|---|
| Dashboard ejecutivo (cards KPI) | Todo el tenant | Tasa reciclaje global, valorización, reutilización, nº SCRAPs, nº convenios, importe liquidado, variación % |
| Ranking SCRAPs por cumplimiento (bar chart horizontal + tabla) | `MarketShares` + `EntryPlantResidues` + `TreatmentPlantResidues` | % cumplimiento por SCRAP descendente. Línea vertical 100%. Tabla: SCRAP, objetivo, real, tasas, convenios, importe |
| Tabla exportable para auditoría (XLSX) | Join completo operativo | Dataset: SCRAP, categoría, CCAA (**nombre**), provincia (**nombre**), municipio (**nombre**), flujo, año, objetivo, real, %, tasas. Patrón ClosedXML |
| Evolución interanual (line chart multi-año) | Agregación anual tasas ecosistema | Líneas referencia: `RegulatoryTargets`. Tendencias plurianuales |
| Mapa calor cumplimiento geográfico (tabla coloreada) | `MarketShares` + `EntryPlantResidues` | Filas: CCAA (**nombre**). Columnas: SCRAPs. Celdas: % con gradiente color |
| Resumen cambios normativos (panel timeline) | `RegulatoryTargets` + `EmissionFactorSets` + `EcoModulationRuleSets` | Cambios recientes: nuevos objetivos, nuevos factores, nuevas reglas eco-modulación |

---

#### Estructura de archivos del módulo

**Capa Web (Blazor)**:

```
Web/Components/Pages/Reporting/RegulatoryCompliance/
├── ScrapComplianceOverview.razor                → /reporting/regulatory-compliance/scrap-overview
├── MarketShareAudit.razor                       → /reporting/regulatory-compliance/market-share-audit
├── AgreementComplianceMonitoring.razor          → /reporting/regulatory-compliance/agreement-monitoring
├── PublicEntityComplianceView.razor             → /reporting/regulatory-compliance/public-view
└── DispatchOfficeComplianceData.razor           → /reporting/regulatory-compliance/dispatch-data
```

**Capa Application (CQRS)**:

```
Application/Features/Reporting/RegulatoryCompliance/
├── Queries/
│   ├── GetScrapComplianceOverviewQuery.cs       → CN-A (SCRAP)
│   ├── GetMarketShareAuditQuery.cs              → CN-B (COORDINATOR / DISPATCH_OFFICE)
│   ├── GetAgreementComplianceMonitoringQuery.cs → CN-C (COORDINATOR / DISPATCH_OFFICE)
│   ├── GetPublicEntityComplianceViewQuery.cs    → CN-D (PUBLIC_ENT)
│   ├── GetDispatchOfficeComplianceDataQuery.cs  → CN-E (DISPATCH_OFFICE)
│   ├── GetComplianceAlertsSummaryQuery.cs       → Widget compartido: alertas
│   ├── GetRecyclingRateByFlowQuery.cs           → Widget compartido: tasa reciclaje por flujo
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

**Componentes reutilizables**:

```
Web/Components/Shared/RegulatoryCompliance/
├── ComplianceGaugeCard.razor                    → Gauge circular de % cumplimiento con semáforo
├── MarketShareProportionalityChart.razor        → Gráfico de proporcionalidad real vs objetivo
├── ComplianceAlertInbox.razor                   → Panel de alertas tipo inbox con severidad
├── RecyclingRateProgressBar.razor               → Barra de progreso tasa reciclaje (extiende ProgressBar.razor)
└── AgreementStatusTimeline.razor                → Timeline horizontal del estado de convenios
```

> **Reutilizar** de los dashboards existentes: `ProgressBar.razor`, `EmissionsCard.razor`, `IncidentsBadge.razor`.

---

#### Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el UC-CN se alimenta de las tablas existentes del modelo v4.1. Las métricas derivadas (tasas de reciclaje, desviaciones, índices de cumplimiento) se calculan en las Queries CQRS y en `ComplianceMonitoringService.cs`.

| Tabla | Campos principales para este UC-CN |
|-------|--------------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `ProvinceCode`, `MunicipalityCode`, `AutonomousCommunity` |
| `MarketShares` | `Id`, `IdScrap`, `Category`, `AutonomousCommunity`, `Year`, `Weight`, `Period`, `EffectiveFrom`, `EffectiveTo`, `FlowType`, `OwnerId` |
| `Agreements` | `Id`, `AgreementNumber`, `Status`, `EffectiveFrom`, `EffectiveTo`, `IdScrap`, `IdPublicEntity`, `IdCoordinator`, `WasteStream`, `SubStream`, `AutonomousCommunity`, `ProvinceCode`, `MunicipalityCode`, `TariffModelType`, `TariffRulesJson`, `MinimumsJson`, `CoveredMethodsJson`, `OwnerId` |
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

---

#### Reglas de autorización y filtrado de datos

> **IMPORTANTE**: El acceso a estos dashboards se gestiona mediante el **sistema de autorización por pantalla configurable desde la interfaz de administración** (`/security/page-permissions`) utilizando las tablas `PageDefinitions` y `PagePermissions`. **No se hardcodea el acceso en código.** Las policies en código actúan como mínimo de seguridad estático; el control fino se delega al sistema dinámico de BD.

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | CN-A | Solo ve sus propios datos: `WasteMoves.IdScrap = LinkedEntityId` OR `IdScrap2 = LinkedEntityId`. `MarketShares.IdScrap = LinkedEntityId`. `Agreements.IdScrap = LinkedEntityId`. `Settlements.IdScrap = LinkedEntityId`. |
| `COORDINATOR` | CN-B, CN-C | Ve transversalmente los SCRAPs vinculados a sus acuerdos: `Agreements.IdCoordinator = LinkedEntityId`. |
| `PUBLIC_ENT` | CN-D | Solo ve datos del ámbito de su municipio: punto de recogida → `Entities.MunicipalityCode` = municipio de su entidad, o `ServiceOrders.IdIssuedBy = LinkedEntityId`. Convenios: `Agreements.IdPublicEntity = LinkedEntityId`. Liquidaciones: `Settlements.IdPublicEntity = LinkedEntityId`. |
| `DISPATCH_OFFICE` | CN-B, CN-C, CN-E | Ve todos los datos del tenant (`OwnerId`). Visión operativa completa. |
| `ADMIN` | CN-A, CN-B, CN-C, CN-D, CN-E | Sin restricciones dentro del tenant. |

**Patrón de filtrado**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` + `IDataScopeService.ApplyScope()` (ya implementado).

---

#### Regla obligatoria: Datos geográficos siempre como nombre

En **todas** las pantallas, tablas, filtros y exportaciones de este módulo:
- `ProvinceCode` → resolver siempre a `Province.Name` (JOIN con tabla `Province`).
- `MunicipalityCode` → resolver siempre a `Municipality.Name` (JOIN con tabla `Municipality`).
- `AutonomousCommunity` → mostrar siempre como nombre legible.
- En selectores/filtros: mostrar `Name` como label, `Code` como value interno.
- En exportaciones XLSX: columnas con **nombre**, no código.

---

#### Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. El acceso a cada dashboard **no está hardcodeado en código**. Se gestiona mediante el sistema de autorización por pantalla (`PageDefinitions`/`PagePermissions`) configurable desde `/security/page-permissions`.
3. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
4. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en CN-B y CN-E como mínimo (patrón ClosedXML).
7. Responsive mobile-first.
8. Modo oscuro/claro (consistente con `MainLayout.razor`).
9. Los **umbrales de alertas** (% cumplimiento, días vencimiento) son configurables en `appsettings.json`, no hardcodeados.
10. Las alertas y recomendaciones se generan en el backend (`ComplianceMonitoringService.cs`), no en el cliente.
11. **No se crean nuevas entidades de dominio.** Todo se implementa con las entidades del modelo v4.1.
12. Cada usuario solo ve datos de las entidades asignadas a él o creadas por él, **a excepción de `ADMIN` y `DISPATCH_OFFICE`** que ven todos los datos del tenant.
13. Datos geográficos (provincia, municipio, comunidad autónoma) se muestran siempre como **nombre**, nunca como código.
14. **Todos los dashboards incluyen al menos un gráfico** (chart de ApexCharts): no se permiten dashboards compuestos exclusivamente de tablas y cards.

---

#### Configuración en `appsettings.json`

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

#### Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de tasa de reciclaje y cumplimiento de cuotas pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **KPIs regulatorios (§5.2)**: el módulo de KPIs (`/kpis`) ya calcula tasas de reciclaje y cumplimiento de `MarketShares`. Los dashboards CN complementan con la dimensión de auditoría inter-SCRAP, convenios y liquidaciones. Reutilizar el patrón de `GetRegulatoryKpisQuery` y `GetMarketShareComplianceQuery`.
- **Trazabilidad (§5.1)**: desde cualquier traslado en los dashboards CN, enlace directo a `/traceability?term={WasteMoveReference}`.
- **Dashboard Monitorización Pública (§5.4.2)**: CN-D extiende la visión de la entidad pública. Enlace cruzado desde CN-D a `/logistics/public-monitoring`.
- **Cuotas de Mercado (§2.3)**: la vista `/market-shares` muestra el CRUD de cuotas; los dashboards CN explotan esos datos en forma analítica. Reutilizar `ProgressBar.razor`.
- **Acuerdos (§2.1)**: desde tablas de convenios en CN-C, enlace directo a `/agreements/{id}`.
- **Liquidaciones (§2.2)**: desde tablas de liquidaciones en CN-A/CN-D, enlace directo al detalle del settlement.
- **Incidencias (§4.3)**: los widgets de incidencias enlazan a `/incidents/{id}`.
- **Huella de Carbono (§5.7)**: enlace cruzado desde CN-A para contextualizar el cumplimiento con el impacto ambiental.
- **Mapas de Calor (§5.8 HeatMaps)**: enlace cruzado desde CN-E para correlacionar cumplimiento geográfico con densidad de residuos.

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


## 📊 Dashboards de Optimización Logística RAEE (UC2)

## 🎯 Objetivo

Crear un **módulo de dashboard(s) de optimización logística RAEE** que permita a los SCRAP optimizar sus servicios de recolección de residuos desde los puntos de recogida hasta las plantas de tratamiento. El módulo debe centralizar datos logísticos (ubicación, volumen, rutas, capacidad vehicular, condiciones de tráfico, horarios de planta) y cruzarlos con las políticas de Distribución Urbana de Mercancía (DUM) para reducir emisiones de CO₂ y costos operativos.

---

## 📊 Dashboards a crear (son TRES vistas diferenciadas)

### Dashboard 1 — **Panel de Optimización Logística SCRAP** (`/logistics/optimization`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Propósito**: vista estratégica para que los SCRAP analicen y optimicen sus rutas de recogida de RAEE.

#### Widgets / KPIs requeridos:

1. **Mapa interactivo de puntos de recogida y plantas**
   - Fuente: `Entities` (filtrar por `EntityRole IN ('Producer', 'CAC', 'PublicEntity')`) con `Latitude`/`Longitude` para puntos de recogida; `Entities` con `EntityRole = 'Plant'` para destinos.
   - Capa adicional: polígonos de `DUMZones.GeometryJson` con estado activo (`Status = 'Active'`), coloreados según `DUMRestrictionRules.ActionType` (`Block` = rojo, `Restrict` = naranja, `Allow` = verde, `Notify` = azul).
   - Iconografía diferenciada por `EntityRole`.
   - Al hacer clic en un punto: popup con nombre, dirección, volumen acumulado del periodo y próximas recogidas planificadas.

2. **Volumen RAEE por zona geográfica** (bar chart / treemap)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `ServiceOrders` → agrupar por `Entities.ProvinceCode` o `MunicipalityCode` del `IdPickupPoint`.
   - Filtrar por `ServiceOrders.WasteStream = 'RAEE'` (o el valor equivalente configurado).
   - Métricas: `SUM(WasteMoveResidues.Weight)` agrupado por zona, periodo (mes/trimestre) y `LERCodes.Code`.

3. **Eficiencia de rutas** (card + trend line)
   - Fuente: `WasteMoveResidues` → campos `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`.
   - KPIs:
     - **km promedio por recogida** = `AVG(TransportInfo_TransportDistance)`.
     - **kgCO₂e promedio por recogida** = `AVG(TransportInfo_TransportCarbonEmissions)`.
     - **kgCO₂e por tonelada transportada** = `SUM(TransportCarbonEmissions) / SUM(Weight) * 1000`.
   - Comparativa mes actual vs mes anterior (% variación).

4. **Capacidad vehicular y utilización** (gauge + tabla)
   - Fuente: `WasteMoveResidues` → `VehicleType`, `TransportInfo_vehicleRegistration`.
   - Métricas por vehículo/tipo: nº de viajes, peso total transportado, distancia acumulada.
   - Si se dispone de capacidad máxima (campo futuro o configuración): ratio de utilización.

5. **Cumplimiento de ventanas horarias DUM** (donut chart)
   - Fuente: cruzar `WasteMoves.PlannedPickupStart`/`PlannedPickupEnd` con `DUMRestrictionRules.ConditionsJson` (campo JSON con horarios permitidos).
   - Categorías: "Dentro de ventana DUM", "Fuera de ventana", "Sin zona DUM aplicable".

6. **Horarios de planta vs llegadas reales** (heatmap semanal)
   - Fuente: `EntryPlants.PlantEntryDate` agrupado por día de semana y hora.
   - Objetivo: identificar picos y valles para redistribuir llegadas.

7. **Incidencias logísticas abiertas** (tabla con semáforo)
   - Fuente: `Incidents` WHERE `ClosedAt IS NULL` AND `Type IN ('Retraso', 'AveriVehiculo', 'DescuadrePeso')`.
   - Columnas: referencia del traslado, tipo, severidad, fecha apertura, días abierta.

#### Filtros globales del dashboard:
- `Year`, `Month`/`Quarter`.
- `IdScrap` (solo ADMIN puede ver todos; SCRAP ve solo los suyos, filtrado por `WasteMoves.IdScrap` o `IdScrap2`).
- `AutonomousCommunity` / `ProvinceCode`.
- `WasteStream` (para aislar RAEE de otros flujos).
- `EntityRole` del punto de recogida (Producer, CAC, PublicEntity).

---

### Dashboard 2 — **Panel de Monitorización para Entidades Públicas** (`/logistics/public-monitoring`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Propósito**: los ayuntamientos adscritos a la oficina de asignación monitorizan los servicios prestados por los SCRAP (actuales e históricos) y consultan las facturas de compensación.

#### Widgets / KPIs requeridos:

1. **Servicios prestados por SCRAP** (tabla + filtros)
   - Fuente: `WasteMoves` JOIN `ServiceOrders` WHERE `ServiceOrders.IdIssuedBy` = entidad pública logueada.
   - Columnas: SCRAP (`Entities.Name` vía `WasteMoves.IdScrap`), nº traslados, kg totales (`SUM(EntryPlants.NetWeight)`), periodo, estado actual del traslado.

2. **Histórico de recogidas** (line chart mensual)
   - Fuente: `EntryPlants.PlantEntryDate` + `EntryPlantResidues.Weight`, agrupado por mes.
   - Líneas separadas por SCRAP (si hay más de uno asignado).

3. **Liquidaciones / Facturas de compensación** (tabla)
   - Fuente: `Settlements` WHERE `IdPublicEntity` = entidad logueada.
   - Columnas: `SettlementNumber`, `Year`, `Month`, `Status` (Pending/Approved/Rejected), `TotalAmount`, `Currency`, `ValidatedAt`.
   - Enlace al detalle de la liquidación existente.

4. **Emisiones evitadas / generadas** (card comparativa)
   - Fuente: `SUM(WasteMoveResidues.TransportInfo_TransportCarbonEmissions)` del periodo vs periodo anterior.

5. **Cumplimiento de objetivos municipales** (progress bar)
   - Fuente: `MarketShares` WHERE `IdScrap` IN (SCRAPs vinculados) AND `AutonomousCommunity` = la del ayuntamiento.
   - Real vs objetivo: `SUM(EntryPlants.NetWeight)` vs `MarketShares.Weight`.

#### Filtros:
- `Year`, `Month`.
- `IdScrap` (los SCRAPs que operan en su municipio).
- `WasteStream`.

---

### Dashboard 3 — **Panel Operativo de Gestores / CACs / Plantas** (`/logistics/operations`)

**Destinado a**: perfiles `DISPATCH_OFFICE`, `CAC_OP`, `PLANT_OP` y `ADMIN`.

**Propósito**: visión operativa en tiempo real para los actores que ejecutan la logística y el tratamiento.

#### Widgets / KPIs por perfil:

**Para `DISPATCH_OFFICE` (Gestor de residuos / Oficina de asignación):**
1. **Órdenes de servicio pendientes de planificar** → `ServiceOrders` WHERE `Status = 'Pending'`.
2. **Traslados en curso** → embudo de `WasteMoves` por `ServiceStatus`.
3. **Planificación semanal** → calendario con `ServiceOrders.PlannedPickupStart` de los próximos 7 días.
4. **Incidencias abiertas asignadas** → `Incidents` filtradas por su ámbito.

**Para `CAC_OP`:**
1. **Entradas en CAC hoy** → `EntryCACs` WHERE `CACEntryDate` = hoy.
2. **Stock acumulado por tipología** → `SUM(EntryCACResidues.Weight)` agrupado por `Residues.Name` / `LERCodes.Code`.
3. **Tickets de pesaje pendientes de envío** → entradas sin `TicketScale` o sin peso registrado.

**Para `PLANT_OP`:**
1. **Entradas en planta hoy** → `EntryPlants` WHERE `PlantEntryDate` = hoy.
2. **Balance de tratamiento** → `TreatmentPlantResidues`: `WeightReused` + `WeightValued` vs `WeightRemove` (tasa de reciclaje en tiempo real).
3. **Impropios detectados** → `SUM(TreatmentPlants.ImproperWeight)` del periodo.
4. **Incidencias de planta abiertas** → `Incidents` vinculadas a sus `TreatmentPlants`.

---

## 🗄️ Modelo de datos — Tablas y campos clave a consultar

| Tabla | Campos principales para el dashboard |
|-------|---------------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `Latitude`, `Longitude`, `ProvinceCode`, `MunicipalityCode`, `CenterCode` |
| `ServiceOrders` | `Id`, `ServiceOrderNumber`, `Status`, `Priority`, `IdIssuedBy`, `IdPickupPoint`, `WasteStream`, `IdLERCode`, `PlannedPickupStart`, `PlannedPickupEnd`, `OwnerId` |
| `WasteMoves` | `Id`, `WasteMoveReference`, `ServiceStatus`, `IdSource`, `IdDestination`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `IdResidue`, `IdCarrier`, `Weight`, `VehicleType`, `FuelType`, `EuroClass`, `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`, `TransportInfo_vehicleRegistration` |
| `EntryPlants` | `Id`, `IdWasteMove`, `TicketScale`, `PlantEntryDate`, `GrossWeight`, `TareWeight`, `NetWeight`, `ServiceOrderId`, `OwnerId` |
| `EntryPlantResidues` | `IdEntryPlant`, `IdResidue`, `Weight`, `MeasureUnit` |
| `EntryCACs` | `Id`, `IdWasteMove`, `CACEntryDate`, `CollectionMethod`, `OwnerId` |
| `EntryCACResidues` | `IdEntryCAC`, `IdResidue`, `Weight`, `MeasureUnit` |
| `TreatmentPlants` | `Id`, `IdTreatmentOperation`, `ImproperWeight`, `ServiceOrderId`, `IncidentId`, `OwnerId` |
| `TreatmentPlantResidues` | `IdTreatmentPlant`, `IdResidue`, `WeightTotal`, `WeightReused`, `WeightValued`, `WeightRemove` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `ServiceOrderId`, `WasteMoveReference`, `Description` |
| `DUMZones` | `Id`, `ZoneCode`, `GeometryJson`, `Status` |
| `DUMRestrictionRules` | `ZoneId`, `RuleCode`, `ConditionsJson`, `ActionType`, `ValidFrom`, `ValidTo` |
| `MarketShares` | `IdScrap`, `Category`, `AutonomousCommunity`, `Year`, `Weight`, `Period` |
| `Settlements` | `Id`, `SettlementNumber`, `Status`, `IdScrap`, `IdPublicEntity`, `Year`, `Month`, `TotalAmount`, `Currency` |
| `LERCodes` | `Id`, `Code`, `Description` |
| `Residues` | `Id`, `Name`, `Reference`, `ResidueType`, `IdLERCode` |
| `EmissionFactorSets` | `Id`, `Name`, `Version`, `IsActive` |

---

## 🔒 Reglas de autorización y filtrado de datos

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | Dashboard 1 | Solo ve traslados donde `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. |
| `PUBLIC_ENT` | Dashboard 2 | Solo ve traslados cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`) o liquidaciones donde `Settlements.IdPublicEntity = LinkedEntityId`. |
| `DISPATCH_OFFICE` | Dashboard 3 | Ve todos los traslados del tenant (`OwnerId`). |
| `CAC_OP` | Dashboard 3 | Solo ve entradas en su CAC (filtrado por `EntryCACs` vinculadas a su entidad). |
| `PLANT_OP` | Dashboard 3 | Solo ve entradas y tratamientos de su planta (filtrado por `EntryPlants`/`TreatmentPlants` de su entidad). |
| `ADMIN` | Todos (1, 2, 3) | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, entidad pública o planta. |
| `COORDINATOR` | Dashboard 1 (lectura) | Lectura transversal del ámbito de los acuerdos en los que participa. |

**Patrón de filtrado de datos**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` (ya implementado en la capa `Application`).

**Control de acceso a pantallas**: los permisos de acceso a cada dashboard se gestionan desde `/security/page-permissions` mediante el sistema `PageDefinitions`/`PagePermissions`. La tabla anterior documenta la configuración recomendada por defecto. Las policies en código (`CanViewLogisticsOptimization`, etc.) actúan como mínimo de seguridad estático.

---

## 🏗️ Arquitectura de implementación sugerida

### Capa Application (CQRS)
```
Application/Features/Logistics/
├── Queries/
│   ├── GetLogisticsOptimizationQuery.cs     → Dashboard 1 (SCRAP)
│   ├── GetPublicMonitoringQuery.cs          → Dashboard 2 (PUBLIC_ENT)
│   ├── GetOperationalDashboardQuery.cs      → Dashboard 3 (DISPATCH/CAC/PLANT)
│   └── GetDUMComplianceQuery.cs             → Widget de cumplimiento DUM (compartido)
└── DTOs/
    ├── LogisticsOptimizationDto.cs
    ├── PublicMonitoringDto.cs
    ├── OperationalDashboardDto.cs
    └── DUMComplianceDto.cs
```

### Capa Web (Blazor)
```
Web/Components/Pages/Logistics/
├── LogisticsOptimization.razor              → /logistics/optimization
├── PublicMonitoring.razor                   → /logistics/public-monitoring
└── OperationalDashboard.razor               → /logistics/operations
```

### Componentes reutilizables
- `WasteVolumeMap.razor` — mapa Leaflet/Mapbox con capas de entidades + DUM.
- `EmissionsCard.razor` — card de CO₂ con comparativa temporal.
- `DUMComplianceDonut.razor` — donut de cumplimiento de ventanas DUM.
- `IncidentsBadge.razor` — badge con conteo de incidencias abiertas por severidad.

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan ApexCharts (consistente con el módulo de KPIs existente).
4. El mapa interactivo renderiza polígonos DUM desde `GeometryJson` (formato GeoJSON).
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en cada dashboard (patrón ya implementado con ClosedXML en `ExportKpisToExcelQuery.cs`).
7. Responsive mobile-first (operadores de campo).
8. Modo oscuro/claro (consistente con `MainLayout.razor`).

---

## 🔗 Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de este módulo pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **Trazabilidad (§5.1)**: desde cualquier traslado en los dashboards, enlace directo a `/traceability?term={WasteMoveReference}`.
- **KPIs regulatorios (§5.2)**: el KPI de "intensidad CO₂ por tonelada" ya existe en `/kpis`; aquí se desglosa por ruta/zona.
- **Incidencias (§4.3)**: los widgets de incidencias enlazan a `/incidents/{id}`.
- **Zonas DUM (§4.4)**: la capa del mapa reutiliza la misma fuente de datos de `DUMZones`/`DUMRestrictionRules`.


---

## 🚗 Módulo de Movilidad Urbana — Impacto RAEE (UC3)

## 🎯 Objetivo — Caso de Uso 2 (UC2)

Crear un **nuevo módulo de dashboard** que evalúe el **impacto de los servicios de recogida RAEE sobre la movilidad urbana**. El objetivo es que los datos operativos de GreenTransit (rutas, horarios, volúmenes, zonas DUM) se crucen con indicadores de movilidad para que:

1. **Los Coordinadores (Clústeres Logísticos)** analicen las variables logísticas que afectan a la movilidad y propongan optimizaciones a los SCRAPs.
2. **Los Ayuntamientos** monitoricen el impacto que las recogidas RAEE tienen sobre la congestión, el transporte público y las operaciones de mantenimiento urbano de su municipio.
3. **La Oficina de Asignación y Coordinación de SCRAP RAEE** provea los datos necesarios para evaluar dicho impacto y coordine la planificación de recogidas para minimizar interferencias con la movilidad.

Este caso de uso se alinea con programas como **Living Lab**, que buscan soluciones de movilidad avanzada sostenible coordinando empresas tecnológicas, operadores logísticos y ayuntamientos.

---

## 📊 Dashboards a crear (son DOS vistas diferenciadas + una vista compartida)

### Dashboard UC3-A — **Panel de Análisis de Impacto en Movilidad — Coordinador** (`/mobility/coordinator-analysis`)

**Destinado a**: perfiles `COORDINATOR` y `ADMIN`.

**Policy de autorización**: `CanViewMobilityCoordinatorAnalysis` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: vista estratégica para que los Clústeres Logísticos analicen el impacto de las recogidas RAEE en la movilidad urbana y propongan optimizaciones.

#### Widgets / KPIs requeridos:

1. **Mapa de densidad de recogidas vs zonas sensibles de movilidad** (mapa interactivo)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `Entities` (punto de recogida con `Latitude`/`Longitude`).
   - Capa 1: heatmap de densidad de recogidas por geolocalización (agrupación por `Entities.MunicipalityCode` del `IdSource`).
   - Capa 2: polígonos de `DUMZones.GeometryJson` con semáforo de `DUMRestrictionRules.ActionType` (`Block` = rojo, `Restrict` = naranja, `Allow` = verde, `Notify` = azul).
   - Capa 3 (datos parametrizables): zonas de interés de movilidad (estaciones de transporte público, obras de mantenimiento, zonas peatonales) — inicialmente como capa estática configurable, preparada para integración futura con APIs municipales.
   - Al hacer clic en un cluster: popup con nº de recogidas del periodo, kg totales, horario predominante y % de recogidas dentro/fuera de ventana DUM.

2. **Distribución temporal de recogidas vs horas pico de movilidad** (heatmap semanal 7×24)
   - Fuente: `WasteMoves.ActualPickupStart` (si disponible) o `WasteMoves.PlannedPickupStart`, agrupado por día de semana y franja horaria.
   - Superposición visual: franjas de hora pico de tráfico urbano (configurable, por defecto 07:30–09:30 y 17:30–19:30) marcadas como bandas rojas en el heatmap.
   - KPI derivado: **% de recogidas en hora pico** = recogidas cuyo inicio cae en franja pico / total recogidas.

3. **Índice de conflicto logístico-movilidad por municipio** (tabla ranking + sparklines)
   - Fuente: cruce de `WasteMoves` + `DUMRestrictionRules` + datos temporales.
   - Métricas por municipio:
     - Nº total de recogidas en el periodo.
     - % de recogidas en hora pico.
     - % de recogidas fuera de ventana DUM.
     - Nº de incidencias logísticas (`Incidents` WHERE `Type IN ('Retraso', 'AveriaVehiculo')`) en el municipio.
   - **Índice de conflicto** = ponderación configurable de las métricas anteriores (0–100).
   - Ordenado por índice descendente. Semáforo: rojo > 70, naranja 40–70, verde < 40.

4. **Comparativa de eficiencia pre/post optimización** (bar chart comparativo)
   - Fuente: `WasteMoveResidues` → `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions`.
   - Permite seleccionar dos periodos (mes A vs mes B) y comparar:
     - km promedio por recogida
     - minutos promedio por recogida
     - kgCO₂e por tonelada transportada
     - % de recogidas en hora pico
   - Objetivo: evidenciar mejoras tras aplicar recomendaciones del Clúster.

5. **Recomendaciones automáticas** (panel con reglas de negocio)
   - Motor de reglas simple que genera sugerencias textuales basadas en umbrales:
     - Si % recogidas en hora pico > 30% → "Considerar redistribuir las recogidas del municipio X fuera de la franja 07:30–09:30".
     - Si % fuera de ventana DUM > 20% → "Revisar la planificación de rutas en la zona DUM Y".
     - Si índice de conflicto > 70 → "Municipio Z requiere intervención prioritaria de coordinación".
   - Las recomendaciones se generan en el backend (Query handler), no en el cliente.

#### Filtros globales:
- `Year`, `Month`/`Quarter`.
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `IdScrap` (el coordinador ve transversalmente los SCRAPs de sus acuerdos — filtrar por `Agreements` donde participa como `IdCoordinator`).
- `WasteStream` (fijo en `RAEE` por defecto, pero configurable).

---

### Dashboard UC3-B — **Panel de Monitorización de Movilidad — Ayuntamiento** (`/mobility/municipal-monitoring`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Policy de autorización**: `CanViewMobilityMunicipalMonitoring` (nueva).

**Propósito**: los ayuntamientos monitorizan cómo los servicios de recogida RAEE afectan a la movilidad de su municipio y verifican el cumplimiento de las ventanas horarias acordadas.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo de impacto** (cards de KPI)
   - **Recogidas totales del periodo** en el municipio: `COUNT(WasteMoves)` WHERE `Entities.MunicipalityCode` del punto de recogida = municipio del ayuntamiento.
   - **Kg RAEE recogidos**: `SUM(WasteMoveResidues.Weight)`.
   - **% de recogidas en horario compatible con movilidad**: recogidas FUERA de hora pico / total.
   - **% de cumplimiento DUM**: recogidas dentro de ventana DUM / total con zona DUM aplicable.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Calendario de recogidas planificadas** (vista semanal/mensual)
   - Fuente: `ServiceOrders` WHERE `Status IN ('Pending', 'Scheduled')` AND `IdPickupPoint` → `Entities.MunicipalityCode` = municipio del ayuntamiento.
   - Visualización tipo calendario con las recogidas planificadas, coloreadas por:
     - Verde: dentro de ventana DUM y fuera de hora pico.
     - Naranja: fuera de ventana DUM o en hora pico.
     - Rojo: fuera de ventana DUM Y en hora pico.
   - Permite al ayuntamiento anticipar posibles conflictos con operaciones municipales (obras, eventos, desvíos).

3. **Histórico de recogidas vs incidencias de movilidad** (line chart dual axis)
   - Eje izquierdo: nº de recogidas por semana/mes.
   - Eje derecho: nº de incidencias logísticas (`Incidents` del periodo en el municipio).
   - Líneas separadas por SCRAP (si hay más de uno operando).
   - Objetivo: correlacionar picos de actividad de recogida con incidencias.

4. **Detalle de cumplimiento por SCRAP** (tabla)
   - Fuente: `WasteMoves` JOIN `ServiceOrders` WHERE `ServiceOrders.IdIssuedBy` = entidad pública logueada o `IdPickupPoint` en su municipio.
   - Columnas por SCRAP: nº traslados, kg totales, % en hora pico, % cumplimiento DUM, nº incidencias, índice de conflicto.
   - Semáforo por fila según índice de conflicto.

5. **Notificaciones activas** (lista tipo inbox)
   - Alertas generadas cuando:
     - Se planifica una recogida en hora pico en su municipio.
     - Se supera el umbral de % fuera de ventana DUM (configurable).
     - Hay una incidencia abierta en su municipio (`Incidents.ClosedAt IS NULL`).
   - Fuente: generadas por el backend al crear/actualizar `ServiceOrders` y `Incidents`.

#### Filtros:
- `Year`, `Month`.
- `IdScrap` (los SCRAPs que operan en su municipio — derivado de `WasteMoves` históricas o `Agreements`).
- `WasteStream` (por defecto `RAEE`).

---

### Vista compartida UC3-C — **Datos de Impacto RAEE en Movilidad — Oficina de Asignación** (`/mobility/dispatch-data`)

**Destinado a**: perfiles `DISPATCH_OFFICE` y `ADMIN`.

**Policy de autorización**: `CanViewMobilityDispatchData` (nueva).

**Propósito**: la Oficina de Asignación y Coordinación de SCRAP RAEE provee los datos operativos necesarios para la evaluación del impacto en movilidad y ajusta la planificación.

#### Widgets / KPIs requeridos:

1. **Panel de datos exportables para análisis externo** (tabla con exportación XLSX)
   - Fuente: `WasteMoves` + `WasteMoveResidues` + `ServiceOrders` + `Entities` + `DUMZones`.
   - Dataset plano con campos: fecha recogida, municipio, provincia, SCRAP, tipo vehículo, peso, distancia, duración, emisiones CO₂, código zona DUM, cumplimiento DUM (sí/no), en hora pico (sí/no).
   - Exportable a XLSX (patrón ClosedXML existente).
   - Objetivo: proveer datos limpios y estructurados a los Coordinadores y ayuntamientos para análisis externos (Living Lab, estudios de movilidad).

2. **Resumen operativo por SCRAP** (cards agrupadas)
   - Para cada SCRAP bajo su coordinación:
     - Nº de recogidas del periodo.
     - % en hora pico.
     - % cumplimiento DUM.
     - Nº incidencias abiertas.
   - Con drill-down al detalle.

3. **Planificación semanal con indicadores de movilidad** (calendario + semáforo)
   - Reutiliza el patrón de planificación del Dashboard 3 operativo (`ServiceOrders` de los próximos 7 días).
   - Añade indicadores visuales de conflicto: las SO planificadas en hora pico o fuera de ventana DUM se marcan con semáforo.
   - Permite a la oficina reasignar horarios antes de confirmar la planificación.

4. **Evolución mensual de métricas de movilidad** (line chart multi-serie)
   - Series: % recogidas en hora pico, % cumplimiento DUM, índice de conflicto promedio.
   - Fuente: agregación mensual de las métricas ya calculadas.
   - Objetivo: ver tendencia a lo largo del año para informar a programas como Living Lab.

#### Filtros:
- `Year`, `Month`.
- `IdScrap`.
- `ProvinceCode` / `MunicipalityCode`.

---

## 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el UC3 se alimenta de las tablas existentes del modelo v4.1. Las nuevas métricas (% hora pico, índice de conflicto) se calculan en las Queries CQRS.

| Tabla | Campos principales para este UC3 |
|-------|----------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `Latitude`, `Longitude`, `ProvinceCode`, `MunicipalityCode` |
| `ServiceOrders` | `Id`, `Status`, `IdPickupPoint`, `IdIssuedBy`, `PlannedPickupStart`, `PlannedPickupEnd`, `WasteStream`, `OwnerId` |
| `WasteMoves` | `Id`, `IdSource`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `ServiceStatus`, `ActualPickupStart`, `ActualPickupEnd`, `PlannedPickupStart`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `Weight`, `VehicleType`, `TransportInfo_TransportDistance`, `TransportInfo_TransportDuration`, `TransportInfo_TransportCarbonEmissions` |
| `DUMZones` | `Id`, `ZoneCode`, `GeometryJson`, `Status` |
| `DUMRestrictionRules` | `ZoneId`, `ConditionsJson`, `ActionType`, `ValidFrom`, `ValidTo` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `WasteMoveReference` |
| `Agreements` | `Id`, `IdScrap`, `IdCoordinator`, `IdPublicEntity` |
| `EntryPlants` | `PlantEntryDate`, `NetWeight`, `ServiceOrderId` |

---

## 🔒 Reglas de autorización y filtrado de datos

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `COORDINATOR` | UC3-A | Ve transversalmente los SCRAPs vinculados a sus acuerdos (`Agreements.IdCoordinator = LinkedEntityId`). |
| `PUBLIC_ENT` | UC3-B | Solo ve recogidas cuyo punto de recogida (`IdPickupPoint` → `Entities.MunicipalityCode`) pertenece a su municipio, o cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`). |
| `DISPATCH_OFFICE` | UC3-C | Ve todos los traslados del tenant (`OwnerId`). |
| `ADMIN` | UC3-A, UC3-B, UC3-C | Sin restricciones dentro del tenant. Puede filtrar por cualquier coordinador, ayuntamiento o SCRAP. |

**Patrón de filtrado de datos**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` (ya implementado).

**Control de acceso a pantallas**: los permisos se gestionan dinámicamente desde `/security/page-permissions` mediante el sistema `PageDefinitions`/`PagePermissions`. La tabla anterior documenta la configuración recomendada por defecto. Las policies en código (`CanViewMobilityCoordinatorAnalysis`, etc.) actúan como mínimo de seguridad; el control fino se delega al sistema dinámico de BD.
---

## 🏗️ Arquitectura de implementación

### Capa Application (CQRS)

```
Application/Features/Mobility/
├── Queries/
│   ├── GetMobilityCoordinatorAnalysisQuery.cs   → UC2-A (COORDINATOR)
│   ├── GetMobilityMunicipalMonitoringQuery.cs    → UC2-B (PUBLIC_ENT)
│   ├── GetMobilityDispatchDataQuery.cs           → UC2-C (DISPATCH_OFFICE)
│   ├── GetMobilityConflictIndexQuery.cs          → Widget compartido: índice de conflicto
│   ├── GetPeakHourComplianceQuery.cs             → Widget compartido: % hora pico
│   └── ExportMobilityDataToExcelQuery.cs         → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── MobilityCoordinatorAnalysisDto.cs
│   ├── MobilityMunicipalMonitoringDto.cs
│   ├── MobilityDispatchDataDto.cs
│   ├── MobilityConflictIndexDto.cs
│   ├── PeakHourComplianceDto.cs
│   └── MobilityRecommendationDto.cs
└── Services/
    └── MobilityRecommendationEngine.cs           → Motor de reglas para recomendaciones
```

### Capa Web (Blazor)

```
Web/Components/Pages/Mobility/
├── CoordinatorAnalysis.razor          → /mobility/coordinator-analysis
├── MunicipalMonitoring.razor          → /mobility/municipal-monitoring
└── DispatchData.razor                 → /mobility/dispatch-data
```

### Componentes reutilizables (complementan los ya existentes)

- `MobilityHeatmap.razor` — heatmap 7×24 con bandas de hora pico configurables.
- `ConflictIndexTable.razor` — tabla con ranking de municipios por índice de conflicto + sparklines.
- `PickupCalendar.razor` — calendario de recogidas con semáforo de movilidad.
- `MobilityRecommendations.razor` — panel de recomendaciones automáticas.

> **Reutilizar** de los dashboards existentes: `WasteVolumeMap.razor`, `DUMComplianceDonut.razor`, `EmissionsCard.razor`, `IncidentsBadge.razor`.

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
4. El mapa interactivo reutiliza el componente de mapa ya implementado, añadiendo la capa de heatmap de densidad.
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en UC2-C (patrón ya implementado con ClosedXML).
7. Responsive mobile-first.
8. Modo oscuro/claro (consistente con `MainLayout.razor`).
9. Las **franjas de hora pico** son configurables (no hardcodeadas): se almacenan como parámetros del sistema o como constantes en `appsettings.json`.
10. El **índice de conflicto** usa pesos configurables para cada métrica (inicialmente: 40% hora pico + 30% fuera DUM + 20% incidencias + 10% volumen relativo).
11. Las recomendaciones se generan en el backend (`MobilityRecommendationEngine.cs`), no en el cliente.

---

## 🔗 Integración con módulos existentes

- **Dashboard 1 (Optimización Logística)**: el widget de cumplimiento DUM y el mapa interactivo comparten fuentes de datos. Los componentes `DUMComplianceDonut.razor` y `WasteVolumeMap.razor` se reutilizan.
- **Dashboard 2 (Monitorización Pública)**: el UC2-B extiende la visión del ayuntamiento añadiendo la perspectiva de movilidad. Enlace cruzado desde UC2-B al Dashboard 2 para ver liquidaciones y servicios prestados.
- **Dashboard 3 (Panel Operativo)**: el UC2-C complementa la planificación semanal añadiendo indicadores de conflicto de movilidad.
- **Trazabilidad (§5.1)**: desde cualquier recogida en los dashboards UC2, enlace directo a `/traceability?term={WasteMoveReference}`.
- **Incidencias (§4.3)**: los widgets de incidencias enlazan a `/incidents/{id}`.
- **Zonas DUM (§4.4)**: reutilización completa de la capa DUM del Dashboard 1.

---


---

## 🗺️ Módulo de Mapas de Calor — Densidad y Patrones de Residuos (UC Mapas de Calor)

## 🎯 Objetivo

Crear un **nuevo módulo de dashboards "Mapas de Calor"** dentro de la carpeta `Reporting` que permita visualizar **mapas de calor a partir de los datos operativos de reciclado** existentes en GreenTransit. El objetivo es identificar zonas de elevada densidad de residuos, patrones de generación, estacionalidad y tipología, facilitando la planificación urbana y la optimización de recursos para un entorno más sostenible.

Los datos visualizados incluyen:
- **Clasificación detallada del residuo** según composición (código LER, flujo de residuo), con cantidad exacta.
- **Datos georreferenciados** de los puntos de recogida en territorio español (latitud/longitud de las entidades).
- **Frecuencia de generación y recogida** basada en el volumen de solicitudes (órdenes de servicio) en cada punto de recogida.

**Participantes del ecosistema**:
- **Proveedores de datos**: los SCRAPs, que generan la operativa de recogida y tratamiento.
- **Consumidores de datos**: entes públicos (ayuntamientos, CCAA) que acceden a los mapas de calor para planificación urbana.

**Objetivos específicos**:
1. Facilitar la identificación de áreas con alta concentración de residuos por comunidad autónoma, provincia y municipio.
2. Prevenir la acumulación de residuos en zonas sensibles.
3. Analizar patrones temporales (estacionalidad) y de tipología de residuos.

---

## 📁 Ubicación de archivos

Todos los dashboards de este módulo deben crearse dentro de la carpeta `Reporting/HeatMaps/`:

### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/HeatMaps/
├── WasteDensityHeatMap.razor                → /reporting/heat-maps/waste-density
├── WastePatternAnalysis.razor               → /reporting/heat-maps/pattern-analysis
└── PublicEntityHeatMapView.razor            → /reporting/heat-maps/public-view
```

### Capa Application (CQRS)

```
Application/Features/Reporting/HeatMaps/
├── Queries/
│   ├── GetWasteDensityHeatMapQuery.cs       → Dashboard HM-A (SCRAP)
│   ├── GetWastePatternAnalysisQuery.cs      → Dashboard HM-B (SCRAP — análisis temporal)
│   ├── GetPublicEntityHeatMapQuery.cs       → Dashboard HM-C (PUBLIC_ENT)
│   ├── GetWasteFrequencyByPickupPointQuery.cs → Widget compartido: frecuencia de recogida
│   ├── GetSeasonalityAnalysisQuery.cs       → Widget compartido: estacionalidad
│   └── ExportHeatMapDataToExcelQuery.cs     → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── WasteDensityHeatMapDto.cs
│   ├── WastePatternAnalysisDto.cs
│   ├── PublicEntityHeatMapDto.cs
│   ├── WasteFrequencyByPickupPointDto.cs
│   ├── SeasonalityAnalysisDto.cs
│   └── HeatMapExportDto.cs
└── Services/
    └── HeatMapAggregationService.cs         → Servicio de agregación geoespacial
```

### Componentes reutilizables (complementan los ya existentes)

```
Web/Components/Shared/HeatMaps/
├── WasteHeatMapLayer.razor                  → Capa de heatmap de densidad sobre mapa Leaflet
├── SeasonalityChart.razor                   → Gráfico de estacionalidad (line/area chart)
├── FrequencyByPointTable.razor              → Tabla de frecuencia por punto de recogida
└── WasteTypologyDonut.razor                 → Donut de tipología de residuos (por código LER)
```

> **Reutilizar** de los dashboards existentes: `WasteVolumeMap.razor`, `DUMComplianceDonut.razor`, `EmissionsCard.razor`.

---

## 📊 Dashboards a crear (son TRES vistas diferenciadas)

### Dashboard HM-A — **Mapa de Calor de Densidad de Residuos — SCRAP** (`/reporting/heat-maps/waste-density`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Policy de autorización**: `CanViewHeatMapWasteDensity` (nueva, a registrar en `PolicyConstants.cs`).

**Propósito**: vista estratégica para que los SCRAPs visualicen la distribución geográfica de la densidad de residuos en los puntos de recogida bajo su ámbito, identificando zonas de alta concentración y patrones por tipología.

#### Widgets / KPIs requeridos:

1. **Mapa de calor de densidad de residuos** (mapa interactivo con capa heatmap)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `ServiceOrders` → coordenadas del punto de recogida vía `ServiceOrders.IdPickupPoint` → `Entities.Latitude` / `Entities.Longitude`.
   - Capa heatmap: intensidad basada en `SUM(WasteMoveResidues.Weight)` por punto de recogida georreferenciado.
   - Filtrable por `LERCodes.Code` (flujo de residuo) para aislar tipologías concretas.
   - Capa adicional: polígonos de `DUMZones.GeometryJson` con semáforo de `DUMRestrictionRules.ActionType` (`Block` = rojo, `Restrict` = naranja, `Allow` = verde, `Notify` = azul).
   - Al hacer clic en un cluster/punto: popup con nombre de la entidad, dirección, kg totales del periodo, nº de recogidas, tipología predominante (código LER más frecuente) y última recogida.

2. **Densidad de residuos por zona geográfica** (bar chart / treemap)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` JOIN `ServiceOrders` → agrupar por `Entities.ProvinceCode` o `Entities.MunicipalityCode` del `IdPickupPoint`.
   - Métricas: `SUM(WasteMoveResidues.Weight)` agrupado por zona y `LERCodes.Code`.
   - Drill-down: de Comunidad Autónoma → Provincia → Municipio.

3. **Tipología de residuos por zona** (donut chart + tabla)
   - Fuente: `WasteMoveResidues` JOIN `Residues` JOIN `LERCodes`.
   - Distribución porcentual por `LERCodes.Code` (capítulo/subcapítulo) del peso total recogido.
   - Indicar `LERCodes.IsDangerous` con icono de advertencia.

4. **Top 20 puntos de recogida por volumen** (tabla ranking)
   - Fuente: `ServiceOrders.IdPickupPoint` → `Entities`, `SUM(WasteMoveResidues.Weight)`.
   - Columnas: nombre entidad, municipio, provincia, kg totales, nº recogidas, kg promedio por recogida, tipología predominante.
   - Ordenado por kg totales descendente. Semáforo: rojo > percentil 90, naranja P75–P90, verde < P75.

5. **Frecuencia de recogida por punto** (card + sparklines)
   - Fuente: `COUNT(ServiceOrders)` agrupado por `IdPickupPoint` y periodo (semana/mes).
   - KPIs:
     - **Frecuencia promedio** = nº medio de recogidas por punto de recogida por mes.
     - **Puntos con frecuencia anómala** = puntos cuya frecuencia supera 2× la media.
   - Sparkline de evolución mensual.

6. **Comparativa de densidad entre periodos** (bar chart comparativo)
   - Permite seleccionar dos periodos (mes A vs mes B, o trimestre A vs trimestre B).
   - Compara por zona geográfica: kg totales, nº recogidas, kg/recogida promedio.

#### Filtros globales del dashboard:
- `Year`, `Month` / `Quarter`.
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `LERCodes.Code` (filtro por flujo/tipología de residuo).
- `WasteStream` (para aislar flujos concretos: RAEE, envases, etc.).
- `IdScrap`: **ADMIN** puede ver todos; **SCRAP** ve solo los suyos (filtrado por `WasteMoves.IdScrap` o `WasteMoves.IdScrap2`).

---

### Dashboard HM-B — **Análisis de Patrones y Estacionalidad — SCRAP** (`/reporting/heat-maps/pattern-analysis`)

**Destinado a**: perfiles `SCRAP` y `ADMIN`.

**Policy de autorización**: `CanViewHeatMapPatternAnalysis` (nueva).

**Propósito**: análisis temporal de los patrones de generación de residuos: estacionalidad, tendencias, picos y valles para optimizar la planificación de recogidas y anticipar acumulaciones.

#### Widgets / KPIs requeridos:

1. **Heatmap temporal de generación de residuos** (heatmap 12 meses × tipología)
   - Fuente: `WasteMoveResidues` JOIN `WasteMoves` → `SUM(Weight)` agrupado por mes y `LERCodes.Code` (capítulo).
   - Matriz de calor: eje X = meses del año, eje Y = tipología de residuo, intensidad = kg totales.
   - Permite identificar estacionalidad por tipo de residuo.

2. **Tendencia de volumen mensual** (line chart multi-serie)
   - Series separadas por `LERCodes.Code` (top 5 tipologías por volumen).
   - Línea de tendencia con media móvil de 3 meses.
   - Eje Y: kg totales. Eje X: meses.

3. **Heatmap semanal de frecuencia de recogidas** (heatmap 7×24)
   - Fuente: `ServiceOrders.PlannedPickupStart` o `WasteMoves.ActualPickupStart`, agrupado por día de semana y franja horaria.
   - Intensidad: nº de recogidas por celda.
   - Objetivo: identificar concentraciones horarias para redistribuir.

4. **Índice de concentración geográfica** (card + gauge)
   - Métrica calculada: coeficiente de Gini o índice de Herfindahl sobre la distribución de kg por punto de recogida.
   - Valor alto = residuos muy concentrados en pocos puntos → riesgo de acumulación.
   - Valor bajo = distribución más uniforme.
   - Comparativa con periodo anterior (% variación).

5. **Alertas de acumulación** (panel tipo inbox)
   - Motor de reglas simple que genera alertas basadas en umbrales (calculadas en backend):
     - Si un punto de recogida supera X kg sin recogida en Y días → "Punto [nombre] en [municipio] acumula [kg] sin recogida desde [fecha]".
     - Si un municipio supera el percentil 95 de densidad → "Municipio [nombre] presenta concentración anormalmente alta".
     - Si la frecuencia de recogida baja más de un 30% respecto al periodo anterior → "Frecuencia de recogida reducida en [zona]".
   - Las alertas se generan en el backend (`HeatMapAggregationService.cs`), no en el cliente.

#### Filtros globales:
- `Year`, `Month` / `Quarter`.
- `AutonomousCommunity` / `ProvinceCode` / `MunicipalityCode`.
- `LERCodes.Code` (flujo de residuo).
- `WasteStream`.

---

### Dashboard HM-C — **Vista de Mapas de Calor para Entidades Públicas** (`/reporting/heat-maps/public-view`)

**Destinado a**: perfiles `PUBLIC_ENT` y `ADMIN`.

**Policy de autorización**: `CanViewHeatMapPublicView` (nueva).

**Propósito**: los entes públicos (ayuntamientos, diputaciones) acceden a mapas de calor de su ámbito territorial para identificar zonas de alta densidad de residuos, analizar patrones de generación y planificar actuaciones preventivas.

#### Widgets / KPIs requeridos:

1. **Resumen ejecutivo territorial** (cards de KPI)
   - **Kg totales recogidos** en su ámbito territorial (municipio): `SUM(WasteMoveResidues.Weight)` WHERE `Entities.MunicipalityCode` del `IdPickupPoint` = municipio de la entidad pública logueada.
   - **Nº de puntos de recogida activos**: `COUNT(DISTINCT ServiceOrders.IdPickupPoint)` del periodo.
   - **Tipología predominante**: código LER con mayor peso acumulado.
   - **Frecuencia media de recogida**: recogidas/punto/mes.
   - **Variación % vs periodo anterior** en cada KPI.

2. **Mapa de calor territorial** (mapa interactivo)
   - Fuente: misma que HM-A pero filtrado por `Entities.MunicipalityCode` = municipio de la entidad pública logueada (o `ServiceOrders.IdIssuedBy = LinkedEntityId`).
   - Capa heatmap de densidad por kg en puntos de recogida de su municipio.
   - Al hacer clic: popup con detalle del punto.

3. **Distribución de residuos por tipología** (donut chart)
   - Distribución porcentual por `LERCodes.Code` del peso total recogido en su ámbito.

4. **Evolución temporal de recogidas** (line chart mensual)
   - Eje Y: kg totales. Eje X: meses.
   - Líneas separadas por SCRAP (si hay más de uno operando en el municipio).
   - Permite detectar estacionalidad a nivel municipal.

5. **Detalle de puntos de recogida** (tabla con exportación)
   - Columnas: nombre del punto, dirección, kg periodo, nº recogidas, última recogida, tipología predominante.
   - Exportable a XLSX.

6. **Indicadores de zonas sensibles** (tabla + semáforo)
   - Puntos de recogida cercanos a zonas sensibles (cruce con `DUMZones` si aplica, o configuración estática de zonas de interés: centros escolares, hospitales, zonas peatonales).
   - Semáforo: rojo si el punto supera umbral de acumulación en zona sensible.

#### Filtros:
- `Year`, `Month`.
- `LERCodes.Code` (flujo de residuo).
- `WasteStream`.
- `IdScrap` (los SCRAPs que operan en su municipio — derivado de `WasteMoves` históricas o `Agreements`).

---

## 🗄️ Modelo de datos — Tablas y campos clave

> **No se crean tablas nuevas**. Todo el módulo de Mapas de Calor se alimenta de las tablas existentes del modelo v4.1. Las métricas derivadas (frecuencia, índice de concentración, alertas) se calculan en las Queries CQRS y en `HeatMapAggregationService.cs`.

| Tabla | Campos principales para este módulo |
|-------|--------------------------------------|
| `Entities` | `Id`, `Name`, `EntityRole`, `Latitude`, `Longitude`, `ProvinceCode`, `MunicipalityCode`, `CenterCode` |
| `ServiceOrders` | `Id`, `Status`, `IdPickupPoint`, `IdIssuedBy`, `PlannedPickupStart`, `PlannedPickupEnd`, `WasteStream`, `IdLERCode`, `OwnerId` |
| `WasteMoves` | `Id`, `IdSource`, `IdScrap`, `IdScrap2`, `ServiceOrderId`, `ServiceStatus`, `ActualPickupStart`, `ActualPickupEnd`, `PlannedPickupStart`, `OwnerId` |
| `WasteMoveResidues` | `IdWasteMove`, `IdResidue`, `Weight`, `MeasureUnit`, `VehicleType`, `TransportInfo_TransportDistance`, `TransportInfo_TransportCarbonEmissions` |
| `Residues` | `Id`, `Name`, `Reference`, `ResidueType`, `IdLERCode`, `IdProducer` |
| `LERCodes` | `Id`, `Code`, `Description`, `IsDangerous`, `ChapterCode`, `SubChapterCode` |
| `DUMZones` | `Id`, `ZoneCode`, `GeometryJson`, `Status` |
| `DUMRestrictionRules` | `ZoneId`, `ConditionsJson`, `ActionType`, `ValidFrom`, `ValidTo` |
| `Agreements` | `Id`, `IdScrap`, `IdCoordinator`, `IdPublicEntity` |
| `EntryPlants` | `PlantEntryDate`, `NetWeight`, `ServiceOrderId`, `OwnerId` |
| `Incidents` | `Id`, `Type`, `Severity`, `OpenedAt`, `ClosedAt`, `WasteMoveReference` |

---

## 🔒 Reglas de autorización y filtrado de datos

> **IMPORTANTE**: El acceso a estos dashboards se gestiona mediante el **sistema de autorización por pantalla configurable desde la interfaz de administración** (`/security/page-permissions`) utilizando las tablas `PageDefinitions` y `PagePermissions`. **No se hardcodea el acceso en código.** Las policies en código actúan como mínimo de seguridad estático; el control fino se delega al sistema dinámico de BD.

| Perfil | Dashboard(s) accesible(s) | Filtrado de datos |
|--------|---------------------------|-------------------|
| `SCRAP` | HM-A, HM-B | Solo ve datos de recogidas donde `WasteMoves.IdScrap = LinkedEntityId` OR `WasteMoves.IdScrap2 = LinkedEntityId`. |
| `PUBLIC_ENT` | HM-C | Solo ve recogidas cuyo punto de recogida (`ServiceOrders.IdPickupPoint` → `Entities.MunicipalityCode`) pertenece a su municipio, o cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`). |
| `DISPATCH_OFFICE` | HM-A, HM-B, HM-C | Ve todos los datos del tenant (`OwnerId`). Visión completa equivalente a ADMIN dentro de su ámbito operativo. |
| `ADMIN` | HM-A, HM-B, HM-C | Sin restricciones dentro del tenant. Puede filtrar por cualquier SCRAP, entidad pública o municipio. |

**Patrón de filtrado de datos**: usar `ICurrentUserService.LinkedEntityId` + `ICurrentUserService.IsInAnyProfile(...)` (ya implementado).

**Control de acceso a pantallas**: los permisos se gestionan dinámicamente desde `/security/page-permissions` mediante el sistema `PageDefinitions`/`PagePermissions`. La tabla anterior documenta la configuración recomendada por defecto. Las policies en código (`CanViewHeatMapWasteDensity`, etc.) actúan como mínimo de seguridad estático.

---

## 🏗️ Arquitectura de implementación

### Capa Application (CQRS)

```
Application/Features/Reporting/HeatMaps/
├── Queries/
│   ├── GetWasteDensityHeatMapQuery.cs       → HM-A (SCRAP)
│   ├── GetWastePatternAnalysisQuery.cs      → HM-B (SCRAP)
│   ├── GetPublicEntityHeatMapQuery.cs       → HM-C (PUBLIC_ENT)
│   ├── GetWasteFrequencyByPickupPointQuery.cs → Widget compartido
│   ├── GetSeasonalityAnalysisQuery.cs       → Widget compartido
│   └── ExportHeatMapDataToExcelQuery.cs     → Exportación XLSX (patrón ClosedXML)
├── DTOs/
│   ├── WasteDensityHeatMapDto.cs
│   ├── WastePatternAnalysisDto.cs
│   ├── PublicEntityHeatMapDto.cs
│   ├── WasteFrequencyByPickupPointDto.cs
│   ├── SeasonalityAnalysisDto.cs
│   └── HeatMapExportDto.cs
└── Services/
    └── HeatMapAggregationService.cs         → Servicio de agregación geoespacial y alertas
```

### Capa Web (Blazor)

```
Web/Components/Pages/Reporting/HeatMaps/
├── WasteDensityHeatMap.razor                → /reporting/heat-maps/waste-density
├── WastePatternAnalysis.razor               → /reporting/heat-maps/pattern-analysis
└── PublicEntityHeatMapView.razor            → /reporting/heat-maps/public-view
```

### Componentes reutilizables (complementan los ya existentes)

- `WasteHeatMapLayer.razor` — capa de heatmap de densidad sobre mapa Leaflet/Mapbox.
- `SeasonalityChart.razor` — gráfico de estacionalidad (line/area chart).
- `FrequencyByPointTable.razor` — tabla de frecuencia por punto de recogida.
- `WasteTypologyDonut.razor` — donut de tipología de residuos por código LER.

> **Reutilizar** de los dashboards existentes: `WasteVolumeMap.razor`, `DUMComplianceDonut.razor`, `EmissionsCard.razor`.

---

## 📋 Criterios de aceptación

1. Cada dashboard respeta el filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
2. Los filtros globales persisten en la URL (query string) para permitir compartir enlaces.
3. Los gráficos usan **ApexCharts** (consistente con el módulo de KPIs existente).
4. El mapa interactivo reutiliza el componente de mapa ya implementado, añadiendo la capa de heatmap de densidad (`WasteHeatMapLayer.razor`).
5. Las consultas SQL usan índices existentes y no generan table scans sobre tablas operativas.
6. Exportación a XLSX disponible en HM-A y HM-C (patrón ya implementado con ClosedXML en `ExportKpisToExcelQuery.cs`).
7. Responsive mobile-first.
8. Modo oscuro/claro (consistente con `MainLayout.razor`).
9. Las alertas de acumulación se generan en el backend (`HeatMapAggregationService.cs`), no en el cliente.
10. Los umbrales de alertas son configurables (en `appsettings.json` o como parámetros del sistema).
11. **No se crean nuevas entidades de dominio**. Todo se implementa con las entidades del modelo v4.1 existente.
12. El acceso a cada dashboard **no está hardcodeado en código**. Se gestiona mediante el sistema de autorización por pantalla (`PageDefinitions`/`PagePermissions`) configurable desde `/security/page-permissions`.
13. Cada usuario solo ve datos de las entidades asignadas a él o creadas por él, a excepción de `ADMIN` y `DISPATCH_OFFICE` que ven todos los datos del tenant.

---

## 🔗 Integración con módulos existentes

- **Dashboard principal (§0.1)**: los widgets de densidad y frecuencia pueden integrarse como cards adicionales en la home, condicionados al perfil.
- **Dashboard Optimización Logística (UC2)**: el mapa de calor comparte fuentes de datos con el mapa interactivo del Dashboard 1. Reutilizar componentes: `WasteVolumeMap.razor`, `DUMComplianceDonut.razor`.
- **Dashboard Movilidad Urbana (UC3)**: los datos de densidad y frecuencia complementan el análisis de impacto en movilidad. Enlace cruzado desde HM-A al UC3-A para correlacionar densidad con conflicto de movilidad.
- **Trazabilidad (§5.1)**: desde cualquier punto de recogida en los mapas de calor, enlace directo a `/traceability?term={WasteMoveReference}`.
- **KPIs regulatorios (§5.2)**: los KPIs de volumen por zona complementan los indicadores regulatorios existentes en `/kpis`.


---

# Parte V — Matriz de Permisos y Autorización por Pantalla

> ⚠️ **IMPORTANTE**: Las matrices de esta sección documentan la **configuración recomendada por defecto**, no permisos hardcodeados en código. El acceso real a cada pantalla se gestiona **dinámicamente** desde la interfaz de administración (`/security/page-permissions`) mediante las tablas `PageDefinitions` y `PagePermissions`. El administrador puede ajustar los permisos de cada perfil sobre cada pantalla (Lectura / Escritura / Ambos / Sin acceso) sin necesidad de modificar código.
>
> Las policies de `[Authorize(Policy = ...)]` en el código Blazor actúan como **mínimo de seguridad estático** (suelo). Los permisos dinámicos de BD pueden restringir más, pero nunca ampliar el acceso más allá de lo que permite la policy de código.


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


### Matriz detallada por pantalla (del Mapa de Autorización)

## 4. Matriz de autorización por pantalla

### Leyenda de permisos

| Código | Significado |
|---|---|
| **CRUD** | Crear, Leer, Editar, Eliminar — acceso completo |
| **CRUD-P** | CRUD filtrado por datos propios (ver §3.2) |
| **C+R** | Crear y Leer (no editar ni eliminar) |
| **C+R-P** | Crear y Leer, filtrado por datos propios |
| **R** | Solo lectura (todos los datos del tenant) |
| **R-P** | Solo lectura, filtrado por datos propios |
| **V** | Validar (aprobar/rechazar, sin crear) |
| **—** | Sin acceso a la pantalla |

---

### 4.1. ENTIDADES (Maestros)

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Entidades** | `Entities` | R | R | C+R (su ámbito) | R | R | R | R | CRUD | CRUD |
| **LER** | `LERCodes` | R | R | R | R | R | R | R | R | CRUD |
| **Residuos** | `Residues` | CRUD-P (Product/ProductSpec) | R | R | R | R | R | R | CRUD | CRUD |
| **Operaciones R/D** | `TreatmentOperations` | R | R | R | R | R | R | R | R | CRUD |

**Justificación de creadores:**
- **Entidades**: `DISPATCH_OFFICE` y `ADMIN` son los creadores principales. `SCRAP` puede dar de alta entidades dentro de su ámbito (p.ej. productores adheridos).
- **LER**: Catálogo normativo inmutable. Solo `ADMIN` lo mantiene (cambios normativos muy esporádicos).
- **Residuos**: `PRODUCER` crea sus propios productos y fichas técnicas (`ResidueType = Product | ProductSpec`). `DISPATCH_OFFICE` y `ADMIN` gestionan los residuos operativos (`ResidueType = Waste`).
- **Operaciones R/D**: Catálogo normativo (Directiva 2008/98/CE). Solo `ADMIN`.

---

### 4.2. OPERACIONES

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Órdenes de Servicio** | `ServiceOrders` | CRUD-P | R | R | CRUD-P | R | R | R | CRUD | CRUD |
| **Traslados** | `WasteMoves` / `WasteMoveResidues` | R-P | U-P (asignados) | R | R | R | R | R | CRUD | CRUD |
| **Entradas Planta** | `EntryPlants` / `EntryPlantResidues` | — | — | R | R | — | CRUD-P | R | R | CRUD |
| **Entradas CAC** | `EntryCACs` / `EntryCACResidues` | — | — | R | R | CRUD-P | — | R | R | CRUD |
| **Tratamiento** | `TreatmentPlants` / `TreatmentPlantResidues` | — | — | R | R | — | CRUD-P | R | R | CRUD |

**Justificación de creadores:**
- **Órdenes de Servicio**: `PRODUCER` y `PUBLIC_ENT` crean SOs (solicitan recogida). `DISPATCH_OFFICE` también las crea (planificación centralizada). Cada uno ve solo las suyas.
- **Traslados**: **`DISPATCH_OFFICE` es el creador principal**. Agrupa SOs en movimientos logísticos reales, asigna transportista y planifica. `CARRIER` solo actualiza los traslados donde está asignado (confirma carga, registra tiempos reales). `PRODUCER` ve solo los traslados que se originan de sus SOs.
- **Entradas Planta**: Solo `PLANT_OP` las crea (pesaje en báscula de su planta).
- **Entradas CAC**: Solo `CAC_OP` las crea (registro de entrada en su centro de acopio).
- **Tratamiento**: Solo `PLANT_OP` (clasificación y balance de masas en su planta).

**Detalle del permiso `U-P` del Transportista en Traslados:**
- Puede actualizar: `ActualPickupStart/End`, `GatheredDate`, `DocumentId`, `DocumentHash`, `SignatureStatus`, datos de transporte en `WasteMoveResidues` (`NTNumber`, `DINumber`, `DIPhase`).
- NO puede modificar: origen, destino, SCRAP asignado, ni crear nuevos traslados.

---

### 4.3. SOSTENIBILIDAD

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Incidencias** | `Incidents` | C+R | C+R | C+R | C+R | C+R | C+R | C+R | CRUD | CRUD |
| **Zonas DUM** | `DUMZones` / `DUMRestrictionRules` | R | R | R | R | — | — | R | R | CRUD |
| **Simulador DUM** | *(lógica sobre `DUMZones`)* | R | R | R | R | — | — | R | R | CRUD |
| **Emisiones** | `WasteMoveResidues` (campos CO₂) | R-P | R-P | R | R | — | R | R | R | CRUD |
| **Energía Planta** | `PlantEnergies` | — | — | R | R | — | CRUD-P | R | R | CRUD |
| **Factores Emisión** | `EmissionFactorSets` / `EmissionFactors` | — | — | R | R | — | R | R | R | CRUD |

**Justificación de creadores:**
- **Incidencias**: Cualquier perfil puede abrir una incidencia (la apertura es universal). Solo `DISPATCH_OFFICE` y `ADMIN` pueden resolver/cerrar y eliminar. La resolución también la puede hacer el perfil responsable según el `Type` de incidencia.
- **Zonas DUM y Simulador**: Solo `ADMIN` crea/edita zonas y reglas. El resto las consulta (el simulador es de solo lectura: "¿puedo entrar con mi vehículo?").
- **Emisiones**: Dato calculado automáticamente por el backend al pasar a RECOGIDO. `ADMIN` puede forzar re-cálculo.
- **Energía Planta**: Solo `PLANT_OP` declara consumo eléctrico de su planta (Scope 2).
- **Factores Emisión**: Catálogo versionado. Solo `ADMIN` sube nuevas versiones.

---

### 4.4. REPORTING

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Trazabilidad** | *(vista cruzada)* | R-P | R-P | R | R | R-P | R-P | R | R | R |
| **Vista 360° Traslado** | *(vista cruzada)* | R-P | R-P | R | R | R-P | R-P | R | R | R |
| **KPIs** | *(vistas agregadas)* | — | — | R | R | — | R | R | R | R |
| **Documentos** | `AgreementDocuments` + campos Doc en `WasteMoves` | R-P | R-P | R | R | R-P | R-P | R | R | CRUD |
| **Dashboard Optimización Logística** | `WasteMoveResidues`, `DUMZones`, `Entities` | — | — | R | — | — | — | R | — | R |
| **Dashboard Monitorización Pública** | `WasteMoves`, `Settlements`, `MarketShares` | — | — | — | R | — | — | — | — | R |
| **Dashboard Panel Operativo** | `ServiceOrders`, `WasteMoves`, `EntryCACs`, `EntryPlants`, `Incidents` | — | — | — | — | R | R | — | R | R |
| **Dash. Mapa Calor Densidad** | `WasteMoveResidues`, `Entities`, `LERCodes` | — | — | R | — | — | — | — | R | R |
| **Dash. Mapa Calor Patrones** | `WasteMoveResidues`, `ServiceOrders`, `LERCodes` | — | — | R | — | — | — | — | R | R |
| **Dash. Mapa Calor Público** | `WasteMoveResidues`, `Entities`, `LERCodes` | — | — | — | R | — | — | — | R | R |
| **Dash. Huella Carbono Consolidada** | `WasteMoveResidues`, `PlantEnergies`, `Entities` | — | — | R | — | — | — | R | R | R |
| **Dash. Huella Carbono Transporte** | `WasteMoveResidues`, `EmissionFactors`, `Entities` | — | R | R | — | — | — | R | R | R |
| **Dash. Huella Carbono Plantas** | `PlantEnergies`, `EntryPlants`, `TreatmentPlants` | — | — | R | — | — | R | — | R | R |
| **Dash. Huella Carbono Productor** | `WasteMoveResidues`, `ServiceOrders`, `LERCodes` | R | — | — | — | — | — | — | — | R |
| **Dash. Huella Carbono Ent. Pública** | `WasteMoveResidues`, `Entities`, `ServiceOrders` | — | — | — | R | — | — | — | R | R |
| **Dash. Cumplimiento SCRAP** | `MarketShares`, `TreatmentPlantResidues`, `Agreements`, `Settlements` | — | — | R | — | — | — | — | — | R |
| **Dash. Auditoría Cuotas Mercado** | `MarketShares`, `EntryPlantResidues`, `Agreements` | — | — | — | — | — | — | R | R | R |
| **Dash. Monitorización Convenios** | `Agreements`, `Settlements`, `WasteMoves` | — | — | — | — | — | — | R | R | R |
| **Dash. Cumplimiento Ent. Pública** | `MarketShares`, `Settlements`, `Agreements`, `Incidents` | — | — | — | R | — | — | — | — | R |
| **Dash. Datos Cumplimiento Oficina** | `MarketShares`, `TreatmentPlantResidues`, `Agreements`, `Settlements` | — | — | — | — | — | — | — | R | R |

**Justificación:**
- **Trazabilidad y Vista 360°**: Todos los perfiles acceden pero ven solo los traslados en los que participan. `SCRAP`, `PUBLIC_ENT`, `COORDINATOR`, `DISPATCH_OFFICE` y `ADMIN` ven transversalmente.
- **KPIs**: Solo perfiles con responsabilidad de supervisión o cumplimiento normativo. No tiene sentido para `PRODUCER`, `CARRIER` o `CAC_OP` aislados.
- **Documentos**: `ADMIN` gestiona el repositorio documental. El resto consulta documentos de los traslados donde participa.
- **Dashboard Optimización Logística** (`/logistics/optimization`): Policy `CanViewLogisticsOptimization`. Orientado a SCRAP y COORDINATOR para optimizar rutas, visualizar zonas DUM y analizar eficiencia. `SCRAP` ve solo sus traslados (`IdScrap`/`IdScrap2`).
- **Dashboard Monitorización Pública** (`/logistics/public-monitoring`): Policy `CanViewPublicMonitoring`. Exclusivo para entidades públicas: servicios SCRAP, liquidaciones y objetivos municipales de sus acuerdos.
- **Dashboard Panel Operativo** (`/logistics/operations`): Policy `CanViewOperationalDashboard`. Multirrol (se adapta al perfil activo): SO pendientes y planificación semanal para DISPATCH_OFFICE; stock y tickets para CAC_OP; balance de tratamiento e impropios para PLANT_OP.
- **Dashboard Mapa Calor Densidad** (`/reporting/heat-maps/waste-density`): Policy `CanViewHeatMapWasteDensity`. Orientado a SCRAP para visualizar la distribución geográfica de densidad de residuos. `SCRAP` ve solo recogidas de sus traslados (`IdScrap`/`IdScrap2`). `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.
- **Dashboard Mapa Calor Patrones** (`/reporting/heat-maps/pattern-analysis`): Policy `CanViewHeatMapPatternAnalysis`. Análisis temporal de estacionalidad y patrones para SCRAP. Mismas reglas de filtrado que Mapa Calor Densidad.
- **Dashboard Mapa Calor Público** (`/reporting/heat-maps/public-view`): Policy `CanViewHeatMapPublicView`. Los entes públicos ven mapas de calor de su municipio (`Entities.MunicipalityCode` del punto de recogida = municipio de su entidad, o `ServiceOrders.IdIssuedBy = LinkedEntityId`). `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.
- **Dashboard Huella Carbono Visión Consolidada** (`/reporting/carbon-footprint/overview`): Policy `CanViewCarbonFootprintOverview`. Visión estratégica Scope 1 + Scope 2. `SCRAP` ve solo sus traslados (`IdScrap`/`IdScrap2`). `COORDINATOR` ve transversalmente vía `Agreements.IdCoordinator`. `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.
- **Dashboard Huella Carbono Emisiones Transporte** (`/reporting/carbon-footprint/transport-emissions`): Policy `CanViewTransportEmissionsAnalysis`. Análisis detallado Scope 1 con comparativas y recomendaciones. `CARRIER` ve solo traslados donde es transportista (`WasteMoveResidues.IdCarrier = LinkedEntityId`). Mismo filtrado SCRAP/COORDINATOR/DISPATCH_OFFICE que HC-A.
- **Dashboard Huella Carbono Plantas** (`/reporting/carbon-footprint/plant-energy`): Policy `CanViewPlantEnergyFootprint`. Scope 2 y eficiencia energética. `PLANT_OP` ve solo datos de su planta. `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.
- **Dashboard Huella Carbono Productor** (`/reporting/carbon-footprint/producer-report`): Policy `CanViewProducerCarbonReport`. `PRODUCER` ve solo traslados cuya SO fue emitida por su entidad (`ServiceOrders.IdIssuedBy = LinkedEntityId`).
- **Dashboard Huella Carbono Entidad Pública** (`/reporting/carbon-footprint/public-view`): Policy `CanViewPublicEntityCarbonView`. `PUBLIC_ENT` ve solo traslados cuyo punto de recogida pertenece a su municipio o cuya SO fue emitida por su entidad. `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.
- **Dashboard Cumplimiento SCRAP** (`/reporting/regulatory-compliance/scrap-overview`): Policy `CanViewScrapComplianceOverview`. Cada SCRAP ve su cumplimiento normativo propio: tasas, cuotas, convenios y alertas. `SCRAP` filtrado por `IdScrap = LinkedEntityId`.
- **Dashboard Auditoría Cuotas de Mercado** (`/reporting/regulatory-compliance/market-share-audit`): Policy `CanViewMarketShareAudit`. `COORDINATOR` audita el reparto proporcional entre SCRAPs vía `Agreements.IdCoordinator`. `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.
- **Dashboard Monitorización Convenios** (`/reporting/regulatory-compliance/agreement-monitoring`): Policy `CanViewAgreementComplianceMonitoring`. `COORDINATOR` monitoriza convenios de sus acuerdos. `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.
- **Dashboard Cumplimiento Entidad Pública** (`/reporting/regulatory-compliance/public-view`): Policy `CanViewPublicEntityComplianceView`. `PUBLIC_ENT` ve cumplimiento en su ámbito territorial: `Agreements.IdPublicEntity = LinkedEntityId`, `Settlements.IdPublicEntity = LinkedEntityId`.
- **Dashboard Datos Cumplimiento Oficina** (`/reporting/regulatory-compliance/dispatch-data`): Policy `CanViewDispatchOfficeComplianceData`. Visión consolidada del ecosistema completo para auditorías externas (AENOR Confía). `DISPATCH_OFFICE` y `ADMIN` ven todo el tenant.

---

### 4.5. SEGURIDAD

| Pantalla | Entidad BD | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH_OFFICE | ADMIN |
|---|---|---|---|---|---|---|---|---|---|---|
| **Usuarios** | `Users` | — | — | R-P (sus operadores) | — | — | — | — | — | CRUD |
| **Perfiles** | `Profiles` | — | — | R | — | — | — | — | — | CRUD |

**Justificación:**
- Solo `ADMIN` tiene CRUD completo en Usuarios y Perfiles.
- `SCRAP` puede ver los usuarios asociados a su ámbito (lectura restringida), porque necesita verificar quién opera bajo sus acuerdos.
- `DISPATCH_OFFICE` no gestiona usuarios — su rol es operativo, no de administración de seguridad.

---

## 5. Matriz compacta de referencia rápida

> Tabla resumen para validación rápida. Usar §4 para detalles y justificaciones.

| Pantalla | PRODUCER | CARRIER | SCRAP | PUBLIC_ENT | CAC_OP | PLANT_OP | COORDINATOR | DISPATCH | ADMIN |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Entidades | R | R | C+R | R | R | R | R | **CRUD** | CRUD |
| LER | R | R | R | R | R | R | R | R | **CRUD** |
| Residuos | CRUD-P | R | R | R | R | R | R | **CRUD** | CRUD |
| Operaciones R/D | R | R | R | R | R | R | R | R | **CRUD** |
| Órdenes Servicio | **CRUD-P** | R | R | **CRUD-P** | R | R | R | **CRUD** | CRUD |
| Traslados | R-P | U-P | R | R | R | R | R | **CRUD** | CRUD |
| Entradas Planta | — | — | R | R | — | **CRUD-P** | R | R | CRUD |
| Entradas CAC | — | — | R | R | **CRUD-P** | — | R | R | CRUD |
| Tratamiento | — | — | R | R | — | **CRUD-P** | R | R | CRUD |
| Incidencias | C+R | C+R | C+R | C+R | C+R | C+R | C+R | **CRUD** | CRUD |
| Zonas DUM | R | R | R | R | — | — | R | R | **CRUD** |
| Simulador DUM | R | R | R | R | — | — | R | R | **CRUD** |
| Emisiones | R-P | R-P | R | R | — | R | R | R | **CRUD** |
| Energía Planta | — | — | R | R | — | **CRUD-P** | R | R | CRUD |
| Factores Emisión | — | — | R | R | — | R | R | R | **CRUD** |
| Trazabilidad | R-P | R-P | R | R | R-P | R-P | R | R | R |
| Vista 360° | R-P | R-P | R | R | R-P | R-P | R | R | R |
| KPIs | — | — | R | R | — | R | R | R | R |
| Documentos | R-P | R-P | R | R | R-P | R-P | R | R | CRUD |
| **Dash. Optimización Logística** | — | — | **R** | — | — | — | **R** | — | R |
| **Dash. Monitorización Pública** | — | — | — | **R** | — | — | — | — | R |
| **Dash. Panel Operativo** | — | — | — | — | **R** | **R** | — | **R** | R |
| **Dash. Mapa Calor Densidad** | — | — | **R** | — | — | — | — | **R** | R |
| **Dash. Mapa Calor Patrones** | — | — | **R** | — | — | — | — | **R** | R |
| **Dash. Mapa Calor Público** | — | — | — | **R** | — | — | — | **R** | R |
| **Dash. Huella Carbono Consolidada** | — | — | **R** | — | — | — | **R** | **R** | R |
| **Dash. Huella Carbono Transporte** | — | **R** | **R** | — | — | — | **R** | **R** | R |
| **Dash. Huella Carbono Plantas** | — | — | **R** | — | — | **R** | — | **R** | R |
| **Dash. Huella Carbono Productor** | **R** | — | — | — | — | — | — | — | R |
| **Dash. Huella Carbono Ent. Pública** | — | — | — | **R** | — | — | — | **R** | R |
| **Dash. Cumplimiento SCRAP** | — | — | **R** | — | — | — | — | — | R |
| **Dash. Auditoría Cuotas Mercado** | — | — | — | — | — | — | **R** | **R** | R |
| **Dash. Monitorización Convenios** | — | — | — | — | — | — | **R** | **R** | R |
| **Dash. Cumplimiento Ent. Pública** | — | — | — | **R** | — | — | — | — | R |
| **Dash. Datos Cumplimiento Oficina** | — | — | — | — | — | — | — | **R** | R |
| Usuarios | — | — | R-P | — | — | — | — | — | **CRUD** |
| Perfiles | — | — | R | — | — | — | — | — | **CRUD** |

**Negrita** = perfil creador/acceso principal de esa pantalla.

---

## 6. Verificación de cobertura de creación

> Regla: toda pantalla debe tener al menos un perfil con capacidad de creación (C, CRUD, o CRUD-P).

| Pantalla | Perfiles creadores | ¿Cubierto? |
|---|---|---|
| Entidades | DISPATCH_OFFICE, ADMIN, SCRAP (restringido) | ✅ |
| LER | ADMIN | ✅ |
| Residuos | PRODUCER (Product/ProductSpec), DISPATCH_OFFICE (Waste), ADMIN | ✅ |
| Operaciones R/D | ADMIN | ✅ |
| Órdenes de Servicio | PRODUCER, PUBLIC_ENT, DISPATCH_OFFICE, ADMIN | ✅ |
| Traslados | DISPATCH_OFFICE, ADMIN | ✅ |
| Entradas Planta | PLANT_OP, ADMIN | ✅ |
| Entradas CAC | CAC_OP, ADMIN | ✅ |
| Tratamiento | PLANT_OP, ADMIN | ✅ |
| Incidencias | Todos (apertura), DISPATCH_OFFICE + ADMIN (resolución) | ✅ |
| Zonas DUM | ADMIN | ✅ |
| Emisiones | Automático (backend) + ADMIN (re-cálculo) | ✅ |
| Energía Planta | PLANT_OP, ADMIN | ✅ |
| Factores Emisión | ADMIN | ✅ |
| Documentos | ADMIN | ✅ |
| Usuarios | ADMIN | ✅ |
| Perfiles | ADMIN | ✅ |

---

## Actualización de la Matriz de Permisos (§8)

La siguiente fila se añade a la tabla de la sección 8:

| Funcionalidad | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| ProductDeclaration / Products | CRUD | R (adheridos) | CRUD (suyos) | – | – | – | – | R |
| dicProductDeclaration* (diccionarios) | CRUD | – | – | – | – | – | – | – |

### Actualización de la Matriz de Permisos (§8) — Módulo de Análisis y Cumplimiento Normativo

Las siguientes filas se añaden a la tabla de la sección 8:

| Funcionalidad | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| Dash. Cumplimiento SCRAP (CN-A) | R | R | – | – | – | – | – | – |
| Dash. Auditoría Cuotas Mercado (CN-B) | R | – | – | – | – | – | – | R |
| Dash. Monitorización Convenios (CN-C) | R | – | – | – | – | – | – | R |
| Dash. Cumplimiento Ent. Pública (CN-D) | R | – | – | – | – | – | R | – |
| Dash. Datos Cumplimiento Oficina (CN-E) | R | – | – | – | – | – | – | – |

> **Nota**: `DISPATCH_OFFICE` tiene acceso a CN-B, CN-C y CN-E (con visión completa del tenant). No aparece en esta tabla resumen (§8) pero sí en la matriz detallada (§4.4).

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


---

# Parte VI — Patrón de Autorización en Páginas Blazor

> **Sistema dinámico de permisos**: GreenTransit implementa un sistema de autorización por pantalla **configurable desde la interfaz de administración**, no hardcodeado en código. Los permisos de acceso a cada página se gestionan mediante las tablas `PageDefinitions` y `PagePermissions` en BD, y se administran desde la pantalla `/security/page-permissions`.

---

## 1. Arquitectura del sistema de permisos por pantalla

### 1.1. Tablas en BD

| Tabla | Contenido |
|---|---|
| `PageDefinitions` | Catálogo de todas las páginas del sistema: ruta, nombre legible, módulo, componente. **Se sincroniza automáticamente** en cada arranque. |
| `PagePermissions` | Asignación de permisos por perfil y pantalla: **Lectura**, **Escritura** o **Ambos**. Configurable por el administrador desde la UI. |

### 1.2. Servicios implicados

| Servicio | Responsabilidad |
|---|---|
| `IPageDiscoveryService` | Ejecutado por `DbInitializer` en cada arranque. Escanea por reflexión todos los componentes Blazor con `[RouteAttribute]` y sincroniza la tabla `PageDefinitions`. **No es necesario escribir INSERTs manuales** — cualquier página nueva se registra automáticamente. |
| `IPagePermissionService` | Consulta `PagePermissions` para determinar si un perfil tiene acceso a una ruta. Expone `CanAccessRouteAsync()`. Los permisos se cachean en `IMemoryCache` (5 min). |
| `RouteAccessGuard` | Protege el acceso directo por URL. Aunque un enlace no aparezca en el menú, si un usuario escribe la URL manualmente, el guard verifica los permisos en BD antes de permitir el acceso. |

### 1.3. Triple capa de seguridad

```
Capa 1: ProfileAuthorizeView    → Filtro estático por perfil (código)
        ↓
Capa 2: PagePermissionService   → Filtro dinámico desde BD (configurable por admin)
        ↓
Capa 3: RouteAccessGuard        → Protección de acceso directo por URL
```

- **`NavMenu.razor`** consulta `IPagePermissionService.CanAccessRouteAsync` para cada enlace antes de renderizarlo. Solo aparecen en el menú las rutas que el perfil del usuario tiene asignadas en `PagePermissions`.
- Las policies de `[Authorize(Policy = ...)]` en el código Blazor actúan como **mínimo de seguridad estático**. Los permisos dinámicos en BD pueden **restringir** el acceso adicional, pero nunca ampliarlo más allá de lo que permite la policy de código.

### 1.4. Interfaz de administración (`/security/page-permissions`)

- **Acceso**: solo perfil `ADMIN`.
- **Funcionalidad**: muestra todas las pantallas descubiertas (agrupadas por módulo), y para cada una permite asignar a cada perfil del sistema uno de estos niveles:
  - **Lectura** — el perfil puede ver la pantalla pero no crear/editar/eliminar.
  - **Escritura** — el perfil puede realizar operaciones de escritura.
  - **Ambos** — lectura + escritura.
  - **Sin acceso** — la pantalla no aparece en su menú ni es accesible por URL.
- **Pantallas nuevas sin configurar**: se destacan en **amarillo**, indicando que el administrador debe asignar permisos. Hasta entonces, la pantalla no es accesible para ningún perfil salvo ADMIN.

### 1.5. Auto-descubrimiento de pantallas (`PageDiscoveryService`)

El servicio `PageDiscoveryService.InferModuleName()` clasifica cada página en un módulo automáticamente:

| Namespace / Ruta | Módulo asignado |
|---|---|
| `Security` · `/users` · `/profiles` · `/security` | Seguridad |
| `Reporting` · `/traceability` · `/kpis` · `/documents` · `/reporting/heat-maps/` · `/reporting/carbon-footprint/` · `/reporting/regulatory-compliance/` | Reporting |
| `Logistics` · `/logistics/` | Dashboards Logísticos |
| `Sustainability` · `/incidents` · `/dum-zones` · `/emissions` · `/plant-energies` | Sostenibilidad |
| `/entities` · `/ler-codes` · `/residues` · `/treatment-operations` | Configuración |
| `/service-orders` · `/waste-moves` · `/entry-*` · `/treatment-plants` | Operaciones |
| `/agreements` · `/settlements` · `/market-shares` | Contratos y Liquidaciones |
| `/product-declarations` | Declaraciones de Producto |
| `Mobility` · `/mobility/` | Movilidad Urbana |
| `EcoDataNet` · `/ecodatanet/` | EcoDataNet |

Si una ruta nueva no encaja en ningún módulo conocido, actualizar `InferModuleName()` en `Infrastructure/Services/PageDiscoveryService.cs`.

El método `PageDiscoveryService.HumanizeName()` convierte nombres de componentes en nombres legibles en español. Si el nombre no es autoexplicativo, se puede actualizar el diccionario o renombrarlo manualmente desde `/security/page-permissions`.

Nombres legibles recomendados para las pantallas de Mapas de Calor:

| Componente | Nombre legible |
|---|---|
| `WasteDensityHeatMap` | `Mapa de Calor — Densidad de Residuos` |
| `WastePatternAnalysis` | `Mapa de Calor — Patrones y Estacionalidad` |
| `PublicEntityHeatMapView` | `Mapa de Calor — Vista Entidad Pública` |

Nombres legibles recomendados para las pantallas de Huella de Carbono:

| Componente | Nombre legible |
|---|---|
| `CarbonFootprintOverview` | `Huella de Carbono — Visión Consolidada` |
| `TransportEmissionsAnalysis` | `Huella de Carbono — Emisiones del Transporte` |
| `PlantEnergyFootprint` | `Huella de Carbono — Huella Energética Plantas` |
| `ProducerCarbonReport` | `Huella de Carbono — Reporte Productor` |
| `PublicEntityCarbonView` | `Huella de Carbono — Vista Entidad Pública` |

Nombres legibles recomendados para las pantallas de Análisis y Cumplimiento Normativo:

| Componente | Nombre legible |
|---|---|
| `ScrapComplianceOverview` | `Cumplimiento Normativo — Visión SCRAP` |
| `MarketShareAudit` | `Cumplimiento Normativo — Auditoría Cuotas de Mercado` |
| `AgreementComplianceMonitoring` | `Cumplimiento Normativo — Monitorización Convenios` |
| `PublicEntityComplianceView` | `Cumplimiento Normativo — Vista Entidad Pública` |
| `DispatchOfficeComplianceData` | `Cumplimiento Normativo — Datos Oficina` |

---

## 2. Patrón de implementación en páginas Blazor

### A) Página de acceso completo para todos los autenticados

```razor
@attribute [Authorize]
```

Usar cuando: la página es visible para cualquier perfil pero el contenido varía según el perfil (los botones de acción se controlan dinámicamente desde `PagePermissions`).
Ejemplos: `/incidents`, `/traceability`, `/waste-moves`, `/service-orders`.

### B) Página restringida por policy estática (mínimo de seguridad)

```razor
@attribute [Authorize(Policy = PolicyConstants.CanManageUsers)]
```

Usar cuando: la página requiere un mínimo de seguridad a nivel de código que no debe poder relajarse desde la UI de admin. La policy actúa como **suelo**; los permisos dinámicos de `PagePermissions` pueden restringir más, pero nunca dar acceso a un perfil que la policy excluye.
Ejemplos: `/users`, `/profiles`.

### C) Página con permisos mixtos

```razor
@attribute [Authorize]

@* Los botones se muestran/ocultan según PagePermissions (nivel Escritura) *@
```

Usar cuando: lectura amplia + escritura restringida. El nivel de escritura se configura por perfil desde `/security/page-permissions`.

---

## 3. Patrón en Query Handlers MediatR

```csharp
public async Task<List<ServiceOrderDto>> Handle(GetServiceOrdersQuery request, CancellationToken ct)
{
    var query = _db.ServiceOrders
        .Where(so => so.OwnerId == _currentUser.OwnerId)  // 1. Filtro multi-tenant
        .AsQueryable();

    query = _dataScope.ApplyScope(query);                  // 2. Filtro por perfil (datos propios)

    if (!string.IsNullOrEmpty(request.SearchTerm))
        query = query.Where(so => so.ServiceOrderNumber.Contains(request.SearchTerm));

    return await query
        .OrderByDescending(so => so.CreatedAt)
        .Select(so => new ServiceOrderDto { ... })
        .ToListAsync(ct);
}
```

> **Nota**: el filtrado de datos propios (`IDataScopeService.ApplyScope`) es independiente del sistema de `PagePermissions`. `PagePermissions` controla **si el usuario ve la pantalla**; `DataScope` controla **qué registros ve dentro de ella**.

---

## 4. Checklist al crear una nueva página

- [ ] `@page "/mi-nueva-ruta"` definida
- [ ] `@attribute [Authorize...]` con policy adecuada (mínimo de seguridad)
- [ ] Si policy nueva → añadida en `PolicyConstants.cs` + `Program.cs`
- [ ] Namespace coherente con el módulo (ej: `Pages/Security/`, `Pages/Reporting/`)
- [ ] Si ruta/namespace no mapea a módulo existente → actualizar `InferModuleName()`
- [ ] Si nombre del componente no es descriptivo → actualizar `HumanizeName()`
- [ ] Entrada añadida en `NavMenu.razor` en la sección correcta (con consulta a `IPagePermissionService`)
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions`

---

## 5. Configuración recomendada por defecto

Las matrices de permisos de la Parte V documentan la **configuración recomendada por defecto** que el administrador debe aplicar desde `/security/page-permissions` tras el despliegue inicial. Estas matrices no son hardcodeadas en código — son la referencia para la configuración inicial del sistema.


# Parte VII — Checklist Técnico

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


### Checklist adicional — Módulo de Declaraciones

## Actualización del Checklist Técnico (§9)

Se añaden los siguientes ítems:

- [ ] Seed de diccionarios: `dicProductDeclarationCategory`, `dicProductDeclarationPeriods`, `dicProductDeclarationProducts`, `dicProductDeclarationSource`, `dicProductDeclarationType`, `dicProductDeclarationUse`.
- [ ] Seed de `DocStates` con estados: `Borrador`, `Emitido`, `Validado`, `Rechazado`.
- [ ] Servicio de dominio `ProductDeclarationStateService` para transiciones de estado.
- [ ] Notificaciones al cambiar estado (EMITIDO → notifica a ADMIN; VALIDADO/RECHAZADO → notifica a PRODUCER).
- [ ] Template de importación CSV/XLSX disponible para descarga.


### Checklist adicional — Módulo de Mapas de Calor

Se añaden los siguientes ítems:

- [x] Policies registradas en `PolicyConstants.cs`: `CanViewHeatMapWasteDensity`, `CanViewHeatMapPatternAnalysis`, `CanViewHeatMapPublicView`.
- [x] Policies registradas en `Program.cs` con los perfiles indicados.
- [x] Páginas Blazor creadas en `Web/Components/Pages/Reporting/HeatMaps/` con `@attribute [Authorize(Policy = ...)]`.
- [x] Queries CQRS creadas en `Application/Features/Reporting/HeatMaps/Queries/`.
- [x] DTOs creados en `Application/Features/Reporting/HeatMaps/DTOs/`.
- [ ] `HeatMapAggregationService.cs` implementado con motor de alertas de acumulación. *(pendiente: servicio de dominio no creado)*
- [ ] Componentes reutilizables creados en `Web/Components/Shared/HeatMaps/`. *(pendiente)*
- [x] Entradas en `NavMenu.razor` en sección Reporting con consulta a `IPagePermissionService`.
- [x] `InferModuleName()` actualizado para reconocer `/reporting/heat-maps/` → Reporting.
- [x] `HumanizeName()` actualizado con nombres legibles en español para los 3 componentes.
- [x] Filtrado multi-tenant (`OwnerId`) aplicado en todos los Query handlers.
- [x] Filtrado por perfil (`LinkedEntityId`) aplicado: SCRAP (IdScrap/IdScrap2), PUBLIC_ENT (MunicipalityCode/IdIssuedBy).
- [x] Umbrales de alertas configurables en `appsettings.json` (sección `HeatMaps.Alerts`).
- [x] Exportación XLSX implementada en HM-A y HM-C (patrón ClosedXML). *(`ExportHeatMapDataToExcelQuery` — ClosedXML)*
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions`.


### Checklist adicional — Módulo de Huella de Carbono

Se añaden los siguientes ítems:

- [x] Policies registradas en `PolicyConstants.cs`: `CanViewCarbonFootprintOverview`, `CanViewTransportEmissionsAnalysis`, `CanViewPlantEnergyFootprint`, `CanViewProducerCarbonReport`, `CanViewPublicEntityCarbonView`.
- [x] Policies registradas en `Program.cs` con los perfiles indicados en §5.7.
- [x] Páginas Blazor creadas en `Web/Components/Pages/Reporting/CarbonFootprint/` con `@attribute [Authorize(Policy = ...)]`.
- [x] Queries CQRS creadas en `Application/Features/Reporting/CarbonFootprint/Queries/`.
- [x] DTOs creados en `Application/Features/Reporting/CarbonFootprint/DTOs/`.
- [ ] `CarbonFootprintCalculationService.cs` implementado con cálculo Scope 2, motor de recomendaciones y umbrales. *(pendiente: servicio de dominio)*
- [ ] Componentes reutilizables creados en `Web/Components/Shared/CarbonFootprint/`. *(pendiente: mejora opcional)*
- [x] Entradas en `NavMenu.razor` en sección Reporting como subcarpeta colapsable "Huella de Carbono" con consulta a `IPagePermissionService`.
- [x] `InferModuleName()` actualizado para reconocer `/reporting/carbon-footprint/` → "Huella de Carbono".
- [x] `HumanizeName()` actualizado con nombres legibles en español para los 5 componentes.
- [x] Configuración `CarbonFootprint` añadida en `appsettings.json` (Scope2 factor, umbrales, referencia Objetivo 55).
- [x] Filtrado multi-tenant (`OwnerId`) aplicado en todos los Query handlers.
- [x] Filtrado por perfil (`LinkedEntityId`) aplicado: SCRAP (IdScrap/IdScrap2), PRODUCER (IdIssuedBy), CARRIER (IdCarrier), PLANT_OP (PlantCenterCode), PUBLIC_ENT (MunicipalityCode/IdIssuedBy), COORDINATOR (Agreements.IdCoordinator).
- [ ] Todos los JOINs geográficos resuelven `ProvinceCode` → `Province.Name` y `MunicipalityCode` → `Municipality.Name`. *(pendiente: mejora de calidad de datos)*
- [x] Exportación XLSX implementada en HC-A y HC-C (patrón ClosedXML). *(`ExportCarbonFootprintToExcelQuery`)*
- [x] Gráficos con **Radzen Blazor Charts** (`AppChart`/`ChartPalette`), responsive, modo oscuro/claro. *(Migrado desde ApexCharts — ver `docs/chart-migration-inventory.md`)*
- [ ] Filtros persistidos en query string.
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions`.


### Checklist adicional — Módulo de Análisis y Cumplimiento Normativo

Se añaden los siguientes ítems:

- [x] Policies registradas en `PolicyConstants.cs`: `CanViewScrapComplianceOverview`, `CanViewMarketShareAudit`, `CanViewAgreementComplianceMonitoring`, `CanViewPublicEntityComplianceView`, `CanViewDispatchOfficeComplianceData`.
- [x] Policies registradas en `Program.cs` con los perfiles indicados en §5.8.
- [x] Páginas Blazor creadas en `Web/Components/Pages/Reporting/RegulatoryCompliance/` con `@attribute [Authorize(Policy = ...)]`.
- [x] Queries CQRS creadas en `Application/Features/Reporting/RegulatoryCompliance/Queries/`.
- [x] DTOs creados en `Application/Features/Reporting/RegulatoryCompliance/DTOs/`.
- [ ] `ComplianceMonitoringService.cs` implementado con motor de alertas, cálculos de desviación y umbrales configurables. *(pendiente: servicio de dominio)*
- [ ] Componentes reutilizables creados en `Web/Components/Shared/RegulatoryCompliance/`. *(pendiente: mejora opcional)*
- [x] Entradas en `NavMenu.razor` en sección Reporting como subcarpeta colapsable "Análisis y Cumplimiento Normativo" con consulta a `IPagePermissionService`.
- [x] `InferModuleName()` actualizado para reconocer `/reporting/regulatory-compliance/` → "Análisis Cumplimiento".
- [x] `HumanizeName()` actualizado con nombres legibles en español para los 5 componentes.
- [x] Configuración `RegulatoryCompliance` añadida en `appsettings.json` (umbrales de alertas, objetivos por defecto — ver sección `RegulatoryTargets`).
- [x] Filtrado multi-tenant (`OwnerId`) aplicado en todos los Query handlers.
- [x] Filtrado por perfil (`LinkedEntityId`) aplicado: SCRAP (IdScrap/IdScrap2), PUBLIC_ENT (MunicipalityCode/IdIssuedBy/IdPublicEntity), COORDINATOR (Agreements.IdCoordinator).
- [ ] Todos los JOINs geográficos resuelven `ProvinceCode` → `Province.Name`, `MunicipalityCode` → `Municipality.Name` y `AutonomousCommunity` como nombre (no código). *(pendiente: mejora de calidad de datos)*
- [x] Exportación XLSX implementada en CN-B, CN-D y CN-E (patrón ClosedXML). *(`ExportComplianceDataToExcelQuery` con DashboardType `MarketShareAudit`, `PublicEntityCompliance`, `DispatchOffice`)*
- [x] Gráficos con **Radzen Blazor Charts** en TODOS los dashboards (mínimo uno por dashboard), responsive, modo oscuro/claro. *(Migrado desde ApexCharts — ver `docs/chart-migration-inventory.md`)*
- [x] Filtros persistidos en query string.
- [x] Todos los dashboards incluyen al menos un gráfico (no solo tablas y cards).
- [ ] Tras despliegue: configurar permisos por perfil desde `/security/page-permissions`.


---

*Documento unificado generado a partir de: Mapa_Funcionalidades_GreenTransit.md, Mapa_Autorizacion_GreenTransit.md, Modelo_de_Datos.md, PATRON_AUTORIZACION_PAGINAS.md, Dashboard_UC2_Optimizacion_RAEE.md, Dashboard_UC3_Movilidad_Urbana.md, Dashboard_Mapas_de_Calor.md, Dashboard_Huella_de_Carbono.md, Dashboard_Analisis_Cumplimiento_Normativo.md.*
