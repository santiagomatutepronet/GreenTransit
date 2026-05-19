# Prompt: Rebranding Radzen Blazor (Humanistic) → Identidad Corporativa GreenTransit

> **Objetivo**: Sustituir los colores y tipografías del tema Humanistic de Radzen Blazor por la paleta corporativa y tipografía PP Mori de GreenTransit, aplicado globalmente a TODOS los controles (NavBar/Sidebar, DataGrids, formularios, botones, cards, diálogos, badges, tabs, dropdowns, notificaciones, charts, etc.) tanto en modo claro como oscuro.

---

## 📋 CONTEXTO DEL PROYECTO (adjuntar siempre)

Archivos de referencia obligatorios para Copilot:
- `README.md` — stack tecnológico (.NET 10, Blazor Web App, Radzen Blazor Components, tema Humanistic)
- `COPILOT_CONTEXT.md` — estado actual del proyecto
- Este archivo (`Prompt_Rebranding_Radzen_GreenTransit.md`)

### Stack relevante
- **UI Framework**: Radzen Blazor Components (tema `humanistic`)
- **Carga del tema**: `<link rel="stylesheet" href="_content/Radzen.Blazor/css/humanistic.css">` (o humanistic-dark.css)
- **CSS personalizado**: `wwwroot/css/site.css` (cargado DESPUÉS del tema Radzen)
- **Layout principal**: `MainLayout.razor` con `<RadzenLayout>`, `<RadzenSidebar>`, `<RadzenHeader>`, `<RadzenBody>`

---

## 🎨 SECCIÓN 1: PALETA DE COLORES CORPORATIVA

### 1.1 Colores Primarios (fondos oscuros, sidebar, header, texto principal)

| Token | Nombre | Hex | Uso principal |
|-------|--------|-----|---------------|
| `--gt-graphite-black` | Graphite Black | `#262626` | Texto principal, fondos oscuros |
| `--gt-dark-blue` | Dark Blue | `#17233C` | Sidebar, header, fondos de navegación |
| `--gt-dark-petroleum` | Dark Petroleum | `#0A404B` | **Color primario de acento** (botones, links, selecciones) |
| `--gt-stone-green` | Stone Green | `#4F5E5A` | Textos secundarios, bordes, fondos sutiles |

### 1.2 Colores Secundarios (gráficos, badges, indicadores)

| Token | Hex | Uso |
|-------|-----|-----|
| `--gt-secondary-1` | `#8ACCC3` | Teal / acento secundario |
| `--gt-secondary-2` | `#B4B736` | Verde oliva |
| `--gt-secondary-3` | `#D8B00E` | Dorado |
| `--gt-secondary-4` | `#D36F15` | Naranja |
| `--gt-secondary-5` | `#C13E43` | Rojo oscuro |
| `--gt-secondary-6` | `#6E4583` | Púrpura |
| `--gt-secondary-7` | `#535497` | Azul índigo |

### 1.3 Colores Alternativos (versiones más claras, hovers, fondos de cards)

| Token | Hex |
|-------|-----|
| `--gt-alt-1` | `#BEE2E4` |
| `--gt-alt-2` | `#D1DA50` |
| `--gt-alt-3` | `#FED525` |
| `--gt-alt-4` | `#F18820` |
| `--gt-alt-5` | `#EB595B` |
| `--gt-alt-6` | `#9464A7` |
| `--gt-alt-7` | `#706DB0` |

### 1.4 Colores de Sistema (feedback semántico)

| Categoría | 400 (oscuro) | 300 (base) | 200 (medio) | 100 (fondo) |
|-----------|-------------|------------|-------------|-------------|
| **Blue** (info) | `#086CD9` | `#1D88FE` | `#8FC3FF` | `#EAF4FF` |
| **Green** (success) | `#11845B` | `#05C168` | `#7FDCA4` | `#DEF2E6` |
| **Red** (danger) | `#DC2B2B` | `#FF5A65` | `#FFBEC2` | `#FFEFF0` |
| **Yellow** (warning) | `#FFA800` | `#FDBD1A` | `#FFE39B` | `#FFF6E4` |

---

## 🔤 SECCIÓN 2: TIPOGRAFÍA CORPORATIVA (PP Mori)

