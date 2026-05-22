# Prompt para GitHub Copilot: Migración completa de gráficos ApexCharts/Chart.js → Radzen Blazor Charts

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat. Adjunta siempre: `COPILOT_CONTEXT.md`, `README.md`, `Crear_BD_v4_1.sql`. Si estás en una sesión con el MCP de Radzen Blazor configurado, Copilot DEBE usar ese MCP para confirmar nombres de componentes, propiedades, eventos y ejemplos actuales. En caso de discrepancia entre la memoria de entrenamiento y el MCP, prevalece el MCP.

---

## Contexto del proyecto

Estoy desarrollando **GreenTransit**, una aplicación **Blazor Web App (.NET 10, Server)** con:

- **Arquitectura**: Clean Architecture (Domain, Application, Infrastructure, Web, Tests), EF Core, MediatR (CQRS), FluentValidation.
- **UI Framework actual**: Radzen Blazor Components (tema `humanistic`), ya instalado y configurado (`AddRadzenComponents()` en `Program.cs`, usings en `_Imports.razor`, JS y CSS en `App.razor`).
- **Gráficos actuales**: **ApexCharts** (paquete NuGet `Blazor-ApexCharts` y/o referencias JS directas a `apexcharts`) y posiblemente **Chart.js** (vía JS interop con `IJSRuntime`). Se usan en dashboards de KPIs, reporting, huella de carbono, cumplimiento normativo, tratamiento/reciclaje, mapas de calor, optimización logística y movilidad urbana.
- **Paleta corporativa de series de gráficos** (obligatoria, definida en el rebranding): `['#0A404B', '#8ACCC3', '#D8B00E', '#D36F15', '#C13E43', '#6E4583', '#535497', '#B4B736']`.
- **Fuente de tooltips/labels**: `'PP Mori', 'Source Sans 3', sans-serif`.
- **Modo oscuro/claro**: implementado, los gráficos deben respetar ambos modos.
- **Multi-tenant**: todas las queries filtran por `OwnerId`.
- **Datos geográficos**: siempre como nombre (`Province.Name`, `Municipality.Name`), nunca como código.

---

## Objetivo

Reemplazar **TODOS** los gráficos basados en ApexCharts y/o Chart.js por componentes nativos de **Radzen Blazor Charts** (`RadzenChart` + series `RadzenLineSeries`, `RadzenAreaSeries`, `RadzenBarSeries`, `RadzenColumnSeries`, `RadzenPieSeries`, `RadzenDonutSeries`, etc.).

> **IMPORTANTE**: Consulta el MCP de Radzen Blazor para confirmar los nombres exactos de componentes, propiedades y eventos antes de escribir código. No te fíes de la memoria de entrenamiento si difiere del MCP.

---

## 1. Definición de Hecho (DoD)

La migración se considera completa **solo** cuando se cumplen TODOS estos criterios:

- [ ] **Cero referencias a ApexCharts**: no queda ningún `using`, `@using`, `<ApexChart`, `ApexChartOptions`, ni referencia al paquete NuGet `Blazor-ApexCharts` en ningún `.csproj`.
- [ ] **Cero referencias a Chart.js**: no queda ningún `<script>` de `chart.js`/`chart.min.js` en layouts ni `wwwroot`, ningún `IJSRuntime.InvokeAsync` que invoque funciones de Chart.js, ningún archivo `.js` de interop para Chart.js.
- [ ] **Cero assets huérfanos**: no quedan archivos `.js`, `.css` ni paquetes NPM de ApexCharts o Chart.js en `wwwroot`, `node_modules`, `package.json` ni `libman.json`.
- [ ] **Compila sin errores**: `dotnet build` de toda la solución pasa limpio (0 warnings relacionados con charts).
- [ ] **Tests pasan**: si existen tests unitarios o de integración que involucren dashboards/gráficos, todos pasan.
- [ ] **Comportamiento funcional preservado**: cada gráfico muestra los mismos datos, con los mismos filtros y la misma lógica de negocio (queries CQRS) que antes. No se altera la capa Application ni Domain.
- [ ] **Paleta corporativa aplicada**: todas las series usan la paleta `['#0A404B', '#8ACCC3', '#D8B00E', '#D36F15', '#C13E43', '#6E4583', '#535497', '#B4B736']`.
- [ ] **Responsive y dark mode**: los gráficos Radzen se adaptan al contenedor y respetan el modo oscuro/claro.
- [ ] **Documentación actualizada**: `README.md` y/o `COPILOT_CONTEXT.md` mencionan Radzen Charts como estándar, sin referencia a ApexCharts/Chart.js.

