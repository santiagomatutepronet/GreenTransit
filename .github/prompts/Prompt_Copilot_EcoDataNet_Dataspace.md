# 🤖 PROMPT FINAL — Implementación del Módulo EcoDataNet Dataspace (Pantallas por Perfil)

> **Prompt para IA generadora de código (GitHub Copilot / Codegen)**
>
> **Stack**: .NET 10+ · Clean Architecture (Domain / Application / Infrastructure / Web) · Blazor Web App (Server interactivo) · Radzen Blazor Components · Entity Framework Core · SQL Server Azure · MediatR · FluentValidation · Serilog
>
> **Instrucciones de uso**: Adjunta siempre como contexto los ficheros `COPILOT_CONTEXT.md`, `README.md`, `Mapa_Funcionalidades.md` y `Crear_BD_v4_1.sql`. Ejecuta cada fase en orden. Antes de cada paso, asegúrate de que el paso anterior compila (`dotnet build`).

---

## 📋 CONTEXTO DEL MÓDULO

GreenTransit se integra con el espacio de datos **EcoDataNet** mediante conectores **EDC** (Eclipse Dataspace Components). Este módulo añade un sistema de pantallas por perfil funcional bajo el menú principal **"EcoDataNet"** del sidebar.

Ya existe un epígrafe colapsable **EcoDataNet** en `NavMenu.razor` (icono `bi-broadcast`) con un ítem hijo "Publicar Datos" que apunta a `/ecodatanet/publish`. Las nuevas pantallas se añaden **debajo** de este grupo existente, como sub-grupos por perfil.

Cada uno de los **8 perfiles funcionales** (participantes del ecosistema) tendrá **3 pantallas**:
- **Configuración** — formulario de configuración del conector EDC
- **Publicar datos** — catálogo informativo de datasets que publica el perfil
- **Consumir datos** — catálogo de datasets que consume, agrupados por perfil proveedor

**IMPORTANTE**: En esta fase NO se implementa lógica de negocio real, ni persistencia, ni llamadas a APIs EDC. Solo estructura UI con botones stub.

---

## 🗂️ SECCIÓN 1 — ESTRUCTURA DE NAVEGACIÓN Y RUTAS

### 1.1. Ficheros a localizar

Antes de implementar, localiza y examina estos ficheros para replicar patrones exactos:

```
src/GreenTransit.Web/Components/Layout/NavMenu.razor          ← Menú lateral con RadzenPanelMenu
src/GreenTransit.Web/Components/Layout/NavMenu.razor.cs        ← Code-behind (si existe)
src/GreenTransit.Infrastructure/Services/PageDiscoveryService.cs ← InferModuleName() + HumanizeName()
src/GreenTransit.Infrastructure/Authorization/PolicyConstants.cs ← Constantes de policies
src/GreenTransit.Web/Program.cs                                ← Registro de policies
```

### 1.2. Menú de navegación — Estructura jerárquica

Dentro del grupo colapsable **EcoDataNet** existente en `NavMenu.razor`, añadir **8 sub-grupos** (uno por perfil), cada uno con 3 ítems hijos. Usar `RadzenPanelMenuItem` siguiendo exactamente el patrón del NavMenu existente.

**Estructura del menú** (dentro del `RadzenPanelMenuItem` padre "EcoDataNet"):

```
EcoDataNet (ya existe, icono "hub" o "bi-broadcast")
  ├─ Publicar Datos (ya existe → /ecodatanet/publish)
  ├─ Oficina Asignación (sub-grupo colapsable)
  │   ├─ Configuración    → /ecodatanet/dispatch-office/config
  │   ├─ Publicar datos   → /ecodatanet/dispatch-office/publish
  │   └─ Consumir datos   → /ecodatanet/dispatch-office/consume
  ├─ SCRAP
  │   ├─ Configuración    → /ecodatanet/scrap/config
  │   ├─ Publicar datos   → /ecodatanet/scrap/publish
  │   └─ Consumir datos   → /ecodatanet/scrap/consume
  ├─ Ayuntamiento
  │   ├─ Configuración    → /ecodatanet/public-entity/config
  │   ├─ Publicar datos   → /ecodatanet/public-entity/publish
  │   └─ Consumir datos   → /ecodatanet/public-entity/consume
  ├─ Transportista
  │   ├─ Configuración    → /ecodatanet/carrier/config
  │   ├─ Publicar datos   → /ecodatanet/carrier/publish
  │   └─ Consumir datos   → /ecodatanet/carrier/consume
  ├─ CAC
  │   ├─ Configuración    → /ecodatanet/cac/config
  │   ├─ Publicar datos   → /ecodatanet/cac/publish
  │   └─ Consumir datos   → /ecodatanet/cac/consume
  ├─ Planta tratamiento
  │   ├─ Configuración    → /ecodatanet/plant/config
  │   ├─ Publicar datos   → /ecodatanet/plant/publish
  │   └─ Consumir datos   → /ecodatanet/plant/consume
  ├─ Productor
  │   ├─ Configuración    → /ecodatanet/producer/config
  │   ├─ Publicar datos   → /ecodatanet/producer/publish
  │   └─ Consumir datos   → /ecodatanet/producer/consume
  └─ Coordinador
      ├─ Configuración    → /ecodatanet/coordinator/config
      ├─ Publicar datos   → /ecodatanet/coordinator/publish
      └─ Consumir datos   → /ecodatanet/coordinator/consume
```

### 1.3. Patrón de NavMenu a replicar

Cada sub-grupo de perfil sigue este patrón Razor (extraído del NavMenu existente):

```razor
@* ── Sub-grupo: Oficina Asignación ── *@
<RadzenPanelMenuItem Text="Oficina Asignación" Icon="business_center"
                     Visible="@HasAnyVisibleChild("EdnDispatchOffice")">
    <RadzenPanelMenuItem Text="Configuración"
                         Icon="settings"
                         Path="/ecodatanet/dispatch-office/config"
                         Visible="@_permissions.GetValueOrDefault("/ecodatanet/dispatch-office/config")" />
    <RadzenPanelMenuItem Text="Publicar datos"
                         Icon="cloud_upload"
                         Path="/ecodatanet/dispatch-office/publish"
                         Visible="@_permissions.GetValueOrDefault("/ecodatanet/dispatch-office/publish")" />
    <RadzenPanelMenuItem Text="Consumir datos"
                         Icon="cloud_download"
                         Path="/ecodatanet/dispatch-office/consume"
                         Visible="@_permissions.GetValueOrDefault("/ecodatanet/dispatch-office/consume")" />
</RadzenPanelMenuItem>
```

