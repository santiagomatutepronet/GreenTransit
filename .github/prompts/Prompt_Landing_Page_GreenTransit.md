# PROMPT PARA GITHUB COPILOT

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `App.razor`, `MainLayout.razor`, `NavMenu.razor`, `Program.cs`, `_Imports.razor`, `wwwroot/css/site.css` y la carpeta `wwwroot/images/`. Adjunta también `COPILOT_CONTEXT.md`, `README.md` y el schema SQL `Crear_BD_v4_1.sql` para contexto completo.

---

## Contexto del proyecto

Estoy desarrollando **GreenTransit**, una aplicación **Blazor Web App (.NET 10)** con:
- **Arquitectura**: Clean Architecture (5 proyectos: Domain, Application, Infrastructure, Web, Tests), EF Core, MediatR (CQRS), FluentValidation, Serilog.
- **Autenticación**: OpenID Connect contra proveedor externo (Azure). Cookie + OIDC. `LoginPath = "/login"`.
- **Multi-tenant**: filtrado por `OwnerId` en todas las queries.
- **UI**: Radzen Blazor (`Radzen.Blazor` NuGet), tema `humanistic` con override corporativo en `site.css`.
- **Perfiles**: ADMIN, SCRAP, PRODUCER, CARRIER, PUBLIC_ENT, CAC_OP, PLANT_OP, COORDINATOR, DISPATCH_OFFICE.
- **Estructura del proyecto Web**: `src/GreenTransit.Web/Components/Pages/`, layouts en `Components/Layout/`.
- **Assets existentes**:
  - Logo: buscar en `wwwroot/images/` un fichero tipo `logo*.png` o `logo*.svg` (el logo dice "GreenTransit by EcoDataNet — powered by Pronet Ise").
  - Fondo de login: buscar `background_login.png` o similar en `wwwroot/images/` (gradiente azul oscuro/teal con partículas).
  - Diagrama de integración EcoDataNet: `wwwroot/images/ecodatanet/integracion-greentransit-ecodatanet.png`.
- **Paleta de colores corporativa** (ya definida en `:root` de `site.css`):
  - Dark Petroleum (primary): `#0A404B`
  - Dark Blue (sidebar/headings): `#17233C`
  - Teal claro (secondary 1): `#8ACCC3`
  - Alt 1 (fondos suaves): `#BEE2E4`
  - Stone Green: `#4F5E5A`
  - Graphite Black: `#262626`
  - Amarillo (accent): `#D8B00E`
  - Naranja: `#D36F15`
  - Variables Radzen: `--rz-primary`, `--rz-primary-light`, `--rz-secondary`, etc.

---

## Objetivo

Crear una **página de landing pública** (sin autenticación) que sea la **nueva ruta de inicio** (`/`) de la aplicación. Esta landing explica qué es GreenTransit, su integración con el espacio de datos EcoDataNet, los módulos de la plataforma y el valor para cada perfil. Incluye un botón "Iniciar sesión" que lleva al login OIDC existente. **La home actual post-login (dashboard) NO se toca**; solo se añade esta capa pública previa.

---

## PASO 1 — Detección y análisis del repositorio

Antes de escribir código, busca y confirma:

1. **Framework y Router**: Abre `App.razor` (o `Routes.razor`). Identifica si usa `<Router>` con `<AuthorizeRouteView>` o `<RouteView>`. Anota el `DefaultLayout`.
2. **Login path**: Busca `@page "/login"` en todo `src/GreenTransit.Web/`. Si es un Razor Page (`Login.cshtml`), anota la ruta. Si es un endpoint, anótalo.
3. **Layout actual**: Abre `MainLayout.razor`. Confirma que usa `@inherits LayoutComponentBase` y tiene `@attribute [Authorize]` (o equivalente). Confirma que el layout envuelve `@Body` con sidebar + topbar.
4. **Assets**: Lista los ficheros en `wwwroot/images/` y `wwwroot/images/ecodatanet/`. Confirma la existencia de:
   - Logo (cualquier formato).
   - `background_login.png` (o nombre similar — es la imagen de fondo azul oscuro).
   - `integracion-greentransit-ecodatanet.png`.