---

## 2. Inventario automático

**Antes de modificar NADA**, realiza un inventario exhaustivo. Ejecuta estas búsquedas en toda la solución y produce una tabla con los resultados.**

### 2.1 Búsquedas obligatorias

Ejecuta cada una de estas búsquedas (grep, Find in Files o equivalente) y anota todos los hits:

```
Búsquedas para ApexCharts:
- "ApexCharts"                    (usings, @using, namespaces)
- "ApexChart"                     (componentes <ApexChart>, ApexChartOptions, etc.)
- "Blazor-ApexCharts"             (referencias en .csproj)
- "apexcharts"                    (scripts en layouts, wwwroot, package.json, libman.json)
- "ApexPointSeries"               (series de ApexCharts)
- "ApexPointAnnotation"           (anotaciones)
- "SeriesType."                   (enums de tipo de serie ApexCharts)

Búsquedas para Chart.js:
- "chart.js"                      (scripts, imports)
- "chart.min.js"                  (scripts minificados)
- "new Chart("                    (instanciación JS)
- "Chart("                        (instanciación abreviada)
- "chartjs"                       (variantes)

Búsquedas de JS Interop para charts:
- "IJSRuntime"                    (filtrar los que invocan funciones de chart)
- "InvokeAsync"                   (filtrar: "apex", "chart", "render", "update", "destroy" + chart)
- "InvokeVoidAsync"               (idem)
- "JSRuntime"                     (idem)

Búsquedas de archivos/assets:
- Archivos .js en wwwroot/ que contengan "chart", "apex", "graph"
- Archivos .css en wwwroot/ que contengan "apex", "chart"
- package.json o libman.json con dependencias de chart

Búsquedas de componentes Razor con gráficos:
- Archivos .razor que contengan "<ApexChart" o "Chart" + contexto de gráfico
- Archivos .razor.cs (code-behind) con ApexChartOptions o similar
- Archivos en carpetas */Charts/*, */Dashboards/*, */Reporting/*, */Shared/*Chart*
```

### 2.2 Tabla de inventario (generar antes de continuar)

Produce una tabla con este formato EXACTO para cada gráfico encontrado:

```
| # | Archivo (.razor o .js)         | Tipo de gráfico (Line/Bar/Pie/...) | Fuente de datos (DTO/Query/método) | Funcionalidad / Dashboard                     | Dependencia (ApexCharts/Chart.js) |
|---|-------------------------------|------------------------------------|------------------------------------|-----------------------------------------------|-----------------------------------|
| 1 | KpiDashboard.razor            | Column + Line (combo)              | GetKpiDataQuery → KpiDto           | KPIs regulatorios (§5.2)                      | ApexCharts                        |
| 2 | EmissionsTrendChart.razor      | Area                               | GetEmissionsTrendQuery → EmDto     | Huella de Carbono HC-B (§5.7)                 | ApexCharts                        |
| ...                                                                                                                                                                                 |
```

**No omitas ningún gráfico.** Si hay archivos de configuración global de charts (ej: `ChartDefaults.cs`, `chartInterop.js`, `ChartOptions.cs`), inclúyelos como filas separadas con tipo "Configuración".