**Iconos sugeridos por perfil** (Material Icons):

| Perfil | Icono | Slug de ruta |
|--------|-------|-------------|
| Oficina Asignación | `business_center` | `dispatch-office` |
| SCRAP | `recycling` | `scrap` |
| Ayuntamiento | `account_balance` | `public-entity` |
| Transportista | `local_shipping` | `carrier` |
| CAC | `warehouse` | `cac` |
| Planta tratamiento | `factory` | `plant` |
| Productor | `precision_manufacturing` | `producer` |
| Coordinador | `groups` | `coordinator` |

### 1.4. Registro en `_groupRoutes` del NavMenu

El NavMenu existente mantiene un diccionario `_groupRoutes` que asocia cada grupo con sus rutas hijas para determinar visibilidad. Añadir las 24 rutas nuevas:

```csharp
// En el bloque de inicialización de _groupRoutes (OnInitializedAsync o constructor)
_groupRoutes["EdnDispatchOffice"] = new[] {
    "/ecodatanet/dispatch-office/config",
    "/ecodatanet/dispatch-office/publish",
    "/ecodatanet/dispatch-office/consume"
};
_groupRoutes["EdnScrap"] = new[] {
    "/ecodatanet/scrap/config",
    "/ecodatanet/scrap/publish",
    "/ecodatanet/scrap/consume"
};
_groupRoutes["EdnPublicEntity"] = new[] {
    "/ecodatanet/public-entity/config",
    "/ecodatanet/public-entity/publish",
    "/ecodatanet/public-entity/consume"
};
_groupRoutes["EdnCarrier"] = new[] {
    "/ecodatanet/carrier/config",
    "/ecodatanet/carrier/publish",
    "/ecodatanet/carrier/consume"
};
_groupRoutes["EdnCac"] = new[] {
    "/ecodatanet/cac/config",
    "/ecodatanet/cac/publish",
    "/ecodatanet/cac/consume"
};
_groupRoutes["EdnPlant"] = new[] {
    "/ecodatanet/plant/config",
    "/ecodatanet/plant/publish",
    "/ecodatanet/plant/consume"
};
_groupRoutes["EdnProducer"] = new[] {
    "/ecodatanet/producer/config",
    "/ecodatanet/producer/publish",
    "/ecodatanet/producer/consume"
};
_groupRoutes["EdnCoordinator"] = new[] {
    "/ecodatanet/coordinator/config",
    "/ecodatanet/coordinator/publish",
    "/ecodatanet/coordinator/consume"
};
```

---

## 🔐 SECCIÓN 2 — AUTORIZACIÓN E INTEGRACIÓN CON PageDefinitions

### 2.1. Auto-descubrimiento (NO se necesitan INSERTs manuales)

`PageDiscoveryService` escanea automáticamente todos los componentes Blazor con `[RouteAttribute]` al arrancar. Al crear las 24 páginas nuevas con `@page "/ecodatanet/..."`, se registrarán automáticamente en `PageDefinitions`.

### 2.2. Actualizar `InferModuleName()` en `PageDiscoveryService.cs`

La regla existente ya cubre `EcoDataNet` · `/ecodatanet/`:

```
| `EcoDataNet` · `/ecodatanet/` | EcoDataNet |
```

Verificar que en `InferModuleName()` existe esta regla. Si no:

```csharp
var r when r.StartsWith("/ecodatanet/") => "EcoDataNet",
```

### 2.3. Actualizar `HumanizeName()` en `PageDiscoveryService.cs`

Añadir al diccionario de nombres legibles:

```csharp
// EcoDataNet Dataspace — Configuración
{ "EdcConfigDispatchOffice",    "EDN — Configuración Oficina Asignación" },
{ "EdcConfigScrap",             "EDN — Configuración SCRAP" },
{ "EdcConfigPublicEntity",      "EDN — Configuración Ayuntamiento" },
{ "EdcConfigCarrier",           "EDN — Configuración Transportista" },
{ "EdcConfigCac",               "EDN — Configuración CAC" },
{ "EdcConfigPlant",             "EDN — Configuración Planta Tratamiento" },
{ "EdcConfigProducer",          "EDN — Configuración Productor" },
{ "EdcConfigCoordinator",       "EDN — Configuración Coordinador" },
// EcoDataNet Dataspace — Publicar datos
{ "EdcPublishDispatchOffice",   "EDN — Publicar Datos Oficina Asignación" },
{ "EdcPublishScrap",            "EDN — Publicar Datos SCRAP" },
{ "EdcPublishPublicEntity",     "EDN — Publicar Datos Ayuntamiento" },
{ "EdcPublishCarrier",          "EDN — Publicar Datos Transportista" },
{ "EdcPublishCac",              "EDN — Publicar Datos CAC" },
{ "EdcPublishPlant",            "EDN — Publicar Datos Planta Tratamiento" },
{ "EdcPublishProducer",         "EDN — Publicar Datos Productor" },
{ "EdcPublishCoordinator",      "EDN — Publicar Datos Coordinador" },
// EcoDataNet Dataspace — Consumir datos
{ "EdcConsumeDispatchOffice",   "EDN — Consumir Datos Oficina Asignación" },
{ "EdcConsumeScrap",            "EDN — Consumir Datos SCRAP" },
{ "EdcConsumePublicEntity",     "EDN — Consumir Datos Ayuntamiento" },
{ "EdcConsumeCarrier",          "EDN — Consumir Datos Transportista" },
{ "EdcConsumeCac",              "EDN — Consumir Datos CAC" },
{ "EdcConsumePlant",            "EDN — Consumir Datos Planta Tratamiento" },
{ "EdcConsumeProducer",         "EDN — Consumir Datos Productor" },
{ "EdcConsumeCoordinator",      "EDN — Consumir Datos Coordinador" },
```

### 2.4. Policy de autorización

Crear una sola policy genérica para todas las pantallas EcoDataNet Dataspace. Todos los perfiles autenticados pueden acceder (el control fino se hace desde `/security/page-permissions`):

```csharp
// PolicyConstants.cs
public const string CanAccessEcoDataNet = nameof(CanAccessEcoDataNet);
```

```csharp
// Program.cs — en la sección de AddAuthorization
options.AddPolicy(PolicyConstants.CanAccessEcoDataNet, policy =>
    policy.RequireAuthenticatedUser());
```