5. **CSS**: Abre `wwwroot/css/site.css`. Confirma que existen las variables `:root` corporativas (`--rz-primary: #0A404B`, etc.). Si hay modo oscuro, anota el selector.
6. **Radzen instalado**: Verifica en `_Imports.razor` que tiene `@using Radzen` y `@using Radzen.Blazor`. Verifica en `App.razor` que tiene `<RadzenTheme>` y el script de Radzen.
7. **Program.cs**: Busca `app.MapBlazorHub().RequireAuthorization()` o equivalente que proteja las rutas. Esto es lo que hay que ajustar para permitir la landing sin auth.

---

## PASO 2 — Crear el layout público (sin sidebar ni topbar)

La landing NO debe usar el `MainLayout` (que tiene sidebar, topbar, requiere auth). Necesita un layout limpio.

### 2.1. Crear `LandingLayout.razor`

Ubicación: `src/GreenTransit.Web/Components/Layout/LandingLayout.razor`

```razor
@inherits LayoutComponentBase

<div class="landing-layout">
    @Body
</div>
```

### 2.2. Crear `LandingLayout.razor.css`

```css
.landing-layout {
    min-height: 100vh;
    background-color: var(--rz-body-background-color, #FFFFFF);
    color: var(--rz-body-color, #262626);
    overflow-x: hidden;
}
```

**No debe tener** `@attribute [Authorize]`. Es un layout público.

---

## PASO 3 — Crear la página Landing.razor

Ubicación: `src/GreenTransit.Web/Components/Pages/Landing.razor`

### 3.0. Directivas de la página

```razor
@page "/"
@layout LandingLayout
@using Radzen
@using Radzen.Blazor
@inject NavigationManager Navigation
```

**IMPORTANTE**: NO debe tener `@attribute [Authorize]`. Es la única página (junto con login) accesible sin autenticación.

### 3.1. Estructura de secciones del landing

El landing debe tener estas secciones, EN ESTE ORDEN, construidas enteramente con componentes Radzen (`RadzenRow`, `RadzenColumn`, `RadzenCard`, `RadzenButton`, `RadzenText`, `RadzenImage`, `RadzenChart`, etc.):

---

#### SECCIÓN 1 — HERO (pantalla completa)

- **Fondo**: usar la imagen `background_login.png` (o como se llame) como `background-image` con overlay semitransparente oscuro (`rgba(23, 35, 60, 0.85)` — Dark Blue).
- **Contenido centrado vertical y horizontalmente**:
  - Logo de GreenTransit (imagen existente en wwwroot, `max-height: 80px`).
  - Título principal (RadzenText, TagName `h1`, tamaño grande, color blanco):

    ```
    GreenTransit by EcoDataNet
    ```

  - Subtítulo (RadzenText, TagName `h2`, peso ligero, color `#8ACCC3`):

    ```
    La plataforma operativa del espacio de datos para la gestión integral de residuos industriales
    ```

  - Párrafo descriptivo (RadzenText, color `rgba(255,255,255,0.85)`, max-width 700px, centrado):

    ```
    GreenTransit conecta a todos los actores del ecosistema de residuos —SCRAP, productores, 
    transportistas, plantas de tratamiento, entidades públicas y coordinadores— en una única 
    plataforma trazable, auditable e interoperable, integrada nativamente con el espacio de 
    datos EcoDataNet.
    ```

  - Dos botones (RadzenRow centrado, gap 16px):
    - **Botón primario** (RadzenButton, ButtonStyle.Primary, Size.Large):
      - Texto: `"Iniciar sesión"`
      - Icono: `"login"` (o `"lock_open"`)
      - Click: `Navigation.NavigateTo("/login", forceLoad: true);`
    - **Botón secundario** (RadzenButton, ButtonStyle.Light, Size.Large, Variant.Outlined):
      - Texto: `"Explorar la plataforma"`
      - Icono: `"arrow_downward"`
      - Click: scroll suave al anchor `#modulos` (usar JS interop o anchor link).