> Si falta contexto para completar la tabla, pide el archivo concreto (ej: "Necesito ver el archivo `KpiDashboard.razor` para identificar el tipo de gráfico"). No hagas preguntas abiertas.

---

## 3. Estrategia de migración

### 3.1 Componente wrapper centralizado

Crea un componente wrapper reutilizable `AppChart.razor` en `Web/Components/Shared/Charts/` que encapsule la configuración común de todos los gráficos Radzen:

```razor
@* AppChart.razor — Wrapper centralizado para RadzenChart *@
@* Centraliza: paleta, tooltips, leyenda, responsive, ejes, formato de fechas/números *@
```

El wrapper debe:

- Aceptar como `RenderFragment` el contenido de series (para que cada dashboard defina sus series específicas).
- Aplicar la **paleta corporativa** por defecto: `#0A404B`, `#8ACCC3`, `#D8B00E`, `#D36F15`, `#C13E43`, `#6E4583`, `#535497`, `#B4B736`.
- Configurar `RadzenChart` con:
  - Leyenda visible y posición consistente.
  - Tooltips con fuente `'PP Mori', 'Source Sans 3', sans-serif`.
  - Ejes con formato español: fechas `dd/MM/yyyy`, números con separador de miles `.` y decimal `,`.
  - Responsive: el chart debe adaptarse al ancho del contenedor padre.
- Respetar modo oscuro/claro (colores de ejes, labels, fondo del tooltip).
- Exponer propiedades opcionales: `ShowLegend`, `Height`, `Title`, `CategoryAxisTitle`, `ValueAxisTitle`.

> Consulta el MCP de Radzen Blazor para confirmar la API exacta de `RadzenChart`, `RadzenLegend`, `RadzenCategoryAxis`, `RadzenValueAxis`, `RadzenChartTooltipOptions` (o equivalente). NO asumas nombres de propiedades.

### 3.2 Clase de constantes de paleta

Crea `Web/Components/Shared/Charts/ChartPalette.cs`:

```csharp
public static class ChartPalette
{
    public static readonly string[] CorporateColors = new[]
    {
        "#0A404B", "#8ACCC3", "#D8B00E", "#D36F15",
        "#C13E43", "#6E4583", "#535497", "#B4B736"
    };

    public static string GetColor(int seriesIndex)
        => CorporateColors[seriesIndex % CorporateColors.Length];
}
```

### 3.3 Mapeo tipo-a-tipo

Aplica este mapeo para cada gráfico del inventario:

| Tipo ApexCharts / Chart.js         | Componente Radzen equivalente          | Notas                                                              |
|------------------------------------|----------------------------------------|--------------------------------------------------------------------|
| Line                               | `RadzenLineSeries<T>`                  | Directo. `Data`, `ValueProperty`, `CategoryProperty`.              |
| Area                               | `RadzenAreaSeries<T>`                  | Directo. Soporte de stacked si se usaba.                           |
| Bar (horizontal)                   | `RadzenBarSeries<T>`                   | Directo. Eje X = valor, eje Y = categoría.                        |
| Column (vertical)                  | `RadzenColumnSeries<T>`               | Directo. Eje X = categoría, eje Y = valor.                        |
| Pie                                | `RadzenPieSeries<T>`                   | Directo. `Data`, `Value`, `Title` (o equivalente del MCP).        |
| Donut                              | `RadzenDonutSeries<T>`                | Directo.                                                           |
| Stacked Bar/Column                 | `RadzenColumnSeries<T>` o `RadzenBarSeries<T>` | Múltiples series con configuración de stack (consultar MCP). |
| Mixed/Combo (ej: Column + Line)    | Múltiples series dentro de un `RadzenChart` | Un `RadzenColumnSeries` + un `RadzenLineSeries` en el mismo chart. |
| Gauge / RadialBar                  | `RadzenArcGauge` o `RadzenRadialGauge` | Consultar MCP para componentes gauge disponibles. Si no hay equivalente exacto, usar `RadzenArcGauge` + CSS. |
| Treemap                            | No hay equivalente nativo              | Simplificar a `RadzenColumnSeries` agrupado o tabla con ProgressBar. Documentar la simplificación. |
| Heatmap                            | No hay equivalente nativo              | Mantener como tabla coloreada con CSS o usar grid con celdas con fondo calculado. Documentar. |
| Sparkline                          | `RadzenSparkline`                      | Confirmar existencia en MCP. Si no existe, usar `RadzenLineSeries` en un `RadzenChart` minimalista sin ejes ni leyenda. |
| Timeline horizontal                | No hay equivalente nativo              | Mantener como componente HTML/CSS custom (`AgreementStatusTimeline.razor`). No migrar a chart. |