Cada página Blazor usará:

```razor
@attribute [Authorize(Policy = PolicyConstants.CanAccessEcoDataNet)]
```

### 2.5. Matriz de permisos recomendada (para documentación)

El admin configurará desde `/security/page-permissions`. Configuración recomendada por defecto:

| Pantalla | DISPATCH_OFFICE | SCRAP | PUBLIC_ENT | CARRIER | CAC_OP | PLANT_OP | PRODUCER | COORDINATOR | ADMIN |
|---|---|---|---|---|---|---|---|---|---|
| `/ecodatanet/{perfil}/config` | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Todos |
| `/ecodatanet/{perfil}/publish` | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Todos |
| `/ecodatanet/{perfil}/consume` | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Solo su perfil | Todos |

> **Nota**: "Solo su perfil" significa que el admin debe habilitar al perfil DISPATCH_OFFICE únicamente las 3 rutas de `/ecodatanet/dispatch-office/*`, al perfil SCRAP las de `/ecodatanet/scrap/*`, etc. El ADMIN ve todas.

---

## 📁 SECCIÓN 3 — ESTRUCTURA DE FICHEROS

### 3.1. Ubicación de los componentes Blazor

Crear las páginas en la estructura de carpetas del proyecto, bajo el namespace `EcoDataNet/Dataspace`:

```
src/GreenTransit.Web/Components/Pages/EcoDataNet/Dataspace/
  ├─ DispatchOffice/
  │   ├─ EdcConfigDispatchOffice.razor
  │   ├─ EdcPublishDispatchOffice.razor
  │   └─ EdcConsumeDispatchOffice.razor
  ├─ Scrap/
  │   ├─ EdcConfigScrap.razor
  │   ├─ EdcPublishScrap.razor
  │   └─ EdcConsumeScrap.razor
  ├─ PublicEntity/
  │   ├─ EdcConfigPublicEntity.razor
  │   ├─ EdcPublishPublicEntity.razor
  │   └─ EdcConsumePublicEntity.razor
  ├─ Carrier/
  │   ├─ EdcConfigCarrier.razor
  │   ├─ EdcPublishCarrier.razor
  │   └─ EdcConsumeCarrier.razor
  ├─ Cac/
  │   ├─ EdcConfigCac.razor
  │   ├─ EdcPublishCac.razor
  │   └─ EdcConsumeCac.razor
  ├─ Plant/
  │   ├─ EdcConfigPlant.razor
  │   ├─ EdcPublishPlant.razor
  │   └─ EdcConsumePlant.razor
  ├─ Producer/
  │   ├─ EdcConfigProducer.razor
  │   ├─ EdcPublishProducer.razor
  │   └─ EdcConsumeProducer.razor
  └─ Coordinator/
      ├─ EdcConfigCoordinator.razor
      ├─ EdcPublishCoordinator.razor
      └─ EdcConsumeCoordinator.razor
```

### 3.2. Componentes reutilizables (RECOMENDADO)

Para evitar duplicación masiva de código, crear **3 componentes base reutilizables** que reciban por parámetro el `profileId` y rendericen los datos del perfil correspondiente:

```
src/GreenTransit.Web/Components/Pages/EcoDataNet/Dataspace/Shared/
  ├─ EdcConfigBase.razor          ← Formulario de configuración del conector
  ├─ EdcPublishDataBase.razor     ← Tabla de datasets publicados
  ├─ EdcConsumeDataBase.razor     ← Secciones de datasets consumidos
  └─ EcoDataNetDatasetStore.cs    ← Clase estática / singleton con el JSON de datasets
```

Cada página por perfil será simplemente un wrapper que pasa el `profileId`:

```razor
@* EdcConfigDispatchOffice.razor *@
@page "/ecodatanet/dispatch-office/config"
@attribute [Authorize(Policy = PolicyConstants.CanAccessEcoDataNet)]

<EdcConfigBase ProfileId="dispatch-office" ProfileLabel="Oficina de Asignación" />
```

---

## 📝 SECCIÓN 4 — PANTALLA 1: CONFIGURACIÓN (EdcConfigBase)

### 4.1. Campos del formulario

Usar componentes **Radzen** (`RadzenTextBox`, `RadzenSwitch`, `RadzenTextArea`) dentro de un `RadzenCard` con `RadzenTemplateForm`. Layout con `RadzenRow` / `RadzenColumn`.

| Campo | Componente | Validación | Requerido |
|-------|-----------|-----------|-----------|
| `connectorName` | `RadzenTextBox` | — | No |
| `connectorDns` | `RadzenTextBox` | — | No |
| `managementUrl` | `RadzenTextBox` | Formato URL (`Uri.IsWellFormedUriString`) | **Sí** |
| `defaultUrl` | `RadzenTextBox` | Formato URL | No |
| `protocolUrl` | `RadzenTextBox` | Formato URL | **Sí** |
| `federatedCatalogEnabled` | `RadzenSwitch` | — | No |
| `federatedCatalogUrl` | `RadzenTextBox` | Formato URL (visible solo si `federatedCatalogEnabled = true`) | **Sí** (si habilitado) |
| `apiToken` | `RadzenTextBox` (tipo password) | No vacío | **Sí** |
| `did` | `RadzenTextBox` | — | No |
| `keycloakTokenUrl` | `RadzenTextBox` | Formato URL | No |
| `keycloakJwksUrl` | `RadzenTextBox` | Formato URL | No |
| `keycloakAudience` | `RadzenTextBox` | — | No |
| `publicApiBaseUrl` | `RadzenTextBox` | Formato URL | No |
| `notes` | `RadzenTextArea` (3 filas) | — | No |

### 4.2. Modelo del formulario (ViewModel local, sin persistencia)

```csharp
// Dentro del componente o como clase en Shared/
public class EdcConnectorConfigModel
{
    public string? ConnectorName { get; set; }
    public string? ConnectorDns { get; set; }
    [Required(ErrorMessage = "La URL de Management es obligatoria")]
    [Url(ErrorMessage = "Formato de URL no válido")]
    public string ManagementUrl { get; set; } = string.Empty;
    public string? DefaultUrl { get; set; }
    [Required(ErrorMessage = "La URL de Protocol es obligatoria")]
    [Url(ErrorMessage = "Formato de URL no válido")]
    public string ProtocolUrl { get; set; } = string.Empty;
    public bool FederatedCatalogEnabled { get; set; }
    public string? FederatedCatalogUrl { get; set; }
    [Required(ErrorMessage = "El API Token es obligatorio")]
    public string ApiToken { get; set; } = string.Empty;
    public string? Did { get; set; }
    public string? KeycloakTokenUrl { get; set; }
    public string? KeycloakJwksUrl { get; set; }
    public string? KeycloakAudience { get; set; }
    public string? PublicApiBaseUrl { get; set; }
    public string? Notes { get; set; }
}
```