### 2.1 Familia tipográfica

La tipografía corporativa es **PP Mori** en tres pesos:
- **ExtraLight** (peso 200) — textos decorativos, subtítulos ligeros
- **Regular** (peso 400) — cuerpo de texto, inputs, labels
- **SemiBold** (peso 600) — headings, botones, énfasis

### 2.2 Escala tipográfica

| Nivel | Peso | Tamaño / Interlineado |
|-------|------|-----------------------|
| H1 | SemiBold | 54px / 66px |
| H2 | SemiBold | 38px / 50px |
| H3 | SemiBold | 24px / 34px |
| H4 | SemiBold | 22px / 28px |
| H5 | SemiBold | 18px / 24px |
| H6 | SemiBold | 16px / 22px |
| Body Extra Large | SemiBold | 38px / 56px |
| Body Large | Regular | 24px / 38px |
| Body Default | Regular | 18px / 30px |
| Body Small | Regular | 14px / 24px |
| Text Single 400 | Regular | 24px / 26px |
| Text Single 300 | Regular | 20px / 22px |
| Text Single 200 | Regular | 18px / 20px |
| Text Single 100 | Regular | 16px / 18px |

### 2.3 Integración de la fuente

Los archivos de fuente PP Mori deben colocarse en `wwwroot/fonts/PPMori/` y declararse con `@font-face` en `site.css`.

---

## 🛠️ SECCIÓN 3: INSTRUCCIONES DE IMPLEMENTACIÓN

### PASO 1 — Registrar la fuente PP Mori

Crear la carpeta `wwwroot/fonts/PPMori/` con los archivos de fuente (`.woff2` preferido, `.woff` como fallback).

Añadir al **inicio** de `wwwroot/css/site.css`:

```css
/* ============================================================
   GreenTransit — PP Mori Font Face Declarations
   ============================================================ */
@font-face {
    font-family: 'PP Mori';
    src: url('/fonts/PPMori/PPMori-ExtraLight.woff2') format('woff2'),
         url('/fonts/PPMori/PPMori-ExtraLight.woff') format('woff');
    font-weight: 200;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: 'PP Mori';
    src: url('/fonts/PPMori/PPMori-Regular.woff2') format('woff2'),
         url('/fonts/PPMori/PPMori-Regular.woff') format('woff');
    font-weight: 400;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: 'PP Mori';
    src: url('/fonts/PPMori/PPMori-SemiBold.woff2') format('woff2'),
         url('/fonts/PPMori/PPMori-SemiBold.woff') format('woff');
    font-weight: 600;
    font-style: normal;
    font-display: swap;
}
```

> **IMPORTANTE**: Si los archivos de fuente tienen nombres diferentes, ajustar las rutas `src`. Si solo se dispone de `.ttf` u `.otf`, usar esos formatos con `format('truetype')` o `format('opentype')`.

---

### PASO 2 — Override de variables CSS de Radzen (Modo Claro)

Añadir en `wwwroot/css/site.css` **después** de las declaraciones `@font-face`, dentro de `:root`:

```css
/* ============================================================
   GreenTransit — Radzen Theme Override (Humanistic Light)
   ============================================================ */
:root {
    /* ── Tipografía global ── */
    --rz-body-font-family: 'PP Mori', 'Source Sans 3', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
    --rz-heading-font-family: 'PP Mori', 'Source Sans 3', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;

    /* ── Colores primarios de acento ── */
    --rz-primary: #0A404B;               /* Dark Petroleum — color principal */
    --rz-primary-light: #8ACCC3;          /* Secondary 1 — hover ligero */
    --rz-primary-lighter: #BEE2E4;        /* Alt 1 — fondo seleccionado */
    --rz-primary-dark: #083338;           /* Dark Petroleum oscurecido */
    --rz-primary-darker: #062028;         /* Dark Petroleum muy oscuro */
    --rz-on-primary: #FFFFFF;             /* Texto sobre primary */
    --rz-on-primary-lighter: #0A404B;     /* Texto sobre primary-lighter */

    /* ── Color secundario ── */
    --rz-secondary: #4F5E5A;             /* Stone Green */
    --rz-secondary-light: #6E7D78;
    --rz-secondary-lighter: #A0ABA7;
    --rz-secondary-dark: #3A4844;
    --rz-secondary-darker: #2A3532;
    --rz-on-secondary: #FFFFFF;
    --rz-on-secondary-lighter: #262626;

    /* ── Colores semánticos (sistema) ── */
    --rz-info: #1D88FE;                  /* Blue 300 */
    --rz-info-light: #8FC3FF;            /* Blue 200 */
    --rz-info-lighter: #EAF4FF;          /* Blue 100 */
    --rz-info-dark: #086CD9;             /* Blue 400 */
    --rz-on-info: #FFFFFF;
    --rz-on-info-lighter: #086CD9;

    --rz-success: #05C168;               /* Green 300 */
    --rz-success-light: #7FDCA4;         /* Green 200 */
    --rz-success-lighter: #DEF2E6;       /* Green 100 */
    --rz-success-dark: #11845B;          /* Green 400 */
    --rz-on-success: #FFFFFF;
    --rz-on-success-lighter: #11845B;

    --rz-danger: #FF5A65;                /* Red 300 */
    --rz-danger-light: #FFBEC2;          /* Red 200 */
    --rz-danger-lighter: #FFEFF0;        /* Red 100 */
    --rz-danger-dark: #DC2B2B;           /* Red 400 */
    --rz-on-danger: #FFFFFF;
    --rz-on-danger-lighter: #DC2B2B;

    --rz-warning: #FDBD1A;              /* Yellow 300 */
    --rz-warning-light: #FFE39B;         /* Yellow 200 */
    --rz-warning-lighter: #FFF6E4;       /* Yellow 100 */
    --rz-warning-dark: #FFA800;          /* Yellow 400 */
    --rz-on-warning: #262626;            /* Texto oscuro sobre amarillo */
    --rz-on-warning-lighter: #262626;

    /* ── Base y superficies ── */
    --rz-base: #4F5E5A;                 /* Stone Green como base */
    --rz-base-background-color: #FFFFFF;
    --rz-body-background-color: #F5F6F5; /* Gris verdoso muy claro */
    --rz-body-color: #262626;            /* Graphite Black */

    /* ── Texto ── */
    --rz-text-color: #262626;
    --rz-text-secondary-color: #4F5E5A;  /* Stone Green */
    --rz-text-tertiary-color: #6E7D78;
    --rz-text-disabled-color: #A0ABA7;
    --rz-text-title-color: #17233C;      /* Dark Blue */
    --rz-text-h1-color: #17233C;
    --rz-text-h2-color: #17233C;
    --rz-text-h3-color: #0A404B;         /* Dark Petroleum */
    --rz-text-h4-color: #0A404B;
    --rz-text-h5-color: #262626;
    --rz-text-h6-color: #262626;

    /* ── Links ── */
    --rz-link-color: #0A404B;
    --rz-link-hover-color: #083338;

    /* ── Bordes ── */
    --rz-border-color: #D1D5D4;
    --rz-input-border-color: #B0B8B5;
    --rz-input-hover-border-color: #0A404B;
    --rz-input-focus-border-color: #0A404B;
    --rz-input-focus-shadow: 0 0 0 3px rgba(10, 64, 75, 0.15);

    /* ── Sidebar / Navegación ── */
    --rz-sidebar-background-color: #17233C;       /* Dark Blue */
    --rz-sidebar-color: rgba(255, 255, 255, 0.85);
    --rz-sidebar-border-inline-end: 1px solid rgba(255, 255, 255, 0.08);

    /* ── Header ── */
    --rz-header-background-color: #FFFFFF;
    --rz-header-color: #262626;

    /* ── Paleta de gráficos (chart series) ── */
    --rz-series-1: #0A404B;              /* Dark Petroleum */
    --rz-series-2: #8ACCC3;              /* Secondary 1 */
    --rz-series-3: #D8B00E;              /* Secondary 3 */
    --rz-series-4: #D36F15;              /* Secondary 4 */
    --rz-series-5: #C13E43;              /* Secondary 5 */
    --rz-series-6: #6E4583;              /* Secondary 6 */
    --rz-series-7: #535497;              /* Secondary 7 */
    --rz-series-8: #B4B736;              /* Secondary 2 */
}
```