- **Responsive**: en mobile, reducir tamaño de título a `1.8rem`, subtítulo a `1.1rem`.
- **ARIA**: `role="banner"` en el contenedor, `aria-label` en los botones.

---

#### SECCIÓN 2 — QUÉ ES GREENTRANSIT (id="que-es")

- **Fondo**: blanco (o `var(--rz-body-background-color)`).
- **RadzenRow con 2 columnas** (RadzenColumn Size=12 SizeMD=6):
  - **Columna izquierda**: texto.
    - Encabezado (RadzenText, h2): `"¿Qué es GreenTransit?"`
    - Párrafo:

      ```
      GreenTransit es una plataforma web multi-rol, multi-tenant y preparada para espacios 
      de datos (IDSA/Gaia-X) que cubre el ciclo completo de la gestión integral de residuos: 
      desde la planificación de órdenes de servicio, pasando por la ejecución logística, el 
      pesaje y entrada en planta, el tratamiento y clasificación, la gestión documental, las 
      liquidaciones económicas, hasta el reporting regulatorio y el análisis de KPIs.
      ```

    - Lista de highlights en 3 mini-cards (RadzenRow de 3 cols):
      - 🔗 **Interoperable**: Conectada con EcoDataNet mediante conectores EDC (Eclipse Dataspace Components).
      - 🔒 **Soberanía del dato**: Cada participante controla qué datos comparte y bajo qué condiciones (políticas ODRL).
      - 📊 **Analítica operativa**: Más de 25 dashboards especializados con KPIs en tiempo real.

  - **Columna derecha**: imagen del diagrama de integración.
    - `RadzenImage` apuntando a `images/ecodatanet/integracion-greentransit-ecodatanet.png`.
    - `alt="Diagrama de integración GreenTransit – EcoDataNet"`.
    - `style="width: 100%; border-radius: 8px; box-shadow: 0 4px 24px rgba(0,0,0,0.10)"`.

---

#### SECCIÓN 3 — INTEGRACIÓN CON ECODATANET (id="ecodatanet")

- **Fondo**: gradiente suave de `#F7FAFA` a blanco (o color de fondo alterno).
- **Encabezado centrado** (RadzenText, h2):

  ```
  Integración nativa con el espacio de datos EcoDataNet
  ```

- **Subtítulo centrado**:

  ```
  GreenTransit actúa como la capa transaccional que permite a cada participante publicar y 
  consumir datos del espacio de datos sin necesidad de desplegar su propio conector EDC ni 
  conocer los detalles técnicos del protocolo IDSA.
  ```

- **RadzenRow con 3 columnas** (Size=12, SizeMD=4), cada una con una RadzenCard:

  **Card 1 — Publicar datos**
  - Icono: `"cloud_upload"` (Material icon o Bootstrap icon `bi-cloud-upload-fill`), color `#0A404B`.
  - Título: `"Publicar datos"`
  - Texto: `"Publica datos operativos de gestión de residuos hacia EcoDataNet directamente desde la interfaz, sin configuración técnica."`

  **Card 2 — Consumir datos**
  - Icono: `"cloud_download"`, color `#8ACCC3`.
  - Título: `"Consumir datos"`
  - Texto: `"Accede a informes sectoriales, benchmarks y datos de referencia del espacio de datos para tomar decisiones informadas."`

  **Card 3 — Trazabilidad soberana**
  - Icono: `"verified_user"`, color `#D8B00E`.
  - Título: `"Trazabilidad soberana"`
  - Texto: `"Cada transacción queda registrada con notarización inmutable. Contratos ODRL garantizan las condiciones de uso de tus datos."`