### 4.3. Comportamiento de botones

| Botón | Estilo | Acción |
|-------|--------|--------|
| **Guardar** | `rz-primary` | Stub: muestra `RadzenNotification` con mensaje "Configuración guardada (simulado — sin persistencia)". NO guarda en BD. |
| **Probar conexión** | `rz-secondary` | Stub: espera 1 segundo (`Task.Delay(1000)`) y muestra notificación "Conexión simulada correctamente". |
| **Cancelar** | `rz-light` | `NavigationManager.NavigateTo("/ecodatanet/publish")` o navegar atrás. |

### 4.4. Layout del formulario

```
┌─────────────────────────────────────────────────────────────┐
│  RadzenCard                                                  │
│  Título: "Configuración del Conector EDC — {ProfileLabel}"  │
│                                                              │
│  ┌─ Sección: Datos del Conector ─────────────────────────┐  │
│  │  connectorName          │  connectorDns               │  │
│  │  managementUrl          │  defaultUrl                  │  │
│  │  protocolUrl            │  did                         │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─ Sección: Catálogo Federado ──────────────────────────┐  │
│  │  [switch] federatedCatalogEnabled                      │  │
│  │  federatedCatalogUrl (condicional)                     │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─ Sección: Seguridad ─────────────────────────────────┐  │
│  │  apiToken (password)    │  [btn] Probar conexión       │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─ Sección: Keycloak (opcional) ────────────────────────┐  │
│  │  keycloakTokenUrl       │  keycloakJwksUrl             │  │
│  │  keycloakAudience       │  publicApiBaseUrl            │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─ Observaciones ──────────────────────────────────────┐  │
│  │  notes (textarea)                                      │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  [Guardar]  [Cancelar]                                       │
└─────────────────────────────────────────────────────────────┘
```

### 4.5. Pista técnica para el codegen

El `apiToken` se usará como cabecera `X-API-Key` en llamadas al Management API del conector EDC. En esta fase es solo informativo, pero dejar un comentario en el código:

```csharp
// TODO: En la fase de integración real, este token se enviará como cabecera:
// httpClient.DefaultRequestHeaders.Add("X-API-Key", model.ApiToken);
```

---

## 📤 SECCIÓN 5 — PANTALLA 2: PUBLICAR DATOS (EdcPublishDataBase)

### 5.1. Layout

La pantalla muestra una tabla/lista con todos los datasets que el perfil publica, según el mapa de datasets (Sección 7).

```
┌─────────────────────────────────────────────────────────────┐
│  RadzenCard                                                  │
│  Título: "Datasets publicados — {ProfileLabel}"              │
│  Subtítulo: "Catálogo de datasets que este participante      │
│              publica en el espacio de datos EcoDataNet"       │
│                                                              │
│  ┌─ RadzenDataGrid ─────────────────────────────────────┐   │
│  │  Columna: UC     │ Descripción              │ Ref.    │   │
│  │  ──────────────────────────────────────────────────── │   │
│  │  UC1              │ Convenios y acuerdos...  │ UC1_... │   │
│  │  UC2              │ Órdenes planificadas...  │ UC2_... │   │
│  │  ...              │ ...                      │ ...     │   │
│  └───────────────────────────────────────────────────────┘   │
│                                                              │
│  Texto: "Endpoint base: /api/usDataSet/{DatasetName}"        │
│                                                              │
│  [Publicar datos]  (botón stub, rz-primary, deshabilitado    │
│   con tooltip "Funcionalidad pendiente de integración EDC")  │
└─────────────────────────────────────────────────────────────┘
```

### 5.2. Columnas del RadzenDataGrid

| Columna | Ancho | Contenido |
|---------|-------|-----------|
| **UC** | 80px | Badge con el caso de uso (ej: `UC1`, `UC2`). Usar `RadzenBadge` con estilo `rz-badge-pill`. |
| **Descripción** | flex | Texto descriptivo del dataset (`desc` del JSON). |
| **Referencia** | 350px | Nombre técnico del dataset (`ref` del JSON). Usar fuente monoespaciada. |

### 5.3. Caso especial: Coordinador

El perfil **Coordinador** tiene `publish: []` (array vacío). En ese caso, mostrar un `RadzenAlert` informativo:

```
"Este perfil no publica datasets en el espacio de datos. Solo consume datos de otros participantes."
```

### 5.4. Botón "Publicar datos"

- Estilo: `ButtonStyle.Primary`, icono `cloud_upload`
- Estado: **Deshabilitado** (`Disabled="true"`)
- Tooltip: `"Funcionalidad pendiente de integración con conector EDC"`
- No hace nada al hacer clic (stub puro)

---

## 📥 SECCIÓN 6 — PANTALLA 3: CONSUMIR DATOS (EdcConsumeDataBase)

### 6.1. Layout

La pantalla muestra una **sección por cada perfil proveedor** del que el perfil actual consume datos. Dentro de cada sección, un panel/card por cada dataset consumido.

```
┌─────────────────────────────────────────────────────────────┐
│  RadzenCard                                                  │
│  Título: "Datasets consumidos — {ProfileLabel}"              │
│  Subtítulo: "Datos que este participante consume de          │
│              otros actores del espacio de datos EcoDataNet"   │
│                                                              │
│  ┌─ RadzenFieldset: "Datos de: SCRAP" ──────────────────┐   │
│  │  ┌─ RadzenCard (cada dataset) ────────────────────┐   │   │
│  │  │  UC: UC1                                        │   │   │
│  │  │  Descripción: Objetivos/cuotas oficiales...     │   │   │
│  │  │  Referencia: UC1_SCRAP_Publish_MarketShares_... │   │   │
│  │  │  [Consumir datos] (stub)                        │   │   │
│  │  └────────────────────────────────────────────────┘   │   │
│  │  ┌─ RadzenCard ────────────────────────────────────┐   │   │
│  │  │  ...siguiente dataset...                        │   │   │
│  │  └────────────────────────────────────────────────┘   │   │
│  └───────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌─ RadzenFieldset: "Datos de: Transportista" ──────────┐   │
│  │  ...                                                  │   │
│  └───────────────────────────────────────────────────────┘   │
│                                                              │
│  Nota: "Si un perfil consume datos de otro perfil,           │
│  se entiende que consume de TODOS los participantes           │
│  de ese perfil (no hay selección por instancia)."            │
└─────────────────────────────────────────────────────────────┘
```