> Para CADA caso "No hay equivalente nativo", documenta la decisión en un comentario `<!-- MIGRACIÓN: ... -->` en el archivo `.razor` afectado.

---

## 4. Implementación paso a paso (con commits)

### Etapa 0 — Rama e inventario (solo reporte, sin cambios de código)

1. Crea la rama: `git checkout -b refactor/migrate-charts-to-radzen`.
2. Ejecuta el inventario de la sección 2.
3. Genera el archivo `docs/chart-migration-inventory.md` con la tabla completa.
4. Commit: `docs: chart migration inventory (ApexCharts/Chart.js → Radzen)`.

### Etapa 1 — Infraestructura de charts Radzen (sin cambiar dashboards)

1. Crea la carpeta `Web/Components/Shared/Charts/`.
2. Crea `ChartPalette.cs` (sección 3.2).
3. Crea `AppChart.razor` + `AppChart.razor.cs` (sección 3.1).
4. Si existe un archivo de configuración global de ApexCharts (`ChartDefaults.cs`, `chartInterop.js`, `apexOptions.cs` o similar), NO lo elimines aún — solo créale el equivalente Radzen.
5. Verifica que compila: `dotnet build`.
6. Commit: `feat: add Radzen chart wrapper and palette infrastructure`.

### Etapa 2..N — Migración por módulo (un commit por grupo)

Migra los gráficos agrupados por módulo/carpeta. El orden sugerido (de menor a mayor complejidad):

1. **KPIs regulatorios** (`/kpis`, §5.2) — suelen ser los más simples (barras, líneas).
2. **Huella de Carbono** (`/reporting/carbon-footprint/`, §5.7) — áreas, donuts, líneas.
3. **Cumplimiento Normativo** (`/reporting/regulatory-compliance/`, §5.8) — combinación de tipos.
4. **Tratamiento y Reciclaje** (`/reporting/tratamiento-reciclaje/`) — barras apiladas, donuts de tipología.
5. **Mapas de Calor** (`/reporting/heatmaps/`) — donuts, barras, tablas coloreadas.
6. **Optimización Logística RAEE** (`/logistics/`) — líneas, columnas, posibles combos.
7. **Movilidad Urbana** (`/logistics/urban-mobility/`) — áreas, barras.
8. **Dashboard principal** (`/`, §0.1) — cards con mini-charts, sparklines.
9. **Cualquier otro gráfico** no cubierto arriba.

**Para cada módulo:**

1. Abre cada archivo `.razor` del inventario que pertenezca a ese módulo.
2. Reemplaza el markup de `<ApexChart>` o `<canvas>` (Chart.js) por `<AppChart>` con las series Radzen correspondientes según el mapeo 3.3.
3. Elimina:
   - Los `@using` de ApexCharts en ese archivo.
   - Las propiedades/campos de `ApexChartOptions`, `ApexPointSeries`, etc.
   - Los métodos de JS interop para Chart.js (`OnAfterRenderAsync` con `InvokeAsync("createChart", ...)`, etc.).
   - El code-behind (`.razor.cs`) que inicialice opciones de ApexCharts/Chart.js.