- **Debajo de las cards**, si existe la ruta `/ecodatanet/publish` en la app, añadir un texto pequeño con enlace:

  ```
  ¿Ya tienes cuenta? Accede directamente al módulo de publicación de datos.
  ```
  (Enlace a `/ecodatanet/publish`, solo visible tras autenticación — por ahora, el botón puede apuntar a `/login`).

---

#### SECCIÓN 4 — MÓDULOS DE LA PLATAFORMA (id="modulos")

- **Fondo**: blanco.
- **Encabezado centrado** (h2): `"Módulos de la plataforma"`
- **Subtítulo**: `"Cobertura funcional completa del ciclo del residuo, desde la orden de servicio hasta el reporting regulatorio."`
- **RadzenRow** con cards de módulos (Size=12, SizeSM=6, SizeLG=4), cada card con icono, nombre y descripción breve:

| # | Icono (Material) | Módulo | Descripción |
|---|---|---|---|
| 1 | `"inventory_2"` | **Catálogos Maestros** | Entidades, códigos LER, residuos, operaciones de tratamiento y catálogos normativos. |
| 2 | `"handshake"` | **Contratación y Economía** | Acuerdos marco, liquidaciones con desglose, cuotas de mercado y reglas de ecomodulación versionadas. |
| 3 | `"local_shipping"` | **Flujo Operativo (Core Logistics)** | Ciclo completo del traslado: orden de servicio → recogida → CAC → planta → clasificación → tratamiento. Vista 360º. |
| 4 | `"eco"` | **Sostenibilidad y Emisiones** | Huella de carbono (Scope 1 y 2), incidencias operativas, control de zonas DUM con geofencing. |
| 5 | `"assessment"` | **Reporting y Trazabilidad** | +25 dashboards especializados, KPIs regulatorios, gestión documental con hash SHA-256, exportación XLSX. |
| 6 | `"directions_car"` | **Movilidad Urbana (UC3)** | Análisis de impacto en movilidad, índice de conflicto, simulador DUM, mapas de calor urbanos. |
| 7 | `"description"` | **Declaraciones de Producción** | Declaraciones con flujo de estados, importación masiva CSV/XLSX, anticipación al Pasaporte Digital de Producto (DPP). |
| 8 | `"cloud_sync"` | **EcoDataNet — Espacio de Datos** | Publicación y consumo de datos mediante conectores EDC. Integración IDSA/Gaia-X. |
| 9 | `"gavel"` | **Cumplimiento Normativo (RAP)** | Dashboards de cumplimiento de la Ley 7/2022, alertas de cuotas, auditoría de acuerdos, desviaciones. |
| 10 | `"co2"` | **Huella de Carbono** | Emisiones Scope 1 (transporte) y Scope 2 (energía planta), factores versionados, KPIs CO₂ publicables. |

- Cada card debe usar `RadzenCard` con estilo consistente: borde `1px solid var(--rz-border-color)`, `border-radius: 12px`, `padding: 24px`, sombra suave.
- En hover, sombra más pronunciada (CSS transition).

---

#### SECCIÓN 5 — CASOS DE USO ECODATANET (id="casos-de-uso")

- **Fondo**: alterno (gris claro `#F7FAFA`).
- **Encabezado centrado** (h2): `"7 casos de uso cubiertos por EcoDataNet"`
- **Subtítulo**: `"GreenTransit genera o consume directamente los datasets definidos para cada caso de uso del proyecto."`
- **Tabla responsive** (RadzenDataGrid o tabla HTML estilizada con clases Radzen) con 3 columnas:

| Caso | Objetivo | Cobertura GreenTransit |
|------|----------|------------------------|
| **UC1** | Cumplimiento normativo RAP | Acuerdos, órdenes de servicio, KPIs de cumplimiento, liquidaciones y cuotas de mercado. |
| **UC2** | Optimización recogida RAEE | Detalle logístico, traslados ejecutados, entradas en planta, KPIs de distancia/puntualidad. |
| **UC3** | Movilidad urbana (DUM) | Zonas DUM con geofencing, simulador de impacto, KPIs por franjas horarias. |
| **UC4** | Tratamiento y reciclaje | Tasas de reciclaje, energía recuperada, reutilización, calidad y rendimiento real. |
| **UC5** | Ecomodulación y DPP-lite | Trazabilidad end-to-end (Vista 360º), reglas de ecomodulación, fichas de producto. |
| **UC6** | Mapas de calor de residuos | Datos georreferenciados de traslados, eventos agregados para heatmaps de densidad. |
| **UC7** | Huella de carbono | Factores de emisión versionados, CO₂ por traslado, energía planta, KPIs ambientales. |