### 6.2. Estructura de cada sección de proveedor

Usar `RadzenFieldset` con `Text="Datos de: {fromProfileLabel}"` y dentro de él, un `RadzenDataList` o cards individuales.

### 6.3. Botón "Consumir datos" (por cada dataset)

- Estilo: `ButtonStyle.Secondary`, icono `cloud_download`, tamaño pequeño
- Estado: **Deshabilitado** (`Disabled="true"`)
- Tooltip: `"Funcionalidad pendiente de integración con conector EDC"`

### 6.4. Caso especial: sin consumo

Si `consumeGrouped` está vacío (no debería pasar con los datos actuales, pero por precaución), mostrar un `RadzenAlert`:

```
"Este perfil no consume datos de otros participantes en el espacio de datos."
```

---

## 📊 SECCIÓN 7 — DATOS MAESTROS (MAPA DE DATASETS)

### 7.1. Fichero de datos

Crear una clase estática con los datos del mapa de datasets. Ubicación:

```
src/GreenTransit.Web/Components/Pages/EcoDataNet/Dataspace/Shared/EcoDataNetDatasetStore.cs
```

### 7.2. Modelos de datos

```csharp
public record DatasetInfo(string Ref, string Desc, string Uc);
public record ConsumeGroup(string FromProfile, string FromProfileLabel, List<DatasetInfo> Items);

public record ProfileDatasets
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;     // Para routing: "dispatch-office", "scrap", etc.
    public List<DatasetInfo> Publish { get; init; } = new();
    public List<ConsumeGroup> ConsumeGrouped { get; init; } = new();
}
```

### 7.3. Datos completos (JSON hardcodeado como constantes C#)