4. Preserva íntegramente:
   - Los queries CQRS (`Mediator.Send(new GetXxxQuery { ... })`).
   - Los filtros de la UI (dropdowns, date pickers, etc.).
   - La lógica de `OnParametersSetAsync` / `OnInitializedAsync` para cargar datos.
   - El filtrado multi-tenant (`OwnerId`) y por perfil (`LinkedEntityId`).
5. Compila: `dotnet build`. Corrige TODOS los errores antes de continuar con el siguiente módulo.
6. Si hay tests para ese módulo, ejecútalos: `dotnet test`.
7. Commit: `refactor: migrate [nombre-módulo] charts to Radzen (X gráficos)`.

> **Regla estricta**: NO acumules errores de compilación entre módulos. Cada commit debe compilar limpio.

### Ejemplo de transformación (referencia)

**Antes (ApexCharts)**:
```razor
@using ApexCharts

<ApexChart TItem="KpiDto" Title="Tasa de Reciclaje por SCRAP"
           Options="@_chartOptions">
    <ApexPointSeries TItem="KpiDto"
                     Items="@_data"
                     SeriesType="SeriesType.Bar"
                     XValue="@(e => e.ScrapName)"
                     YValue="@(e => e.RecyclingRate)" />
</ApexChart>

@code {
    private ApexChartOptions<KpiDto> _chartOptions = new() { ... };
    private List<KpiDto> _data = new();
}
```

**Después (Radzen)** — consulta el MCP para confirmar la API exacta:
```razor
<AppChart Title="Tasa de Reciclaje por SCRAP"
          CategoryAxisTitle="SCRAP"
          ValueAxisTitle="Tasa (%)">
    <RadzenColumnSeries Data="@_data"
                        ValueProperty="RecyclingRate"
                        CategoryProperty="ScrapName"
                        Title="Tasa de Reciclaje" />
</AppChart>

@code {
    private List<KpiDto> _data = new();
}
```

> Este ejemplo es orientativo. **Confirma con el MCP** los nombres exactos de `ValueProperty`, `CategoryProperty`, `Title` y cualquier otra propiedad requerida por `RadzenColumnSeries<T>`.

---

## 5. Limpieza

Una vez migrados TODOS los gráficos (inventario 100% cubierto):

### 5.1 Eliminar dependencias

1. **NuGet**: elimina `Blazor-ApexCharts` (o el nombre exacto del paquete) del `.csproj`:
   ```bash
   dotnet remove package Blazor-ApexCharts
   ```
2. **NPM/Libman**: si `chart.js` o `apexcharts` están en `package.json`, `libman.json` o descargados manualmente en `wwwroot/lib/`, elimínalos.
3. **Scripts en layouts**: elimina cualquier `<script src="...apexcharts...">` o `<script src="...chart.js...">` de `App.razor`, `_Host.cshtml`, `_Layout.cshtml` o cualquier layout.
4. **CSS de ApexCharts**: elimina cualquier `<link>` a CSS de ApexCharts.
5. **Archivos JS de interop**: elimina archivos `.js` en `wwwroot/js/` (o similar) que contengan funciones de inicialización/actualización de Chart.js o ApexCharts.

### 5.2 Verificar limpieza completa

Ejecuta estas búsquedas y confirma **cero resultados**:

```bash
grep -rn "ApexChart" --include="*.razor" --include="*.cs" --include="*.csproj" --include="*.razor.cs" src/
grep -rn "Blazor-ApexCharts" --include="*.csproj" src/
grep -rn "chart\.js\|chart\.min\.js\|new Chart(" --include="*.razor" --include="*.js" --include="*.cshtml" src/ wwwroot/
grep -rn "apexcharts" --include="*.json" --include="*.js" --include="*.cshtml" --include="*.razor" src/ wwwroot/
```

### 5.3 Actualizar documentación