- El texto de la tabla es orientativo (datos de ejemplo, no reales).

---

#### SECCIÓN 6 — GRÁFICOS MOCK / ATREZZO (id="kpis")

- **Fondo**: blanco.
- **Encabezado centrado** (h2): `"KPIs y analítica en tiempo real"`
- **Subtítulo**: `"Ejemplo ilustrativo de los dashboards disponibles en la plataforma (datos de demostración)."`
- **RadzenRow con 2–3 columnas** de gráficos mock:

  **Gráfico 1 — Donut de residuos por tipo (RadzenChart)**
  - Usar `RadzenDonutSeries` con datos mock:
    - RAEE: 42%, Envases: 28%, Papel/Cartón: 18%, Otros: 12%.
  - Colores de la paleta corporativa: `#0A404B`, `#8ACCC3`, `#D8B00E`, `#D36F15`.
  - Título: `"Distribución de residuos por tipología"`.

  **Gráfico 2 — Barras de kg recogidos por mes (RadzenChart)**
  - Usar `RadzenBarSeries` con datos mock de 6 meses:
    - Ene: 12.400, Feb: 15.200, Mar: 18.700, Abr: 14.300, May: 21.500, Jun: 19.800.
  - Color primary: `#0A404B`.
  - Título: `"Kg recogidos por mes (ejemplo)"`

  **Gráfico 3 — KPI cards resumen (sin chart, solo cards numéricas)**
  - 4 mini RadzenCards en fila (Size=6, SizeMD=3):
    - 📦 **12.847** — Traslados gestionados (icono `local_shipping`).
    - ♻️ **87,3%** — Tasa de reciclaje (icono `recycling`).
    - 🌍 **-14,2%** — Reducción CO₂ vs año anterior (icono `co2`, color verde `#11845B`).
    - ⚖️ **1.245 t** — Residuos tratados (icono `scale`).

- **Nota al pie** (texto pequeño, cursiva, color gris):
  ```
  * Datos ilustrativos de demostración. Los dashboards reales muestran información operativa en tiempo real.
  ```

---

#### SECCIÓN 7 — PERFILES Y VALOR POR ROL (id="perfiles")

- **Fondo**: alterno (`#F7FAFA`).
- **Encabezado centrado** (h2): `"Una plataforma, múltiples perspectivas"`
- **Subtítulo**: `"Cada perfil accede a vistas, acciones y dashboards adaptados a su rol dentro del ecosistema."`
- **RadzenRow** con cards de perfil (Size=12, SizeSM=6, SizeLG=4), cada card con:
  - Icono + nombre del perfil (bold).
  - Breve descripción del valor.