```csharp
public static class EcoDataNetDatasetStore
{
    public const string BaseDatasetEndpointHint = "/api/usDataSet/{DatasetName}";

    public static readonly IReadOnlyList<ProfileDatasets> Profiles = new List<ProfileDatasets>
    {
        // === OFICINA DE ASIGNACIÓN (DISPATCH_OFFICE) ===
        new ProfileDatasets
        {
            Id = "dispatch-office",
            Label = "Oficina de Asignación",
            Slug = "dispatch-office",
            Publish = new()
            {
                new("UC1_OFIRAEE_Publish_Agreements", "Convenios y acuerdos marco con condiciones, vigencia y obligaciones.", "UC1"),
                new("UC1_OFIRAEE_Publish_ServiceOrders", "Órdenes de servicio para recogida/entrega con planificación y ubicación.", "UC1"),
                new("UC1_OFIRAEE_Publish_ComplianceKPIs", "KPIs de cumplimiento por SCRAP/periodo/categoría.", "UC1"),
                new("UC1_OFIRAEE_Publish_Settlements", "Liquidaciones/compensaciones económicas por convenio/periodo.", "UC1"),
                new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics", "Órdenes planificadas con detalle logístico (ventanas, destino, etc.).", "UC2"),
                new("UC2_OFIRAEE_Publish_LogisticsKPIs", "KPIs logísticos (distancia, duración, puntualidad, volumen, etc.).", "UC2"),
                new("UC3_OFIRAEE_Publish_MobilityKPIs_Basic", "KPIs de movilidad por franjas/uso urbano para análisis DUM.", "UC3"),
                new("UC4_OFIRAEE_Publish_RecyclingKPIs", "KPIs de reciclaje/valorización agregados por periodo/tipología.", "UC4"),
                new("UC5_OFIRAEE_Publish_TraceabilityKPIs", "KPIs de trazabilidad agregados (CAC → traslado → tratamiento).", "UC5"),
                new("UC6_Publish_HeatmapEvents_Aggregated", "Eventos georreferenciados agregados para mapas de calor.", "UC6"),
                new("UC7_Publish_EmissionFactors_Active", "Factores de emisión activos (fuente/metodología/versionado).", "UC7"),
                new("UC7_Publish_Emissions_Calculated", "Emisiones calculadas por servicio/traslado en base a distancia y factores.", "UC7"),
                new("UC7_Publish_CarbonFootprintKPIs", "KPIs consolidados de huella de carbono por zona/periodo/tipología.", "UC7"),
            },
            ConsumeGrouped = new()
            {
                new("scrap", "SCRAP", new()
                {
                    new("UC1_SCRAP_Publish_MarketShares_Objectives", "Objetivos y cuotas SCRAP por periodo/categoría/territorio.", "UC1"),
                }),
                new("carrier", "Transportista", new()
                {
                    new("UC2_Gestor_Publish_WasteMoves_Execution", "Ejecución real de traslados (fechas, ruta, residuos, vehículo).", "UC1/UC2/UC3/UC7"),
                    new("UC3_Gestor_Publish_Execution_ForAudit", "Ejecución real orientada a auditoría movilidad (plan vs real).", "UC3"),
                }),
                new("plant", "Planta de tratamiento", new()
                {
                    new("UC2_Planta_Publish_Entries", "Entradas en planta con pesaje/fecha/referencia.", "UC1/UC2/UC4"),
                    new("UC2_Planta_Publish_Treatments", "Tratamiento ejecutado con impropios y resultado por fracciones.", "UC1/UC2/UC4"),
                    new("UC4_Planta_Publish_TreatmentQuality", "Calidad/rendimiento del tratamiento final (ratios, rechazos).", "UC4"),
                }),
                new("cac", "Operador de Centro de Acopio", new()
                {
                    new("UC2_CAC_Publish_AvailableVolumes", "Volúmenes disponibles por punto/método/tipología.", "UC1/UC2/UC6"),
                }),
                new("public-entity", "Entidad Pública / Ayuntamiento", new()
                {
                    new("UC1_Ayto_Publish_TonnageByMethod_Point_Period", "Toneladas municipales por punto/método/periodo.", "UC1/UC6"),
                    new("UC3_Ayto_Publish_DUMZones", "Zonificación DUM/áreas urbanas reguladas.", "UC3"),
                    new("UC3_Ayto_Publish_DUMRestrictionRules", "Reglas y restricciones DUM (horarios, condiciones, acciones).", "UC3"),
                }),
                new("producer", "Productor", new()
                {
                    new("UC5_Productor_Publish_ProductSpecs", "Ficha de producto/categoría (composición, reparabilidad, etc.).", "UC5"),
                }),
            }
        },

        // === SCRAP ===
        new ProfileDatasets
        {
            Id = "scrap",
            Label = "SCRAP",
            Slug = "scrap",
            Publish = new()
            {
                new("UC1_SCRAP_Publish_MarketShares_Objectives", "Objetivos/cuotas oficiales por categoría, territorio y periodo.", "UC1"),
                new("UC1_SCRAP_Publish_OperationalEvidence_Aggregated", "Evidencia operativa agregada (movimientos por periodo/tipología).", "UC1"),
                new("UC5_SCRAP_Publish_EcoModulationRules", "Reglas de ecomodulación (incentivos/penalizaciones por criterios).", "UC5"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC1_OFIRAEE_Publish_Agreements", "Convenios aplicables al SCRAP.", "UC1"),
                    new("UC1_OFIRAEE_Publish_Settlements", "Liquidaciones/compensaciones asociadas.", "UC1"),
                    new("UC1_OFIRAEE_Publish_ComplianceKPIs", "KPIs de cumplimiento (logro objetivo vs evidencia).", "UC1"),
                    new("UC1_OFIRAEE_Publish_ServiceOrders", "Órdenes/servicios emitidos.", "UC1"),
                    new("UC4_OFIRAEE_Publish_RecyclingKPIs", "KPIs globales de reciclaje para seguimiento.", "UC4"),
                    new("UC5_OFIRAEE_Publish_TraceabilityKPIs", "KPIs trazabilidad para rendimiento por tipología.", "UC5"),
                }),
                new("public-entity", "Entidad Pública / Ayuntamiento", new()
                {
                    new("UC1_Ayto_Publish_TonnageByMethod_Point_Period", "Datos municipales de recogida por método/punto/periodo.", "UC1"),
                }),
                new("plant", "Planta de tratamiento", new()
                {
                    new("UC4_Planta_Publish_TreatmentQuality", "Resultado final/certificado de tratamiento y ratios de recuperación.", "UC4"),
                }),
            }
        },

        // === ENTIDAD PÚBLICA / AYUNTAMIENTO ===
        new ProfileDatasets
        {
            Id = "public-entity",
            Label = "Entidad Pública / Ayuntamiento",
            Slug = "public-entity",
            Publish = new()
            {
                new("UC1_Ayto_Publish_TonnageByMethod_Point_Period", "Toneladas recogidas por punto/método/periodo y tipología.", "UC1"),
                new("UC1_Ayto_Publish_PointInventory_Embedded", "Inventario de puntos de recogida (ubicación/datos básicos).", "UC1"),
                new("UC3_Ayto_Publish_DUMZones", "Zonificación DUM (geometría/ámbitos).", "UC3"),
                new("UC3_Ayto_Publish_DUMRestrictionRules", "Restricciones DUM (condiciones, vigencias, acción).", "UC3"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC1_OFIRAEE_Publish_Settlements", "Liquidaciones/compensaciones.", "UC1"),
                    new("UC1_OFIRAEE_Publish_ComplianceKPIs", "KPIs cumplimiento del sistema.", "UC1"),
                    new("UC1_OFIRAEE_Publish_ServiceOrders", "Planificación/servicios.", "UC1"),
                    new("UC3_OFIRAEE_Publish_MobilityKPIs_Basic", "KPIs de movilidad urbana asociados a recogidas.", "UC3"),
                }),
                new("carrier", "Transportista", new()
                {
                    new("UC3_Gestor_Publish_Execution_ForAudit", "Ejecución real para auditoría plan vs real.", "UC3"),
                }),
                new("scrap", "SCRAP", new()
                {
                    new("UC1_SCRAP_Publish_MarketShares_Objectives", "Objetivos/cuotas para contraste (si aplica).", "UC1"),
                }),
            }
        },

        // === TRANSPORTISTA ===
        new ProfileDatasets
        {
            Id = "carrier",
            Label = "Transportista",
            Slug = "carrier",
            Publish = new()
            {
                new("UC2_Gestor_Publish_WasteMoves_Execution", "Traslados ejecutados (residuos, fechas, distancias, vehículo).", "UC2"),
                new("UC3_Gestor_Publish_Execution_ForAudit", "Ejecución real orientada a auditoría movilidad (servicio y tiempos).", "UC3"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics", "Órdenes planificadas logística.", "UC2"),
                    new("UC1_OFIRAEE_Publish_ServiceOrders", "Órdenes (si se usa en UC1).", "UC1"),
                }),
                new("scrap", "SCRAP", new()
                {
                    new("UC7_Publish_EmissionFactors_Active", "Factores de emisión vigentes (si se requiere).", "UC7"),
                }),
            }
        },

        // === OPERADOR DE CENTRO DE ACOPIO (CAC) ===
        new ProfileDatasets
        {
            Id = "cac",
            Label = "Operador de Centro de Acopio",
            Slug = "cac",
            Publish = new()
            {
                new("UC2_CAC_Publish_AvailableVolumes", "Volumen disponible/eventos por punto, método y tipología.", "UC2"),
            },
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics", "Servicios planificados (para preparación).", "UC2"),
                }),
            }
        },

        // === PLANTA DE TRATAMIENTO ===
        new ProfileDatasets
        {
            Id = "plant",
            Label = "Planta de tratamiento",
            Slug = "plant",
            Publish = new()
            {
                new("UC2_Planta_Publish_Entries", "Entradas y pesajes en planta (ticket, neto/bruto, fecha).", "UC2"),
                new("UC2_Planta_Publish_Treatments", "Tratamiento ejecutado (operación, impropios, fracciones, incidencias).", "UC2"),
                new("UC4_Planta_Publish_TreatmentQuality", "Calidad/rendimiento final (ratios recuperación/rechazo por tipología).", "UC4"),
            },
            ConsumeGrouped = new()
            {
                new("carrier", "Transportista", new()
                {
                    new("UC2_Gestor_Publish_WasteMoves_Execution", "Preaviso/relación de traslados para contrastar entrada y tratamiento.", "UC2"),
                }),
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC2_OFIRAEE_Publish_ServiceOrders_Logistics", "Planificación prevista (opcional).", "UC2"),
                }),
            }
        },

        // === PRODUCTOR ===
        new ProfileDatasets
        {
            Id = "producer",
            Label = "Productor",
            Slug = "producer",
            Publish = new()
            {
                new("UC5_Productor_Publish_ProductSpecs", "Ficha de producto/categoría (composición, reparabilidad, etc.).", "UC5"),
            },
            ConsumeGrouped = new()
            {
                new("scrap", "SCRAP", new()
                {
                    new("UC5_SCRAP_Publish_EcoModulationRules", "Reglas de ecomodulación.", "UC5"),
                }),
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC5_OFIRAEE_Publish_TraceabilityKPIs", "KPIs de rendimiento por tipología.", "UC5"),
                }),
                new("plant", "Planta de tratamiento", new()
                {
                    new("UC4_Planta_Publish_TreatmentQuality", "Resultados reales de tratamiento vinculables a tipologías.", "UC4/UC5"),
                }),
            }
        },

        // === COORDINADOR ===
        new ProfileDatasets
        {
            Id = "coordinator",
            Label = "Coordinador del acuerdo",
            Slug = "coordinator",
            Publish = new(),   // No publica nada
            ConsumeGrouped = new()
            {
                new("dispatch-office", "Oficina de Asignación", new()
                {
                    new("UC3_OFIRAEE_Publish_MobilityKPIs_Basic", "KPIs movilidad para análisis logístico.", "UC3"),
                }),
            }
        },
    };

    /// <summary>
    /// Obtiene los datasets de un perfil por su slug de ruta.
    /// </summary>
    public static ProfileDatasets? GetBySlug(string slug)
        => Profiles.FirstOrDefault(p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
}
```