---

### PASO 3 — Override de variables CSS de Radzen (Modo Oscuro)

Buscar en el proyecto cómo se gestiona el modo oscuro. Si se usa el tema `humanistic-dark.css`, crear un bloque equivalente con selector adecuado. Si se usa un atributo `data-theme="dark"` o una clase `.rz-dark`, aplicar el override así:

```css
/* ============================================================
   GreenTransit — Radzen Theme Override (Humanistic Dark)
   ============================================================ */
/* NOTA: Ajustar el selector según cómo active el modo oscuro la app.
   Opciones comunes:
   - html[data-theme="dark"]
   - body.rz-dark
   - Si carga humanistic-dark.css separado, este bloque va en un archivo aparte
*/
html[data-theme="dark"],
.rz-dark {
    --rz-body-font-family: 'PP Mori', 'Source Sans 3', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
    --rz-heading-font-family: 'PP Mori', 'Source Sans 3', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;

    --rz-primary: #8ACCC3;               /* Teal claro como primario en dark */
    --rz-primary-light: #BEE2E4;
    --rz-primary-lighter: rgba(138, 204, 195, 0.15);
    --rz-primary-dark: #6EAFA7;
    --rz-primary-darker: #4F9189;
    --rz-on-primary: #17233C;
    --rz-on-primary-lighter: #BEE2E4;

    --rz-secondary: #A0ABA7;
    --rz-on-secondary: #17233C;

    --rz-base-background-color: #1A1F2E;   /* Fondo base oscuro con tinte azul */
    --rz-body-background-color: #12162A;
    --rz-body-color: #E8EAEA;

    --rz-text-color: #E8EAEA;
    --rz-text-secondary-color: #A0ABA7;
    --rz-text-tertiary-color: #6E7D78;
    --rz-text-title-color: #BEE2E4;
    --rz-text-h1-color: #BEE2E4;
    --rz-text-h2-color: #BEE2E4;
    --rz-text-h3-color: #8ACCC3;
    --rz-text-h4-color: #8ACCC3;

    --rz-link-color: #8ACCC3;
    --rz-link-hover-color: #BEE2E4;

    --rz-border-color: #2E3647;
    --rz-input-border-color: #3A4558;
    --rz-input-hover-border-color: #8ACCC3;
    --rz-input-focus-border-color: #8ACCC3;
    --rz-input-focus-shadow: 0 0 0 3px rgba(138, 204, 195, 0.2);

    --rz-sidebar-background-color: #0F1320;
    --rz-sidebar-color: rgba(255, 255, 255, 0.8);

    --rz-header-background-color: #1A1F2E;
    --rz-header-color: #E8EAEA;

    /* Info / Success / Danger / Warning: mismos colores de sistema */
    --rz-info: #1D88FE;
    --rz-success: #05C168;
    --rz-danger: #FF5A65;
    --rz-warning: #FDBD1A;

    /* Chart series en dark */
    --rz-series-1: #8ACCC3;
    --rz-series-2: #BEE2E4;
    --rz-series-3: #FED525;
    --rz-series-4: #F18820;
    --rz-series-5: #EB595B;
    --rz-series-6: #9464A7;
    --rz-series-7: #706DB0;
    --rz-series-8: #D1DA50;
}
```

---

### PASO 4 — Overrides específicos por componente

Añadir en `site.css` reglas CSS adicionales para componentes que no se controlan solo con variables:

```css
/* ============================================================
   GreenTransit — Component-Level Overrides
   ============================================================ */

/* ── Tipografía: forzar PP Mori en todos los componentes Radzen ── */
.rz-body,
.rz-sidebar,
.rz-header,
.rz-dialog,
.rz-panel,
.rz-datatable,
.rz-grid,
.rz-dropdown,
.rz-textbox,
.rz-textarea,
.rz-button,
.rz-badge,
.rz-card,
.rz-tabs,
.rz-autocomplete,
.rz-scheduler,
.rz-notification,
[class*="rz-"] {
    font-family: 'PP Mori', 'Source Sans 3', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
}

/* ── Headings de Radzen ── */
.rz-text-h1 { font-weight: 600; }
.rz-text-h2 { font-weight: 600; }
.rz-text-h3 { font-weight: 600; }
.rz-text-h4 { font-weight: 600; }
.rz-text-h5 { font-weight: 600; }
.rz-text-h6 { font-weight: 600; }
.rz-text-subtitle1,
.rz-text-subtitle2 { font-weight: 600; }
.rz-text-body1,
.rz-text-body2 { font-weight: 400; }
.rz-text-caption,
.rz-text-overline { font-weight: 200; }

/* ── Sidebar: ítems de menú ── */
.rz-sidebar .rz-navigation-item-text {
    font-family: 'PP Mori', sans-serif;
    font-weight: 400;
    font-size: 14px;
}

.rz-sidebar .rz-navigation-item-icon {
    color: rgba(255, 255, 255, 0.65);
}

.rz-sidebar .rz-navigation-item:hover {
    background-color: rgba(138, 204, 195, 0.12);  /* Teal semi-transparente */
}

.rz-sidebar .rz-navigation-item.rz-navigation-item-active,
.rz-sidebar .rz-navigation-item-wrapper-active {
    background-color: rgba(138, 204, 195, 0.18);
    border-left: 3px solid #8ACCC3;
}

.rz-sidebar .rz-navigation-item-active .rz-navigation-item-text,
.rz-sidebar .rz-navigation-item-active .rz-navigation-item-icon {
    color: #FFFFFF;
}

/* ── Botones: ajuste fino ── */
.rz-button.rz-primary {
    font-weight: 600;
    letter-spacing: 0.02em;
}

.rz-button.rz-secondary {
    font-weight: 400;
}

/* ── DataGrid: cabeceras y filas ── */
.rz-grid-table thead th,
.rz-datatable-thead th {
    font-weight: 600;
    font-size: 13px;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: #4F5E5A;  /* Stone Green */
}

.rz-grid-table tbody td,
.rz-datatable-tbody td {
    font-weight: 400;
    font-size: 14px;
}

/* Fila seleccionada en grid */
.rz-datatable-tbody > tr.rz-state-highlight,
.rz-grid-table tbody > tr.rz-data-row.rz-state-highlight {
    background-color: #BEE2E4 !important;  /* Alt 1 */
    color: #262626;
}

/* ── Cards ── */
.rz-card {
    border-color: #D1D5D4;
    border-radius: 8px;
}

/* ── Badges (estados de ServiceOrder, WasteMove, etc.) ── */
.rz-badge.rz-badge-pill {
    font-weight: 600;
    font-size: 12px;
    letter-spacing: 0.03em;
}

/* ── Tabs ── */
.rz-tabview-nav li.rz-tabview-selected a {
    color: #0A404B;
    border-bottom-color: #0A404B;
    font-weight: 600;
}

/* ── Notificaciones ── */
.rz-notification {
    font-family: 'PP Mori', sans-serif;
    border-radius: 8px;
}

/* ── Diálogos ── */
.rz-dialog-titlebar {
    font-weight: 600;
    color: #17233C;
}

/* ── Scheduler ── */
.rz-scheduler {
    font-family: 'PP Mori', sans-serif;
}

/* ── Fieldset / Panel ── */
.rz-fieldset-legend,
.rz-panel-titlebar {
    font-weight: 600;
    color: #17233C;
}

/* ── Tooltip ── */
.rz-tooltip {
    font-family: 'PP Mori', sans-serif;
    font-size: 13px;
}
```

---

### PASO 5 — Overrides en NavMenu.razor (sidebar colapsable del proyecto)

El componente `NavMenu.razor` ya implementa un sidebar colapsable con grupos y chevrones. Revisar si usa clases CSS propias (archivo `NavMenu.razor.css`) y asegurar que:

1. Los colores de texto, iconos y fondos de hover usen las variables `--gt-*` o directamente los hex corporativos.
2. El chevron de collapse/expand use `color: rgba(255,255,255,0.5)` en estado normal y `color: #8ACCC3` en hover.
3. Los group headers del menú usen `font-weight: 600`, `font-size: 11px`, `text-transform: uppercase`, `letter-spacing: 0.08em`, `color: rgba(255,255,255,0.45)`.

