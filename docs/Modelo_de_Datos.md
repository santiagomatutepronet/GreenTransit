# Modelo de Datos – GreenTransitDB

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

#### `PageDefinitions`

Catálogo de **páginas registradas** del sistema. Se sincroniza automáticamente al arrancar la aplicación mediante `IPageDiscoveryService.SyncPageDefinitionsAsync`, que descubre las rutas declaradas con `@page` en los componentes Blazor.

| Campo | Tipo | Descripción |
|---|---|---|
| `ID` | `int IDENTITY` | PK |
| `Route` | `nvarchar(256)` | Ruta Razor, p.ej. `/service-orders` (índice único) |
| `PageName` | `nvarchar(256)` | Nombre legible de la página |
| `ModuleName` | `nvarchar(128)` | Agrupación funcional (Operaciones, Seguridad…) |
| `ComponentName` | `nvarchar(256)?` | Nombre del componente Blazor |
| `IsActive` | `bit` | Solo las páginas activas participan en PagePermissions |
| `SortOrder` | `int` | Orden de presentación en la matriz de permisos |
| `CreatedAt` | `datetime` | Auditoría |
| `UpdatedAt` | `datetime?` | Auditoría |

> **Regla**: las rutas NO registradas en `PageDefinitions` no están sujetas a control dinámico de permisos; en ese caso la protección recae exclusivamente en el atributo `[Authorize]` de la propia página.

---

#### `PagePermissions`

Tabla de **concesión de acceso** por perfil y página. Implementa el control dinámico configurable por el administrador.

| Campo | Tipo | Descripción |
|---|---|---|
| `ID` | `int IDENTITY` | PK |
| `IdPageDefinition` | `int` | FK → `PageDefinitions.ID` |
| `IdProfile` | `int` | FK → `Profiles.ID` |
| `AccessLevel` | `nvarchar(16)` | `Read` \| `Write` \| `ReadWrite` |
| `CreatedAt` | `datetime` | Auditoría |
| `UpdatedAt` | `datetime?` | Auditoría |
| `IdUser` | `int?` | Usuario que concedió el permiso |

**Lógica de evaluación** (triple capa):

| Capa | Dónde | Qué evalúa |
|---|---|---|
| 1 — Perfil estático | `ProfileAuthorizeView` en `NavMenu.razor` | Tipo de perfil del usuario (PRODUCER, ADMIN…) |
| 2 — Permiso dinámico | `IPagePermissionService.CanAccessRouteAsync` | Entrada en `PagePermissions` para el perfil + ruta |
| 3 — Guarda de ruta | `RouteAccessGuard.razor` | Re-evalúa ambas capas en acceso directo por URL |

> **Regla**: si un perfil tiene al menos una entrada en `PagePermissions`, se aplica la lista blanca estricta (solo ve lo que tiene concedido). Si el perfil **no tiene ninguna entrada** pero la ruta SÍ está en `PageDefinitions`, el acceso también se **deniega** (lista blanca estricta: sin concesión explícita = sin acceso). Solo si la ruta NO está en `PageDefinitions` el acceso se delega a la capa 1 (`[Authorize]`). Esto garantiza que perfiles con 0 permisos configurados no vean ninguna pantalla gestionada.

> **Caché**: `PagePermissionService` cachea el conjunto de IDs de páginas permitidas por `ProfileId` durante **5 minutos** en `IMemoryCache`. Se invalida explícitamente al guardar cambios en la matriz de permisos. El menú lateral precarga todos los permisos del usuario en `NavMenu.OnInitializedAsync` en un único ciclo.

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