---

## 🧩 SECCIÓN 8 — COMPONENTES REUTILIZABLES

### 8.1. Parámetros comunes de los 3 componentes base

```csharp
[Parameter, EditorRequired]
public string ProfileId { get; set; } = string.Empty;  // Slug: "dispatch-office", "scrap", etc.

[Parameter, EditorRequired]
public string ProfileLabel { get; set; } = string.Empty; // "Oficina de Asignación", "SCRAP", etc.
```

### 8.2. Carga de datos en cada componente

```csharp
private ProfileDatasets? _profile;

protected override void OnParametersSet()
{
    _profile = EcoDataNetDatasetStore.GetBySlug(ProfileId);
}
```

### 8.3. Ejemplo de página wrapper (las 24 páginas siguen este patrón)

```razor
@* EdcConfigDispatchOffice.razor *@
@page "/ecodatanet/dispatch-office/config"
@attribute [Authorize(Policy = PolicyConstants.CanAccessEcoDataNet)]

<PageTitle>Configuración EDC — Oficina de Asignación</PageTitle>

<EdcConfigBase ProfileId="dispatch-office" ProfileLabel="Oficina de Asignación" />
```

```razor
@* EdcPublishDispatchOffice.razor *@
@page "/ecodatanet/dispatch-office/publish"
@attribute [Authorize(Policy = PolicyConstants.CanAccessEcoDataNet)]

<PageTitle>Publicar Datos — Oficina de Asignación</PageTitle>

<EdcPublishDataBase ProfileId="dispatch-office" ProfileLabel="Oficina de Asignación" />
```

```razor
@* EdcConsumeDispatchOffice.razor *@
@page "/ecodatanet/dispatch-office/consume"
@attribute [Authorize(Policy = PolicyConstants.CanAccessEcoDataNet)]

<PageTitle>Consumir Datos — Oficina de Asignación</PageTitle>

<EdcConsumeDataBase ProfileId="dispatch-office" ProfileLabel="Oficina de Asignación" />
```

### 8.4. Tabla de slugs y labels para las 24 páginas

| Perfil | Slug | Label para pasar al componente |
|--------|------|-------------------------------|
| Oficina Asignación | `dispatch-office` | `Oficina de Asignación` |
| SCRAP | `scrap` | `SCRAP` |
| Ayuntamiento | `public-entity` | `Entidad Pública / Ayuntamiento` |
| Transportista | `carrier` | `Transportista` |
| CAC | `cac` | `Operador de Centro de Acopio` |
| Planta tratamiento | `plant` | `Planta de tratamiento` |
| Productor | `producer` | `Productor` |
| Coordinador | `coordinator` | `Coordinador del acuerdo` |

---

## ✅ SECCIÓN 9 — CRITERIOS DE ACEPTACIÓN

### 9.1. Compilación y estructura

- [ ] El proyecto compila sin errores (`dotnet build`).
- [ ] Se han creado **24 páginas Blazor** (8 perfiles × 3 pantallas).
- [ ] Se han creado **3 componentes base reutilizables** (`EdcConfigBase`, `EdcPublishDataBase`, `EdcConsumeDataBase`).
- [ ] Se ha creado **1 clase de datos estáticos** (`EcoDataNetDatasetStore.cs`) con el mapa completo de datasets.

### 9.2. Navegación

- [ ] En el menú lateral "EcoDataNet" aparecen **8 sub-grupos** (uno por perfil).
- [ ] Cada sub-grupo tiene **3 ítems**: Configuración, Publicar datos, Consumir datos.
- [ ] Los sub-grupos se muestran/ocultan según permisos de `PagePermissions` (sistema existente).
- [ ] Las rutas siguen el patrón `/ecodatanet/{slug}/{config|publish|consume}`.

### 9.3. Autorización

- [ ] Las 24 páginas usan `@attribute [Authorize(Policy = PolicyConstants.CanAccessEcoDataNet)]`.
- [ ] Tras desplegar, las 24 páginas aparecen en `/security/page-permissions` dentro del módulo **EcoDataNet**, destacadas en amarillo (sin permisos configurados).
- [ ] El admin puede asignar permisos por perfil desde la UI de admin existente.
- [ ] `InferModuleName()` clasifica todas las rutas `/ecodatanet/` como módulo "EcoDataNet".
- [ ] `HumanizeName()` genera nombres legibles para los 24 componentes.

### 9.4. Pantalla Configuración

- [ ] Formulario con todos los campos de la Sección 4.
- [ ] Validación de formato URL en campos obligatorios.
- [ ] `federatedCatalogUrl` solo visible si `federatedCatalogEnabled = true`.
- [ ] Botón "Guardar" muestra notificación simulada (sin persistencia).
- [ ] Botón "Probar conexión" espera 1 segundo y muestra notificación simulada.
- [ ] Botón "Cancelar" navega hacia atrás.