Si `NavMenu.razor.css` existe, actualizar los colores hardcodeados. Si usa clases genéricas Radzen, los overrides del Paso 4 deberían cubrirlo.

---

### PASO 6 — Overrides en MainLayout.razor y TopBar

Revisar `MainLayout.razor` y su archivo `.razor.css` asociado:

1. **Topbar**: fondo `#FFFFFF` (modo claro) / `#1A1F2E` (dark), texto `#262626` / `#E8EAEA`.
2. **Selector de OwnerId**: mismo estilo que dropdowns corporativos.
3. **Buscador global** (`GlobalSearchBar.razor`): placeholder en `color: #A0ABA7`, borde focus en `#0A404B`.
4. **Badge de notificaciones**: usar `--rz-danger` (`#FF5A65`) para el contador.
5. **Botón de toggle sidebar**: icono en `#4F5E5A`, hover en `#0A404B`.

---

### PASO 7 — Badges de estado (ServiceOrderStatuses, WasteMoveStatuses, etc.)

El proyecto usa badges con colores por estado. Actualizar las clases CSS (o estilos inline) que definen los colores de cada estado para usar la paleta corporativa:

| Estado | Background | Text Color | Basado en |
|--------|-----------|------------|-----------|
| Pending / Borrador | `#FFF6E4` | `#FFA800` | Yellow 100/400 |
| Scheduled / Planificado | `#EAF4FF` | `#086CD9` | Blue 100/400 |
| InProgress / En curso | `#BEE2E4` | `#0A404B` | Alt 1 / Dark Petroleum |
| Completed / Completado | `#DEF2E6` | `#11845B` | Green 100/400 |
| Cancelled / Anulado | `#FFEFF0` | `#DC2B2B` | Red 100/400 |
| Emitido | `#EAF4FF` | `#086CD9` | Blue 100/400 |
| Validado | `#DEF2E6` | `#11845B` | Green 100/400 |
| Rechazado | `#FFEFF0` | `#DC2B2B` | Red 100/400 |

---

### PASO 8 — Charts (ApexCharts / Chart.js)

El proyecto usa ApexCharts para dashboards. Asegurar que:

1. La paleta de colores por defecto de las series sea: `['#0A404B', '#8ACCC3', '#D8B00E', '#D36F15', '#C13E43', '#6E4583', '#535497', '#B4B736']`.
2. Si hay un archivo de configuración global de charts (ej: `ChartDefaults.cs` o un JS de configuración), actualizar la paleta ahí.
3. Los tooltips de gráficos usen `font-family: 'PP Mori'`.

---

### PASO 9 — CSS personalizado existente (app.css, site.css o componentes .razor.css)

Buscar en todo el proyecto archivos `.css` y `.razor.css` que contengan colores hardcodeados y reemplazarlos:

```
Buscar y reemplazar (regex sugerida):
- Colores del tema Humanistic por defecto que no sean los corporativos
- Cualquier referencia a 'Source Sans', 'Source Sans Pro', 'Source Sans 3' → añadir 'PP Mori' antes
```

**Comando para encontrar colores hardcodeados**:
```bash
grep -rn --include="*.css" --include="*.razor" --include="*.razor.css" "#[0-9a-fA-F]\{3,8\}" src/GreenTransit.Web/
```

---

### PASO 10 — Verificación visual

Tras aplicar todos los cambios, verificar visualmente las siguientes pantallas/componentes:

#### Checklist de verificación

- [ ] **Login redirect page**: si hay splash o página de carga, usa colores corporativos
- [ ] **Sidebar**: fondo Dark Blue, texto blanco, hover teal, ítem activo con borde teal
- [ ] **Topbar / Header**: fondo blanco, texto Graphite Black, buscador con focus Dark Petroleum
- [ ] **Dashboard Home** (`/`): cards KPI, gráficos, mapa — todos con paleta corporativa
- [ ] **DataGrid de Entidades** (`/entities`): cabeceras, filas alternas, fila seleccionada, paginación
- [ ] **Formulario de Service Orders** (`/service-orders/new`): labels, inputs, dropdowns, botones
- [ ] **Grid de WasteMoves** (`/waste-moves`): badges de estado, iconos de acción
- [ ] **Diálogos**: fondo, título, botones primario/secundario
- [ ] **Tabs**: pestaña activa con color Dark Petroleum
- [ ] **Notificaciones toast**: info/success/warning/danger con colores del sistema
- [ ] **Charts del dashboard de logística** (`/logistics/optimization`): series con paleta corporativa
- [ ] **Modo oscuro**: toggle dark mode y verificar todos los anteriores
- [ ] **Responsive mobile**: sidebar colapsada, formularios apilados — fuentes legibles
- [ ] **EcoDataNet** (`/ecodatanet/publish`): botón primario, barra de progreso
- [ ] **Page Permissions** (`/security/page-permissions`): grid con badges