| Perfil | Icono | Valor principal |
|--------|-------|-----------------|
| **SCRAP** | `"hub"` | Reporting regulatorio automatizado, benchmarking sectorial, gestión económica integrada con ecomodulación y trazabilidad verificable end-to-end. |
| **Productor** | `"factory"` | Declaraciones de producción con flujo de validación, visibilidad sobre el destino de sus residuos y anticipación al Pasaporte Digital de Producto (DPP). |
| **Transportista** | `"local_shipping"` | Ejecución de recogidas en campo, confirmación de cargas, registro logístico y KPIs de ruta con cálculo automático de emisiones. |
| **Planta de tratamiento** | `"precision_manufacturing"` | Registro de entradas, pesaje, clasificación, tratamiento y declaración energética. Panel operativo dedicado con KPIs de rendimiento. |
| **Entidad Pública** | `"account_balance"` | Supervisión de servicios por SCRAP, histórico de recogidas, liquidaciones, cumplimiento de objetivos municipales y visibilidad ambiental. |
| **Coordinador** | `"groups"` | Lectura transversal del ámbito de acuerdos, análisis de movilidad y optimización logística entre SCRAPs vinculados. |
| **Oficina de Asignación** | `"assignment"` | Planificación logística, creación de traslados, asignación de transportistas y visión operativa completa del tenant. |
| **Operador CAC** | `"warehouse"` | Registro de entradas en centro de acopio ciudadano, gestión de stock y tickets pendientes. |
| **Administrador** | `"admin_panel_settings"` | CRUD total, gestión de usuarios y perfiles, configuración de permisos por pantalla y catálogos normativos. |

---

#### SECCIÓN 8 — FOOTER CON CTA FINAL

- **Fondo**: Dark Blue (`#17233C`), texto blanco.
- **Contenido centrado**:
  - Logo (versión clara si existe, o el mismo logo con fondo transparente).
  - Texto:

    ```
    ¿Preparado para conectar con el espacio de datos?
    ```

  - Botón "Iniciar sesión" (RadzenButton, Primary, Size.Large) → navega a `/login`.
  - Texto legal pequeño (gris claro):

    ```
    GreenTransit by EcoDataNet — powered by Pronet Ise · © 2026
    Plataforma desarrollada en el marco del proyecto EcoDataNet.
    ```

  - Links secundarios opcionales (si existen rutas): `Publicar datos en EcoDataNet` → `/login`.

---

## PASO 4 — Crear el archivo CSS del landing

Ubicación: `src/GreenTransit.Web/Components/Pages/Landing.razor.css`

Incluir estilos scoped para:
- `.landing-hero`: background-image con la imagen de fondo de login, `background-size: cover`, `background-position: center`, `min-height: 100vh`, `display: flex`, overlay, `position: relative`.
- `.landing-hero::before`: overlay semitransparente `rgba(23, 35, 60, 0.85)`.
- `.landing-section`: `padding: 80px 24px`, `max-width: 1200px`, `margin: 0 auto`.
- `.landing-section-alt`: fondo `#F7FAFA`.
- `.module-card`: hover con `box-shadow` más pronunciada, `transition: 0.3s`.
- `.kpi-card`: número grande (`font-size: 2.2rem`, `font-weight: 700`, `color: var(--rz-primary)`).
- Responsive: `@media (max-width: 768px)` — ajustar paddings, font-sizes, ocultar imagen del diagrama en mobile (o ponerla debajo del texto).

**USAR EXCLUSIVAMENTE variables CSS de la paleta corporativa** (`--rz-primary`, `--rz-primary-light`, `--rz-secondary`, etc.) y los colores hex documentados. NO inventar colores.

---

## PASO 5 — Ajustar el routing para permitir acceso público

### 5.1. En `App.razor` (o equivalente)

El `Router` debe permitir que la página `/` (Landing) y `/login` se rendericen SIN autenticación. El resto sigue protegido.

**Estrategia**: Si el Router usa `<AuthorizeRouteView>`, la landing ya funciona porque NO tiene `@attribute [Authorize]`. Simplemente verifica que el `DefaultLayout` no fuerce auth (la landing usa `@layout LandingLayout`, que no requiere auth).

**Si** la app usa `RequireAuthorization()` global en `Program.cs` (ej: `app.MapRazorPages().RequireAuthorization()`):
- Añadir una `AuthorizationPolicy` por defecto que permita anónimos en la ruta `/`.
- O mejor: usar `[AllowAnonymous]` en la página Landing (si es Blazor SSR con Razor Pages backend).
- O configurar un `FallbackPolicy` que no sea restrictivo, y usar `[Authorize]` explícito en las páginas protegidas (que ya lo tienen).

### 5.2. Si la home actual está en `@page "/"`