### 9.5. Pantalla Publicar datos

- [ ] Muestra `RadzenDataGrid` con columnas UC, Descripción, Referencia.
- [ ] Los datos coinciden exactamente con el mapa de datasets (Sección 7).
- [ ] Perfil Coordinador muestra alerta "no publica datasets".
- [ ] Botón "Publicar datos" está **deshabilitado** con tooltip explicativo.
- [ ] Se muestra el endpoint base: `/api/usDataSet/{DatasetName}`.

### 9.6. Pantalla Consumir datos

- [ ] Muestra secciones agrupadas por perfil proveedor (usando `RadzenFieldset`).
- [ ] Dentro de cada sección, cards/filas con descripción, referencia y badge UC.
- [ ] Cada dataset tiene un botón "Consumir datos" **deshabilitado** con tooltip.
- [ ] Los datos coinciden exactamente con el `consumeGrouped` del mapa (Sección 7).
- [ ] Nota visible: "se consume de TODOS los participantes de ese perfil".

### 9.7. Restricciones NO-GO

- [ ] **NO** se han creado nuevas tablas en BD.
- [ ] **NO** se han creado nuevos Commands/Queries en MediatR.
- [ ] **NO** se realizan llamadas reales a APIs EDC ni a ningún servicio externo.
- [ ] **NO** se ha implementado persistencia de configuración del conector.
- [ ] **NO** se ha inventado un nuevo sistema de permisos; se reutiliza `PageDefinitions`/`PagePermissions`.
- [ ] **NO** se han hardcodeado role checks; toda la autorización pasa por el sistema dinámico existente.

---

## 🔍 SECCIÓN 10 — INSTRUCCIONES DE "NO INVENTAR"

1. **Si algo del stack no es evidente** (ej: cómo se registra un servicio, cómo se usa `RadzenNotification`, etc.), buscar primero en las pantallas existentes del proyecto y copiar el patrón exacto.

2. **Componentes Radzen**: usar siempre los componentes de Radzen Blazor ya instalados. No instalar librerías nuevas de UI.

3. **Estilo visual**: no crear CSS custom nuevo. Usar las clases y variables CSS de Radzen y los overrides existentes del tema GreenTransit (`--gt-*`).

4. **Patrón de pantalla nueva**: seguir exactamente el checklist documentado en `instrucciones_adicionales.md`:
   - `@page "/ruta"` definida
   - `@attribute [Authorize(...)]` con policy adecuada
   - Namespace coherente (`Pages/EcoDataNet/Dataspace/`)
   - Entrada en `NavMenu.razor` con control de visibilidad por permisos
   - `InferModuleName()` actualizado si necesario
   - `HumanizeName()` actualizado

5. **No crear entidades de dominio**: todo el módulo es mock frontend con datos hardcodeados en `EcoDataNetDatasetStore.cs`.

---

## 📋 SECCIÓN 11 — ORDEN DE EJECUCIÓN RECOMENDADO

| Paso | Qué hacer | Ficheros afectados |
|------|-----------|-------------------|
| 1 | Crear `EcoDataNetDatasetStore.cs` con modelos y datos | `Shared/EcoDataNetDatasetStore.cs` |
| 2 | Crear `EdcConnectorConfigModel.cs` | `Shared/EdcConnectorConfigModel.cs` |
| 3 | Crear los 3 componentes base | `Shared/EdcConfigBase.razor`, `EdcPublishDataBase.razor`, `EdcConsumeDataBase.razor` |
| 4 | Crear las 24 páginas wrapper (una por perfil/pantalla) | 8 carpetas × 3 ficheros `.razor` |
| 5 | Añadir policy `CanAccessEcoDataNet` | `PolicyConstants.cs`, `Program.cs` |
| 6 | Actualizar `NavMenu.razor` con los 8 sub-grupos | `NavMenu.razor` |
| 7 | Actualizar `_groupRoutes` en `NavMenu.razor` | `NavMenu.razor` (code-behind) |
| 8 | Actualizar `InferModuleName()` (verificar regla existente) | `PageDiscoveryService.cs` |
| 9 | Actualizar `HumanizeName()` con los 24 nombres | `PageDiscoveryService.cs` |
| 10 | Compilar y verificar | `dotnet build` |

---

## 📚 SECCIÓN 12 — ACTUALIZACIÓN DE DOCUMENTACIÓN

### 12.1. En `Mapa_Funcionalidades.md`

Añadir debajo del epígrafe 11.1 (Publicar Datos en EcoDataNet) las siguientes subsecciones:

```markdown
### 11.2. EcoDataNet Dataspace — Pantallas por Perfil

> Módulo de visualización del catálogo de datasets por perfil funcional del ecosistema EcoDataNet.
> Cada perfil dispone de 3 pantallas: Configuración del conector EDC, Publicar datos (catálogo informativo)
> y Consumir datos (datasets que consume de otros participantes).
> En esta fase es mock frontend sin persistencia ni integración real con conectores EDC.

**Perfiles con pantallas**:

| Perfil | Rutas |
|---|---|
| Oficina de Asignación | `/ecodatanet/dispatch-office/{config\|publish\|consume}` |
| SCRAP | `/ecodatanet/scrap/{config\|publish\|consume}` |
| Ayuntamiento | `/ecodatanet/public-entity/{config\|publish\|consume}` |
| Transportista | `/ecodatanet/carrier/{config\|publish\|consume}` |
| CAC | `/ecodatanet/cac/{config\|publish\|consume}` |
| Planta tratamiento | `/ecodatanet/plant/{config\|publish\|consume}` |
| Productor | `/ecodatanet/producer/{config\|publish\|consume}` |
| Coordinador | `/ecodatanet/coordinator/{config\|publish\|consume}` |

**Acceso**: `@attribute [Authorize]` — cualquier usuario autenticado. Control fino por `PagePermissions`.
**Estado**: ⬜ PENDIENTE (mock frontend)
```

### 12.2. En `COPILOT_CONTEXT.md`

Añadir referencia al nuevo módulo y a este prompt.

---

*Fin del prompt. Ejecutar en orden. Adjuntar siempre `COPILOT_CONTEXT.md`, `README.md` y `Mapa_Funcionalidades.md` como contexto.*
