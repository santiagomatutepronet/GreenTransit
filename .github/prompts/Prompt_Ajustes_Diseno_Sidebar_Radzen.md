# Prompt para GitHub Copilot: Ajustes de Diseño del Sidebar/NavBar Radzen

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `MainLayout.razor`, `NavMenu.razor`, `NavMenu.razor.css` (o `site.css` donde estén los estilos custom del sidebar) y este archivo.

---

## Contexto

La aplicación **GreenTransit** usa **Radzen Blazor Components** con tema **Humanistic** y layout basado en `RadzenLayout` + `RadzenSidebar` + `RadzenPanelMenu`. El sidebar ya está funcional y migrado a Radzen, pero necesita ajustes visuales para alinearse con la identidad corporativa definida en las CSS custom variables `--gt-*`.

La paleta corporativa relevante para el sidebar es:

| Variable CSS | Hex | Uso |
|---|---|---|
| `--gt-dark-blue` | `#17233C` | Fondo del sidebar |
| `--gt-dark-petroleum` | `#0A404B` | Color primario de acento (ítem activo) |
| `--gt-secondary-1` | `#8ACCC3` | Teal — borde de acento, hover sutil |
| `--gt-stone-green` | `#4F5E5A` | Textos secundarios, labels de sección |
| `--gt-graphite-black` | `#262626` | Texto principal (en fondos claros) |

---

## Cambios solicitados

### 1. Ítem activo / seleccionado (`RadzenPanelMenuItem` con `Path` = ruta actual)

**Problema actual**: El ítem seleccionado usa un fondo verde sólido muy saturado que no pertenece a la paleta corporativa y rompe visualmente.

**Solución**: Reemplazar por un estilo sutil y corporativo:

```css
/* Ítem activo del sidebar */
.rz-sidebar .rz-panel-menu .rz-navigation-item-active,
.rz-sidebar .rz-panel-menu .rz-navigation-item-active > .rz-navigation-item-link {
    background-color: rgba(10, 64, 75, 0.25); /* --gt-dark-petroleum con transparencia */
    border-left: 3px solid var(--gt-secondary-1, #8ACCC3); /* borde teal como indicador */
    color: #FFFFFF;
    font-weight: 600;
}

/* Asegurar que el texto del ítem activo sea blanco */
.rz-sidebar .rz-panel-menu .rz-navigation-item-active .rz-navigation-item-text {
    color: #FFFFFF;
}

/* Icono del ítem activo en teal */
.rz-sidebar .rz-panel-menu .rz-navigation-item-active .rz-navigation-item-icon {
    color: var(--gt-secondary-1, #8ACCC3);
}
```

---

### 2. Labels de sección / grupos padre (Configuración, Operaciones, Economía, etc.)

**Problema actual**: Los títulos de grupo padre tienen el mismo peso visual que los items hijos, lo que dificulta escanear la estructura del menú.

**Solución**: Diferenciar los encabezados de grupo con uppercase, menor tamaño y color atenuado:

```css
/* Grupos padre del PanelMenu — solo los de primer nivel */
.rz-sidebar .rz-panel-menu > .rz-navigation-item > .rz-navigation-item-link {
    text-transform: uppercase;
    font-size: 0.7rem;
    font-weight: 700;
    letter-spacing: 0.05em;
    color: rgba(255, 255, 255, 0.5);
    padding: 0.75rem 1rem 0.4rem 1rem;
}

/* Al expandir, el grupo padre se resalta levemente */
.rz-sidebar .rz-panel-menu > .rz-navigation-item.rz-expanded > .rz-navigation-item-link {
    color: rgba(255, 255, 255, 0.7);
}

/* Items hijos mantienen tamaño y peso normal */
.rz-sidebar .rz-panel-menu .rz-navigation-item .rz-navigation-item .rz-navigation-item-link {
    font-size: 0.85rem;
    font-weight: 400;
    text-transform: none;
    letter-spacing: normal;
    color: rgba(255, 255, 255, 0.8);
    padding: 0.5rem 1rem 0.5rem 2.5rem; /* indentación para hijos */
}
```

---

### 3. Hover states