Busca si hay una página existente con `@page "/"`. Si existe:
- **Cámbiala** a `@page "/dashboard"` (u otra ruta como `/home`).
- Actualiza las referencias en `NavMenu.razor` para que el enlace "Home" / "Dashboard" apunte a la nueva ruta.
- La landing se queda con `@page "/"`.

### 5.3. Verificar el flujo login → dashboard

Tras el login OIDC, el redirect debe llevar al dashboard (antes era `/`, ahora será `/dashboard` o la ruta que hayas reasignado). Busca en `Program.cs` o en las opciones de Cookie/OIDC dónde se configura el redirect post-login:
- `options.Events.OnTokenValidated` → redirect.
- O `options.CallbackPath` → post-redirect.
- Si usa `LoginPath = "/login"` con cookie, el `ReturnUrl` por defecto debería ser `/` (que ahora es la landing), así que tras login debería redirigir al dashboard. **Ajustar** para que el `ReturnUrl` apunte a `/dashboard` si la home se ha movido.

**ALTERNATIVA SEGURA**: En la configuración de cookies:
```csharp
options.Events.OnSignedIn = context =>
{
    // Si el ReturnUrl es "/" (landing), redirigir al dashboard real
    if (context.Properties?.RedirectUri == "/" || context.Properties?.RedirectUri == null)
    {
        context.Properties!.RedirectUri = "/dashboard";
    }
    return Task.CompletedTask;
};
```

---

## PASO 6 — Ajustar NavMenu.razor (si aplica)

Si el menú lateral tiene un enlace "Home" que apunta a `/`:
- Cambiarlo para que apunte a `/dashboard` (o la nueva ruta de la home post-login).
- No añadir la landing al menú lateral (es pública, no aparece dentro de la app autenticada).

---

## PASO 7 — Verificación de no-ruptura

Ejecuta los siguientes checks mentales antes de dar por terminado:

1. **`dotnet build`** sin errores.
2. Navegar a `/` sin estar autenticado → se ve la landing, sin sidebar ni topbar.
3. Hacer clic en "Iniciar sesión" → redirige a `/login` → flujo OIDC → tras login llega al dashboard (no a la landing de nuevo).
4. Si accede a `/dashboard` sin auth → redirige a `/login` (flujo protegido intacto).
5. Si está autenticado y navega a `/` → ve la landing (es pública, se permite). El botón "Iniciar sesión" puede ocultarse o cambiar a "Ir al dashboard" si se desea.
6. Todas las demás rutas (`/entities`, `/waste-moves`, etc.) siguen requiriendo autenticación.
7. El layout `MainLayout` sigue intacto.
8. No se ha introducido ningún paquete NuGet nuevo.
9. La landing es responsive (probar en viewport de 375px).
10. Los gráficos mock se renderizan correctamente.

---

## PASO 8 — Detección inteligente de autenticación en la landing (OPCIONAL pero recomendado)

Si es fácil inyectar `AuthenticationStateProvider` en la landing:

```razor
@inject AuthenticationStateProvider AuthState

@code {
    private bool _isAuthenticated;

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        _isAuthenticated = state.User?.Identity?.IsAuthenticated ?? false;
    }
}
```

Si `_isAuthenticated` es true:
- Cambiar botón hero de "Iniciar sesión" a "Ir al dashboard" → `Navigation.NavigateTo("/dashboard")`.
- Añadir un badge "Ya has iniciado sesión" en el hero.

---

## RESUMEN DE ARCHIVOS A CREAR / MODIFICAR