---

## ⚠️ NOTAS IMPORTANTES

1. **Orden de carga CSS**: `site.css` DEBE cargarse DESPUÉS de `_content/Radzen.Blazor/css/humanistic.css` para que los overrides tengan efecto. Verificar en `App.razor` o `_Host.cshtml` o `_Layout.cshtml`.

2. **No modificar los archivos de Radzen directamente** (`_content/Radzen.Blazor/css/*`). Todos los cambios van en `site.css` o en archivos `.razor.css` del proyecto.

3. **PP Mori es una fuente comercial**: asegurar que se tienen las licencias adecuadas para uso web. Si la fuente no está disponible en formato web, usar el fallback `'Source Sans 3'` que ya incluye Radzen, pero cambiar los pesos a 200/400/600.

4. **Compatibilidad**: las variables CSS de Radzen Blazor están disponibles desde v4+. Si el proyecto usa una versión anterior de Radzen.Blazor, verificar la disponibilidad de las variables consultando el changelog en https://blazor.radzen.com/changelog.

5. **Especificidad CSS**: si algún override no surte efecto, puede ser necesario aumentar la especificidad del selector (ej: `.rz-body .rz-grid-table thead th` en lugar de solo `.rz-grid-table thead th`). Evitar `!important` excepto donde sea estrictamente necesario.

6. **Variables `--gt-*` propias**: las variables con prefijo `--gt-` son tokens internos del proyecto para reutilizar en componentes custom. Las variables con prefijo `--rz-` son las que Radzen lee para aplicar el tema.

---

## 📁 RESUMEN DE ARCHIVOS A CREAR/MODIFICAR

| Archivo | Acción |
|---------|--------|
| `wwwroot/fonts/PPMori/*.woff2` | **CREAR** — Archivos de fuente |
| `wwwroot/css/site.css` | **MODIFICAR** — Añadir @font-face + :root overrides + component overrides |
| `App.razor` o `_Host.cshtml` | **VERIFICAR** — Orden de carga de CSS (humanistic.css antes de site.css) |
| `NavMenu.razor.css` | **MODIFICAR** — Colores del sidebar si tiene estilos propios |
| `MainLayout.razor.css` | **MODIFICAR** — Colores del header/topbar si tiene estilos propios |
| `GlobalSearchBar.razor.css` | **MODIFICAR** — Colores del buscador si tiene estilos propios |
| Archivos `*.razor.css` con colores hardcodeados | **MODIFICAR** — Reemplazar por colores corporativos |
| Configuración global de ApexCharts/Chart.js | **MODIFICAR** — Paleta de colores de series |

---

## 🔄 ESTRATEGIA DE EJECUCIÓN EN COPILOT

Dado el tamaño del cambio, ejecutar en **4 fases**:

### Fase 1: Fuentes y variables globales
- Paso 1 (@font-face)
- Paso 2 (variables :root modo claro)
- Paso 3 (variables modo oscuro)

### Fase 2: Overrides de componentes
- Paso 4 (reglas CSS por componente)
- Paso 5 (NavMenu.razor)
- Paso 6 (MainLayout/TopBar)

### Fase 3: Elementos de dominio
- Paso 7 (badges de estado)
- Paso 8 (charts)
- Paso 9 (búsqueda y reemplazo de colores hardcodeados)

### Fase 4: Verificación
- Paso 10 (checklist visual)

> Al inicio de cada sesión de Copilot, adjuntar este archivo + `COPILOT_CONTEXT.md` + `README.md`.
> Al final de cada fase, guardar el estado en `COPILOT_CONTEXT.md`.