**Problema actual**: El hover sobre los items es poco diferenciado del estado normal.

**Solución**:

```css
/* Hover en items hijos */
.rz-sidebar .rz-panel-menu .rz-navigation-item-link:hover {
    background-color: rgba(138, 204, 195, 0.1); /* teal sutil */
    color: #FFFFFF;
    transition: background-color 0.2s ease;
}

/* Hover en items hijos — icono */
.rz-sidebar .rz-panel-menu .rz-navigation-item-link:hover .rz-navigation-item-icon {
    color: var(--gt-secondary-1, #8ACCC3);
    transition: color 0.2s ease;
}
```

---

### 4. Chevrons (expand/collapse)

**Problema actual**: Los chevrones de expandir/colapsar grupos son poco visibles y no tienen transición suave.

**Solución**:

```css
/* Chevron más tenue que el texto */
.rz-sidebar .rz-panel-menu .rz-navigation-item-icon-children {
    color: rgba(255, 255, 255, 0.35);
    transition: transform 0.25s ease, color 0.2s ease;
    font-size: 0.75rem;
}

/* Chevron más visible al hover */
.rz-sidebar .rz-panel-menu .rz-navigation-item-link:hover .rz-navigation-item-icon-children {
    color: rgba(255, 255, 255, 0.6);
}
```

---

### 5. Separadores entre grupos

**Problema actual**: No hay separación visual entre los bloques de secciones (Configuración, Operaciones, etc.), lo que hace difícil distinguir dónde empieza cada grupo.

**Solución**: Añadir una línea sutil entre los grupos de primer nivel:

```css
/* Separador entre grupos principales */
.rz-sidebar .rz-panel-menu > .rz-navigation-item + .rz-navigation-item {
    border-top: 1px solid rgba(255, 255, 255, 0.08);
    margin-top: 0.25rem;
    padding-top: 0.25rem;
}
```

---

### 6. Fondo del sidebar

**Verificar** que el fondo del sidebar use `--gt-dark-blue`:

```css
.rz-sidebar {
    background-color: var(--gt-dark-blue, #17233C);
}
```

---

### 7. Scrollbar del sidebar (opcional pero recomendado)

Para mantener la estética oscura, personalizar la scrollbar del sidebar:

```css
.rz-sidebar::-webkit-scrollbar {
    width: 6px;
}

.rz-sidebar::-webkit-scrollbar-track {
    background: transparent;
}

.rz-sidebar::-webkit-scrollbar-thumb {
    background-color: rgba(255, 255, 255, 0.15);
    border-radius: 3px;
}

.rz-sidebar::-webkit-scrollbar-thumb:hover {
    background-color: rgba(255, 255, 255, 0.25);
}
```

---

## Instrucciones de implementación

1. **Ubicación del CSS**: Añade todos estos estilos en `wwwroot/css/site.css` DESPUÉS de la carga del tema Radzen (`humanistic.css`), para que los overrides tengan prioridad.
2. **No modificar** `humanistic.css` directamente — siempre trabajar con overrides en `site.css`.
3. **Inspeccionar selectores**: Los selectores de arriba son orientativos para Radzen Blazor. Usa las DevTools del navegador para verificar los selectores exactos que genera tu versión de Radzen y ajústalos si es necesario. Los nombres de clase pueden variar ligeramente entre versiones (ej: `.rz-navigation-item-active` vs `.rz-state-active`).
4. **Verificar accesibilidad**: El contraste entre texto (blanco/rgba) y fondo (`#17233C`) debe cumplir ratio AA (≥ 4.5:1 para texto normal). Los colores propuestos cumplen este requisito.

---

## Resultado esperado

- Sidebar con fondo `#17233C` (Dark Blue corporativo)
- Grupos padre (Configuración, Operaciones…) en uppercase, pequeños, color atenuado — actúan como labels de sección
- Items hijos con tamaño normal, indentados, con hover teal sutil
- Ítem activo con borde izquierdo teal (`#8ACCC3`) y fondo semi-transparente petroleum
- Separadores sutiles entre grupos
- Chevrons discretos con transición suave
- Scrollbar personalizada para mantener la estética oscura