1. En `README.md`: reemplaza "ApexCharts" por "Radzen Blazor Charts (`RadzenChart`)" en la sección de tecnología/stack.
2. En `COPILOT_CONTEXT.md`: actualiza la mención de gráficos para reflejar Radzen Charts.
3. Si existe `Documentacion_Completa_GreenTransit.md` o `Mapa_Funcionalidades.md` con mención a ApexCharts, indica que el estándar es ahora Radzen Charts (no es necesario editar cada línea, pero sí la tabla de tecnología y las notas de criterios de aceptación principales).

### 5.4 Commit final de limpieza

```
chore: remove ApexCharts/Chart.js dependencies and update docs
```

---

## 6. Reglas de calidad (cumplir en TODAS las etapas)

- **No duplicar lógica**: los formatos de número (`N2`), fecha (`dd/MM/yyyy`), paleta de colores y configuración de ejes se definen SOLO en `AppChart.razor` y `ChartPalette.cs`. Los dashboards individuales NO repiten estos valores.
- **No romper SSR/Blazor patterns**: los gráficos Radzen son componentes Blazor nativos. NO añadas JS interop salvo que el MCP de Radzen lo requiera explícitamente. Minimiza el JavaScript.
- **Mantener comportamiento funcional**: mismos datos, mismos filtros, misma lógica de negocio. Solo cambia la capa de presentación del gráfico.
- **Output visual razonablemente equivalente**: los gráficos deben transmitir la misma información. No es necesario replicar pixel-perfect animaciones o transiciones de ApexCharts — Radzen tiene sus propias.
- **No alterar capa Application ni Domain**: los queries CQRS, DTOs, servicios y validators NO se modifican. Solo se cambia la capa Web (componentes `.razor`).
- **Respetar la paleta corporativa**: toda serie debe usar un color de `ChartPalette.CorporateColors`. No uses colores por defecto de Radzen ni colores arbitrarios.
- **Mantener la autorización dinámica**: el acceso a dashboards sigue siendo por `PageDefinitions`/`PagePermissions`. No hardcodees roles.
- **No crear nuevas entidades de dominio**: la migración es puramente visual.
- **Modo oscuro/claro**: los gráficos deben ser legibles en ambos modos. Configura colores de ejes, labels y tooltips que se adapten (usa las variables CSS `--gt-*` o `--rz-*` donde sea posible).

---

## Resumen de archivos a crear

| Archivo | Acción |
|---------|--------|
| `docs/chart-migration-inventory.md` | CREAR — Inventario completo |
| `Web/Components/Shared/Charts/ChartPalette.cs` | CREAR — Paleta corporativa |
| `Web/Components/Shared/Charts/AppChart.razor` | CREAR — Wrapper centralizado |
| `Web/Components/Shared/Charts/AppChart.razor.cs` | CREAR — Code-behind del wrapper |
| Cada `.razor` con gráficos (del inventario) | MODIFICAR — Reemplazar ApexChart/Chart.js por Radzen |
| `.csproj` del proyecto Web | MODIFICAR — Eliminar paquete ApexCharts |
| `App.razor` / layouts | MODIFICAR — Eliminar scripts/CSS de ApexCharts/Chart.js |
| `wwwroot/js/chart*.js` (si existen) | ELIMINAR — Interop JS obsoleto |
| `README.md` | MODIFICAR — Actualizar stack tecnológico |
| `COPILOT_CONTEXT.md` | MODIFICAR — Actualizar referencia de gráficos |

---

## Recordatorios finales

- Antes de escribir cualquier serie Radzen, **consulta el MCP de Radzen Blazor** para confirmar la API actual.
- Si un componente de gráfico no tiene equivalente directo en Radzen y la simplificación no es aceptable, documéntalo y proponme una alternativa ANTES de implementarla.
- Cada etapa debe compilar limpio. No pases a la siguiente etapa con errores.
- Si necesitas ver el contenido de un archivo concreto para completar el inventario o la migración, pídelo explícitamente: "Necesito ver el archivo `[ruta]`".