| Acción | Archivo | Descripción |
|--------|---------|-------------|
| **CREAR** | `Components/Layout/LandingLayout.razor` | Layout limpio sin sidebar/topbar, sin auth. |
| **CREAR** | `Components/Layout/LandingLayout.razor.css` | Estilos mínimos del layout público. |
| **CREAR** | `Components/Pages/Landing.razor` | La página de landing completa con 8 secciones. |
| **CREAR** | `Components/Pages/Landing.razor.css` | Estilos scoped del landing (hero, secciones, cards, responsive). |
| **MODIFICAR** | Página home existente (`@page "/"`) | Cambiar ruta a `/dashboard` (si hay conflicto). |
| **MODIFICAR** | `NavMenu.razor` | Actualizar enlace Home → `/dashboard`. |
| **MODIFICAR** | `Program.cs` (posiblemente) | Asegurar que `/` es accesible sin auth. Ajustar redirect post-login. |
| **MODIFICAR** | `App.razor` (posiblemente) | Solo si hace falta ajustar el Router para rutas anónimas. |

---

## CONTENIDO COPY — REFERENCIA RÁPIDA

**Tagline hero**: `"GreenTransit by EcoDataNet"`
**Subtítulo hero**: `"La plataforma operativa del espacio de datos para la gestión integral de residuos industriales"`
**Claim EcoDataNet**: `"GreenTransit no es una app adicional. Es la pieza operativa que hace posible que EcoDataNet materialice, con evidencias auditables, lo que el espacio de datos promete: interoperabilidad, soberanía, trazabilidad, cumplimiento normativo y explotación de datos."`
**Disclaimer gráficos**: `"* Datos ilustrativos de demostración. Los dashboards reales muestran información operativa en tiempo real."`

---

# CHECKLIST DE VALIDACIÓN

Tras implementar, verificar cada punto:

- [ ] 1. **Ruta `/` sin auth**: Navegar a `/` sin sesión muestra la landing completa, sin sidebar ni topbar.
- [ ] 2. **Ruta `/` con auth**: Navegar a `/` con sesión activa muestra la landing (con botón "Ir al dashboard" si se implementó PASO 8).
- [ ] 3. **Botón "Iniciar sesión"**: Hace clic y redirige a `/login` correctamente (flujo OIDC se inicia).
- [ ] 4. **Redirect post-login**: Tras completar el login OIDC, el usuario llega al dashboard (no vuelve a la landing).
- [ ] 5. **Dashboard intacto**: La home post-login (`/dashboard` o equivalente) funciona igual que antes.
- [ ] 6. **Rutas protegidas**: Acceder a `/entities`, `/waste-moves`, etc. sin auth redirige a `/login`.
- [ ] 7. **Layout landing**: No tiene sidebar, ni topbar, ni menú de la app. Es un layout limpio.
- [ ] 8. **Paleta corporativa**: Todos los colores son de la paleta existente (Dark Petroleum `#0A404B`, Dark Blue `#17233C`, Teal `#8ACCC3`, etc.). Ningún color inventado.
- [ ] 9. **Componentes Radzen**: Toda la UI usa RadzenRow, RadzenColumn, RadzenCard, RadzenButton, RadzenText, RadzenImage, RadzenChart. No se usan componentes HTML crudos salvo envoltorio mínimo.
- [ ] 10. **Assets reutilizados**: Logo, fondo de login y diagrama de integración EcoDataNet son los existentes en `wwwroot`. No se duplican ni se añaden assets nuevos.
- [ ] 11. **Responsive mobile**: En viewport 375px: hero legible, cards apiladas, gráficos reducidos, tabla de UC scrollable horizontalmente.
- [ ] 12. **Accesibilidad**: Imágenes con `alt`, botones con `aria-label`, contraste de texto sobre fondos oscuros ≥ 4.5:1.
- [ ] 13. **Modo oscuro**: Si la app soporta toggle dark/light, la landing respeta las variables CSS del modo activo (no se rompe).
- [ ] 14. **Sin librerías nuevas**: No se ha añadido ningún paquete NuGet, npm ni CDN que no estuviera ya en el proyecto.
- [ ] 15. **Build limpio**: `dotnet build` sin warnings ni errores relacionados con los cambios.
- [ ] 16. **EcoDataNet destacado**: El nombre "EcoDataNet" aparece de forma prominente en el hero, en la sección de integración (§3), en los casos de uso (§5) y en el footer. Es el hilo conductor del landing.
