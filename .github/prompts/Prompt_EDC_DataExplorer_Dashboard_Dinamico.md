# 🤖 Prompt para GitHub Copilot — EDC Data Explorer: Dashboard Dinámico para Assets del Espacio de Datos

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades.md`, `ConsumeData.razor`, `DownloadTransferDataCommand.cs`, `EdcDataDownloadResponse.cs`, `IEdcManagementClient.cs` y `EdcManagementClient.cs`.
>
> **Stack**: .NET 10 · Clean Architecture (Domain / Application / Infrastructure / Web) · Blazor Server · Radzen Blazor Components · MediatR · FluentValidation · System.Text.Json.
>
> **Ejecuta los prompts en orden**. Cada fase debe compilar antes de pasar a la siguiente.

---

## 📋 ÍNDICE Y ESTADO

| ID | Fase | Descripción | Estado |
|---|---|---|:-:|
| DE-0 | Contexto | Instrucción base — NO genera código | ⬜ |
| DE-1 | DTOs | Modelo intermedio de descriptores de widgets | ⬜ |
| DE-2 | Servicio | `IJsonSchemaAnalyzer` + `JsonSchemaAnalyzer` — análisis de estructura JSON | ⬜ |
| DE-3 | Servicio | `IDashboardLayoutBuilder` + `DashboardLayoutBuilder` — heurísticas de widgets | ⬜ |
| DE-4 | Query | `AnalyzeEdcDataQuery` — CQRS que orquesta análisis + layout | ⬜ |
| DE-5 | UI | Componentes Blazor dinámicos (`EdcDataExplorer.razor` + widgets) | ⬜ |
| DE-6 | Integración | Botón "Explorar datos" en `ConsumeData.razor` + navegación | ⬜ |
| DE-7 | Menú | Actualización de menú lateral y permisos | ⬜ |
| DE-8 | Tests | Tests unitarios del analizador y del builder | ⬜ |

---

## 🎯 OBJETIVO GENERAL

Actualmente, la pantalla `/ecodatanet/consume-data` permite descubrir catálogos EDC, negociar contratos y descargar datos. El resultado de la descarga es un JSON con la estructura propia de cada asset del espacio de datos EcoDataNet — **no hay una estructura común entre assets**.

El objetivo es crear un **"Data Explorer"**: una pantalla que reciba cualquier JSON descargado de una transferencia EDC, analice su estructura en runtime, y genere automáticamente un dashboard con KPI cards, tablas y gráficos, **sin necesidad de programar un componente específico para cada tipo de asset**.

**Principios clave**:
- No se crean nuevas entidades de dominio ni tablas en la base de datos.
- Todo el procesamiento es en memoria sobre el JSON descargado.
- Se reutilizan los componentes Radzen Blazor ya instalados en el proyecto (RadzenCard, RadzenDataGrid, RadzenChart, etc.).
- Se respetan las variables CSS corporativas `--gt-*` y la paleta de colores del proyecto.
- La pantalla se integra en el menú EcoDataNet y usa el sistema de permisos dinámico (`PageDefinitions`/`PagePermissions`).

---

## DE-0 — Instrucción base (contexto)

```
CONTEXTO DEL PROYECTO:
- Proyecto GreenTransit — .NET 10, Blazor Web App (Server), Radzen Blazor Components, EF Core, SQL Server Azure.
- Clean Architecture: GreenTransit.Domain / Application / Infrastructure / Web / Tests.
- MediatR, FluentValidation, Serilog, xUnit ya configurados.
- Módulo EcoDataNet COMPLETAMENTE IMPLEMENTADO: configuración de conector, descubrimiento de catálogo DCAT/ODRL, negociación de contrato EDC v3, transferencia de datos y descarga.
- El resultado de `DownloadTransferDataCommand` es un `EdcDataDownloadResponse` que contiene el contenido descargado como string (JSON, CSV, etc.).
- Los gráficos del proyecto usan Radzen Blazor Charts (migrado desde ApexCharts).
- La paleta de colores corporativa usa tokens CSS: --gt-dark-petroleum (#0A404B), --gt-secondary-1 (#8ACCC3), --gt-secondary-3 (#D8B00E), --gt-secondary-4 (#D36F15), --gt-secondary-5 (#C13E43), --gt-secondary-6 (#6E4583), --gt-secondary-7 (#535497), --gt-secondary-2 (#B4B736).
- Autorización dinámica vía PageDefinitions/PagePermissions (no hardcoded).
- Multi-tenant: filtro por OwnerId en todas las queries.

OBJETIVO:
Implementar "EDC Data Explorer" — un visualizador dinámico de JSON que genera dashboards automáticos a partir de datos descargados de assets del espacio de datos EcoDataNet.

REGLAS GENERALES:
1. Respetar Clean Architecture: interfaces en Application, implementaciones pueden ir en Application (son servicios puros de lógica, no de infraestructura).
2. Usar System.Text.Json (JsonDocument, JsonElement) para el análisis — NO Newtonsoft.
3. No crear tablas ni entidades de dominio.
4. Reutilizar componentes Radzen existentes.
5. Nombrar todo en inglés (clases, métodos, variables). Comentarios en español.
6. Seguir el patrón CQRS con MediatR para la query de análisis.

NO generes código aún. Confirma que has entendido el contexto.
```

---

## DE-1 — DTOs: Modelo intermedio de descriptores

```
FASE DE-1 — DTOs del Data Explorer

OBJETIVO: Crear los DTOs que describen la estructura detectada del JSON y los widgets a renderizar.

UBICACIÓN: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/

CREAR estos archivos:

--- ARCHIVO 1: JsonPropertyDescriptor.cs ---

Describe una propiedad individual detectada en el JSON.

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Describe una propiedad individual detectada en un JSON analizado.
/// </summary>
public class JsonPropertyDescriptor
{
    /// <summary>Nombre original de la propiedad en el JSON (ej: "totalTonsProcessed").</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Nombre humanizado para mostrar en UI (ej: "Total Tons Processed").</summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>Ruta completa en el JSON (ej: "root.wasteByCategory[].tons").</summary>
    public string JsonPath { get; set; } = string.Empty;
    
    /// <summary>Tipo CLR detectado: String, Number, Boolean, DateTime, Array, Object, Null.</summary>
    public JsonPropertyType PropertyType { get; set; }
    
    /// <summary>Si es Number: true si parece un porcentaje (valor entre 0-1 o nombre contiene rate/percent/tasa).</summary>
    public bool IsPercentage { get; set; }
    
    /// <summary>Si es String: true si parece una fecha ISO 8601.</summary>
    public bool IsDate { get; set; }
    
    /// <summary>Si está dentro de un array: número de valores únicos encontrados.</summary>
    public int? UniqueValueCount { get; set; }
    
    /// <summary>Hasta 5 valores de ejemplo para previsualización.</summary>
    public List<string> SampleValues { get; set; } = new();
}

public enum JsonPropertyType
{
    String,
    Number,
    Boolean,
    DateTime,
    Array,
    Object,
    Null
}

--- ARCHIVO 2: JsonDataSchema.cs ---

Describe la estructura completa detectada del JSON.

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Estructura completa detectada del JSON analizado.
/// </summary>
public class JsonDataSchema
{
    /// <summary>Propiedades escalares del nivel raíz (candidatas a KPI cards o cabecera).</summary>
    public List<JsonPropertyDescriptor> RootScalars { get; set; } = new();
    
    /// <summary>Arrays de objetos homogéneos detectados (candidatos a tablas/gráficos).</summary>
    public List<JsonArrayDescriptor> Arrays { get; set; } = new();
    
    /// <summary>Objetos anidados (subgrupos lógicos).</summary>
    public List<JsonObjectDescriptor> NestedObjects { get; set; } = new();
    
    /// <summary>True si el JSON raíz es un array (en vez de un objeto).</summary>
    public bool RootIsArray { get; set; }
    
    /// <summary>Número total de propiedades detectadas en todos los niveles.</summary>
    public int TotalPropertyCount { get; set; }
}

/// <summary>
/// Describe un array de objetos homogéneos encontrado en el JSON.
/// </summary>
public class JsonArrayDescriptor
{
    /// <summary>Nombre de la propiedad que contiene el array (ej: "wasteByCategory").</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Nombre humanizado (ej: "Waste By Category").</summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>Ruta en el JSON.</summary>
    public string JsonPath { get; set; } = string.Empty;
    
    /// <summary>Número de elementos en el array.</summary>
    public int ItemCount { get; set; }
    
    /// <summary>Propiedades comunes de los objetos del array (esquema del item).</summary>
    public List<JsonPropertyDescriptor> ItemProperties { get; set; } = new();
    
    /// <summary>True si los objetos del array son homogéneos (mismas propiedades).</summary>
    public bool IsHomogeneous { get; set; }
    
    /// <summary>Propiedad candidata a eje de categorías (primer string con pocos valores únicos).</summary>
    public string? CategoryProperty { get; set; }
    
    /// <summary>Propiedad candidata a eje temporal (primer campo fecha/temporal).</summary>
    public string? TemporalProperty { get; set; }
    
    /// <summary>Propiedades candidatas a valores numéricos (para gráficos).</summary>
    public List<string> NumericProperties { get; set; } = new();
    
    /// <summary>Datos crudos del array como lista de diccionarios para renderizado.</summary>
    public List<Dictionary<string, object?>> RawData { get; set; } = new();
}

/// <summary>
/// Describe un objeto anidado encontrado en el JSON.
/// </summary>
public class JsonObjectDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
    public List<JsonPropertyDescriptor> Properties { get; set; } = new();
    /// <summary>Sub-arrays dentro de este objeto.</summary>
    public List<JsonArrayDescriptor> ChildArrays { get; set; } = new();
}

--- ARCHIVO 3: DynamicWidgetDescriptor.cs ---

Describe un widget a renderizar en el dashboard dinámico.

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Describe un widget individual a renderizar en el dashboard dinámico.
/// </summary>
public class DynamicWidgetDescriptor
{
    /// <summary>Identificador único del widget (para reordenación).</summary>
    public string WidgetId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    
    /// <summary>Tipo de widget a renderizar.</summary>
    public WidgetType Type { get; set; }
    
    /// <summary>Título para mostrar en la cabecera del widget.</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>Subtítulo opcional (ej: nombre del proveedor, fecha).</summary>
    public string? Subtitle { get; set; }
    
    /// <summary>Orden de renderizado (menor = más arriba/izquierda).</summary>
    public int SortOrder { get; set; }
    
    /// <summary>Ancho en columnas del grid CSS (1-12, sistema de 12 columnas).</summary>
    public int ColumnSpan { get; set; } = 12;
    
    // --- Datos para KPI Card ---
    
    /// <summary>Valor principal formateado (ej: "14.250,5", "87,3%").</summary>
    public string? KpiValue { get; set; }
    
    /// <summary>Valor numérico crudo (para formato condicional).</summary>
    public double? KpiNumericValue { get; set; }
    
    /// <summary>Unidad o sufijo (ej: "t", "%", "€").</summary>
    public string? KpiUnit { get; set; }
    
    /// <summary>Icono sugerido (nombre de icono Material Design para Radzen).</summary>
    public string? KpiIcon { get; set; }
    
    /// <summary>Color de acento del KPI (hex).</summary>
    public string? KpiColor { get; set; }
    
    // --- Datos para Tabla ---
    
    /// <summary>Definición de columnas para la tabla.</summary>
    public List<TableColumnDescriptor>? TableColumns { get; set; }
    
    /// <summary>Filas de datos como diccionarios clave-valor.</summary>
    public List<Dictionary<string, object?>>? TableData { get; set; }
    
    // --- Datos para Gráfico ---
    
    /// <summary>Tipo de gráfico sugerido.</summary>
    public ChartSubType? ChartType { get; set; }
    
    /// <summary>Nombre de la propiedad usada como eje X / categorías.</summary>
    public string? ChartCategoryField { get; set; }
    
    /// <summary>Nombres de las propiedades usadas como series de valores.</summary>
    public List<string>? ChartValueFields { get; set; }
    
    /// <summary>Datos del gráfico (misma estructura que TableData).</summary>
    public List<Dictionary<string, object?>>? ChartData { get; set; }
    
    // --- Datos para texto/cabecera ---
    
    /// <summary>Texto libre para widgets de tipo Header o Info.</summary>
    public string? TextContent { get; set; }
    
    /// <summary>Pares clave-valor para widgets de tipo KeyValueList.</summary>
    public Dictionary<string, string>? KeyValuePairs { get; set; }
}

public enum WidgetType
{
    /// <summary>Tarjeta KPI con valor grande, icono y unidad.</summary>
    KpiCard,
    /// <summary>Tabla paginada con columnas dinámicas.</summary>
    DataTable,
    /// <summary>Gráfico Radzen (bar, line, donut, etc.).</summary>
    Chart,
    /// <summary>Cabecera de sección con texto descriptivo.</summary>
    SectionHeader,
    /// <summary>Lista de pares clave-valor (para objetos anidados simples).</summary>
    KeyValueList,
    /// <summary>Texto informativo (strings largos, descripciones).</summary>
    InfoText
}

public enum ChartSubType
{
    BarVertical,
    BarHorizontal,
    Line,
    Area,
    Donut,
    Pie
}

/// <summary>
/// Describe una columna de una tabla dinámica.
/// </summary>
public class TableColumnDescriptor
{
    /// <summary>Nombre de la propiedad en el diccionario de datos.</summary>
    public string PropertyName { get; set; } = string.Empty;
    
    /// <summary>Título humanizado para la cabecera de columna.</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>Tipo de dato para formato (Number, String, DateTime, Boolean, Percentage).</summary>
    public string DataType { get; set; } = "String";
    
    /// <summary>Ancho sugerido en píxeles (null = auto).</summary>
    public int? Width { get; set; }
    
    /// <summary>Formato .NET para números/fechas (ej: "N2", "dd/MM/yyyy").</summary>
    public string? FormatString { get; set; }
}

--- ARCHIVO 4: EdcDataExplorerResult.cs ---

DTO de resultado que la query CQRS devuelve al componente Blazor.

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Resultado del análisis de un JSON de asset EDC, listo para renderizar.
/// </summary>
public class EdcDataExplorerResult
{
    /// <summary>True si el análisis fue exitoso.</summary>
    public bool Success { get; set; }
    
    /// <summary>Mensaje de error si el análisis falló (JSON inválido, vacío, etc.).</summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>Esquema detectado del JSON (para debug/inspección).</summary>
    public JsonDataSchema? Schema { get; set; }
    
    /// <summary>Lista ordenada de widgets a renderizar.</summary>
    public List<DynamicWidgetDescriptor> Widgets { get; set; } = new();
    
    /// <summary>Metadatos del asset (proveedor, fecha de descarga, tamaño).</summary>
    public DataExplorerMetadata Metadata { get; set; } = new();
}

public class DataExplorerMetadata
{
    /// <summary>Nombre del proveedor (si se pasa como parámetro).</summary>
    public string? ProviderName { get; set; }
    
    /// <summary>Nombre del dataset/asset (si se pasa como parámetro).</summary>
    public string? DatasetName { get; set; }
    
    /// <summary>Fecha y hora de la descarga.</summary>
    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Tamaño del JSON en bytes.</summary>
    public long JsonSizeBytes { get; set; }
    
    /// <summary>Formato detectado (JSON Object, JSON Array).</summary>
    public string DetectedFormat { get; set; } = "JSON";
}

VERIFICACIÓN: El proyecto compila sin errores. Los 4 archivos están en Application/Features/EcoDataNet/DTOs/DataExplorer/.
```

---

## DE-2 — Servicio: Analizador de estructura JSON

```
FASE DE-2 — IJsonSchemaAnalyzer + JsonSchemaAnalyzer

OBJETIVO: Crear el servicio que recibe un string JSON y produce un JsonDataSchema describiendo su estructura.

--- ARCHIVO 1: GreenTransit.Application/Features/EcoDataNet/Services/IJsonSchemaAnalyzer.cs ---

using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Analiza la estructura de un JSON arbitrario y produce un esquema tipado.
/// </summary>
public interface IJsonSchemaAnalyzer
{
    /// <summary>
    /// Analiza el JSON proporcionado y devuelve un esquema descriptivo.
    /// </summary>
    /// <param name="jsonContent">String con el contenido JSON.</param>
    /// <returns>Esquema detectado o null si el JSON es inválido.</returns>
    JsonDataSchema? Analyze(string jsonContent);
}

--- ARCHIVO 2: GreenTransit.Application/Features/EcoDataNet/Services/JsonSchemaAnalyzer.cs ---

Implementación usando System.Text.Json (JsonDocument, JsonElement).

REGLAS DE ANÁLISIS:

1. **Parsing seguro**: usar JsonDocument.Parse con try-catch. Si falla, devolver null.

2. **Detección del nivel raíz**:
   - Si el root es un JsonObject: recorrer sus propiedades.
   - Si el root es un JsonArray: marcar RootIsArray=true, tratar como un único array anónimo.

3. **Clasificación de propiedades del nivel raíz** (para cada propiedad del objeto raíz):
   - `JsonValueKind.String`: comprobar si es fecha (regex ISO 8601: `^\d{4}-\d{2}(-\d{2})?`). Añadir a RootScalars.
   - `JsonValueKind.Number`: añadir a RootScalars. Comprobar si es porcentaje (ver regla 4).
   - `JsonValueKind.True/False`: añadir a RootScalars con PropertyType=Boolean.
   - `JsonValueKind.Array`: procesar como array (ver regla 5).
   - `JsonValueKind.Object`: procesar como objeto anidado (ver regla 6).
   - `JsonValueKind.Null`: ignorar.

4. **Detección de porcentajes** — un campo numérico se marca IsPercentage=true si:
   - Su nombre (case-insensitive) contiene: `rate`, `ratio`, `percent`, `percentage`, `tasa`, `porcentaje`, `pct`, `share`.
   - O su valor está entre 0.0 y 1.0 (inclusive) Y el nombre NO contiene palabras que indiquen cantidad absoluta como `count`, `total`, `amount`, `quantity`, `tons`, `kg`, `units`.

5. **Procesamiento de arrays**:
   - Si el array está vacío: ignorar.
   - Si los items son valores simples (string/number): crear un JsonArrayDescriptor con una sola propiedad "value".
   - Si los items son objetos: analizar las propiedades del PRIMER elemento como esquema base. Comparar con los siguientes 10 elementos para verificar homogeneidad (IsHomogeneous=true si ≥80% de items tienen las mismas propiedades).
   - Extraer hasta 5 valores de ejemplo por propiedad.
   - Identificar CategoryProperty: primera propiedad string con ≤20 valores únicos.
   - Identificar TemporalProperty: primera propiedad cuyo nombre contenga `date`, `fecha`, `month`, `mes`, `year`, `año`, `period`, `periodo`, `quarter`, `trimestre`, `time`, `timestamp`, `week`, `semana` O cuyo valor sea una fecha ISO 8601.
   - Identificar NumericProperties: todas las propiedades con JsonValueKind.Number.
   - Cargar RawData: convertir cada elemento del array a Dictionary<string, object?> (números → double, strings → string, booleans → bool, null → null).
   - **LÍMITE**: procesar máximo 1000 elementos del array para evitar problemas de memoria.

6. **Procesamiento de objetos anidados**:
   - Crear un JsonObjectDescriptor con las propiedades escalares del objeto.
   - Si el objeto contiene sub-arrays, procesarlos recursivamente (máx. 2 niveles de profundidad).

7. **Humanización de nombres** — convertir camelCase/snake_case/PascalCase a título legible:
   - `totalTonsProcessed` → "Total Tons Processed"
   - `waste_by_category` → "Waste By Category"
   - `recyclingRate` → "Recycling Rate"
   - `CO2Emissions` → "CO2 Emissions"
   - Usar un método helper `HumanizePropertyName(string name)`.

8. **Iconos sugeridos para KPI cards** — asignar iconos Material Design según el nombre del campo:
   - Contiene `ton`, `weight`, `peso`, `kg` → "scale"
   - Contiene `rate`, `percent`, `tasa` → "percent"
   - Contiene `co2`, `carbon`, `emission` → "eco"
   - Contiene `cost`, `price`, `amount`, `importe` → "euro"
   - Contiene `count`, `total`, `number`, `cantidad` → "tag"
   - Contiene `date`, `time`, `fecha` → "calendar_today"
   - Default → "analytics"

REGISTRO DI: NO registrar en el contenedor de DI todavía (se hará en DE-4).

VERIFICACIÓN: Crear un test mental — dado este JSON:
{
  "provider": "SCRAP Andalucía",
  "reportDate": "2026-03-15",
  "totalTonsProcessed": 14250.5,
  "recyclingRate": 0.873,
  "wasteByCategory": [
    { "category": "RAEE", "tons": 3200, "percentage": 22.5 },
    { "category": "Vidrio", "tons": 5100, "percentage": 35.8 }
  ]
}

El resultado debería tener:
- RootScalars: 4 propiedades (provider=String, reportDate=DateTime, totalTonsProcessed=Number, recyclingRate=Number+IsPercentage)
- Arrays: 1 (wasteByCategory, 2 items, CategoryProperty="category", NumericProperties=["tons","percentage"])
- NestedObjects: 0
```

---

## DE-3 — Servicio: Constructor de layout de widgets

```
FASE DE-3 — IDashboardLayoutBuilder + DashboardLayoutBuilder

OBJETIVO: Crear el servicio que recibe un JsonDataSchema y produce una lista ordenada de DynamicWidgetDescriptor.

--- ARCHIVO 1: GreenTransit.Application/Features/EcoDataNet/Services/IDashboardLayoutBuilder.cs ---

using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Construye el layout de widgets dinámicos a partir de un esquema JSON analizado.
/// </summary>
public interface IDashboardLayoutBuilder
{
    /// <summary>
    /// Genera la lista de widgets a renderizar.
    /// </summary>
    List<DynamicWidgetDescriptor> Build(JsonDataSchema schema);
}

--- ARCHIVO 2: GreenTransit.Application/Features/EcoDataNet/Services/DashboardLayoutBuilder.cs ---

Implementación con las siguientes HEURÍSTICAS DE ASIGNACIÓN:

PALETA DE COLORES para KPI cards (rotar en orden):
string[] kpiColors = { "#0A404B", "#8ACCC3", "#D8B00E", "#D36F15", "#C13E43", "#6E4583", "#535497", "#B4B736" };

REGLA 1 — CABECERA DE CONTEXTO (SortOrder=0):
- Si entre las propiedades RootScalars hay campos de tipo String que NO son fechas y tienen un solo valor (no están en arrays), combinarlos en un widget SectionHeader.
- Ejemplo: "provider": "SCRAP Andalucía" + "reportDate": "2026-03-15" → SectionHeader con Title="Datos del proveedor" y KeyValuePairs={"Proveedor":"SCRAP Andalucía","Fecha del informe":"15/03/2026"}.

REGLA 2 — KPI CARDS (SortOrder=10..19):
- Cada propiedad numérica del nivel raíz genera un KpiCard.
- ColumnSpan: si hay 1 KPI → 12; 2 KPIs → 6; 3 KPIs → 4; 4+ KPIs → 3.
- Formato del valor:
  - Si IsPercentage y valor entre 0-1: multiplicar por 100 y mostrar con sufijo "%" (ej: 0.873 → "87,3 %").
  - Si IsPercentage y valor > 1: mostrar tal cual con sufijo "%" (ej: 22.5 → "22,5 %").
  - Si es entero grande (>1000): usar formato con separador de miles (ej: 14250 → "14.250").
  - Si tiene decimales: usar "N2" (ej: 14250.5 → "14.250,50").
- KpiIcon: asignado por el analizador (regla 8 de DE-2).
- KpiColor: rotar de la paleta.

REGLA 3 — ARRAYS CON TEMPORAL + NUMÉRICO → LINE/AREA CHART (SortOrder=20..29):
- Si un array tiene TemporalProperty Y al menos una NumericProperty:
  - Generar un widget Chart con ChartType=Line (o Area si hay una sola serie).
  - ChartCategoryField = TemporalProperty.
  - ChartValueFields = NumericProperties (máx. 5 series).
  - ColumnSpan = 12 (o 6 si hay más de un array).
  - ChartData = RawData del array.

REGLA 4 — ARRAYS CON CATEGORÍA + NUMÉRICO → BAR/DONUT (SortOrder=30..39):
- Si un array tiene CategoryProperty Y al menos una NumericProperty, y NO tiene TemporalProperty:
  - Si ≤7 categorías y 1 sola propiedad numérica: Donut. ColumnSpan=6.
  - Si >7 categorías o múltiples propiedades numéricas: BarVertical. ColumnSpan=6.
  - ChartCategoryField = CategoryProperty.
  - ChartValueFields = NumericProperties.

REGLA 5 — TODOS LOS ARRAYS HOMOGÉNEOS → DATA TABLE (SortOrder=40..49):
- Cada array homogéneo genera SIEMPRE un widget DataTable (además del gráfico si aplican reglas 3-4).
- TableColumns: una columna por propiedad del item.
  - Para números: DataType="Number", FormatString="N2".
  - Para porcentajes: DataType="Percentage", FormatString="N1".
  - Para fechas: DataType="DateTime", FormatString="dd/MM/yyyy".
  - Para strings: DataType="String".
- TableData = RawData del array.
- ColumnSpan = 12.

REGLA 6 — OBJETOS ANIDADOS → KEY-VALUE LIST (SortOrder=50..59):
- Cada NestedObject con ≤10 propiedades escalares genera un KeyValueList.
- Si tiene >10 propiedades: generar una DataTable de 2 columnas (Propiedad, Valor).
- Si contiene sub-arrays: procesar los sub-arrays con las reglas 3-5.
- ColumnSpan=6.

REGLA 7 — STRINGS LARGAS → INFO TEXT (SortOrder=60..69):
- Cualquier propiedad String del nivel raíz con longitud > 200 caracteres genera un InfoText.
- ColumnSpan=12.

REGLA 8 — ARRAYS NO HOMOGÉNEOS O DE VALORES SIMPLES:
- Array de strings simples: generar InfoText con los valores separados por comas.
- Array de números simples: generar un mini BarVertical con índice como categoría.
- Array no homogéneo: generar DataTable con las columnas del superconjunto de propiedades.

REGLA 9 — LAYOUT FINAL:
- Ordenar todos los widgets por SortOrder.
- Los KpiCards siempre van primero (fila de tarjetas).
- Los Charts van debajo de los KPIs.
- Las DataTables van debajo de los Charts.
- Los KeyValueList e InfoText van al final.

VERIFICACIÓN: Compilar. Con el JSON de ejemplo de DE-2, el resultado debería ser:
1. SectionHeader: "Proveedor: SCRAP Andalucía — Fecha: 15/03/2026"
2. KpiCard: "Total Tons Processed" = "14.250,50" icon=scale
3. KpiCard: "Recycling Rate" = "87,3 %" icon=percent
4. Donut o BarVertical: "Waste By Category" con categorías RAEE/Vidrio
5. DataTable: "Waste By Category" con columnas category/tons/percentage
```

---

## DE-4 — Query CQRS: AnalyzeEdcDataQuery

```
FASE DE-4 — AnalyzeEdcDataQuery (MediatR)

OBJETIVO: Crear la query CQRS que orquesta el análisis y la construcción del layout.

--- ARCHIVO 1: GreenTransit.Application/Features/EcoDataNet/Queries/AnalyzeEdcDataQuery.cs ---

using MediatR;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

public class AnalyzeEdcDataQuery : IRequest<EdcDataExplorerResult>
{
    /// <summary>Contenido JSON crudo descargado de la transferencia EDC.</summary>
    public string JsonContent { get; set; } = string.Empty;
    
    /// <summary>Nombre del proveedor (para metadatos, opcional).</summary>
    public string? ProviderName { get; set; }
    
    /// <summary>Nombre del dataset/asset (para metadatos, opcional).</summary>
    public string? DatasetName { get; set; }
}

--- ARCHIVO 2: GreenTransit.Application/Features/EcoDataNet/Queries/AnalyzeEdcDataQueryHandler.cs ---

El handler inyecta IJsonSchemaAnalyzer y IDashboardLayoutBuilder.

Lógica:
1. Validar que JsonContent no esté vacío. Si lo está → EdcDataExplorerResult con Success=false y ErrorMessage.
2. Llamar a _schemaAnalyzer.Analyze(request.JsonContent). Si devuelve null → error de JSON inválido.
3. Llamar a _layoutBuilder.Build(schema) para obtener la lista de widgets.
4. Construir EdcDataExplorerResult con Success=true, Schema, Widgets y Metadata (tamaño, proveedor, dataset, fecha).
5. Envolver todo en try-catch. Si hay cualquier excepción → Success=false con mensaje descriptivo.

--- REGISTRO DI ---

En GreenTransit.Application (o donde tengas el registro de servicios de Application):

services.AddTransient<IJsonSchemaAnalyzer, JsonSchemaAnalyzer>();
services.AddTransient<IDashboardLayoutBuilder, DashboardLayoutBuilder>();

Si el proyecto centraliza el DI de Application en un método AddApplicationServices() o similar, registrar ahí.
Si el proyecto usa auto-registro de handlers de MediatR, la query handler se registra automáticamente.

VERIFICACIÓN: Compilar. Crear un test unitario mínimo que pase un JSON de ejemplo y verifique que devuelve Success=true con al menos 1 widget.
```

---

## DE-5 — Componentes Blazor: Dashboard dinámico

```
FASE DE-5 — Componentes Blazor del Data Explorer

OBJETIVO: Crear la pantalla principal y los componentes de renderizado dinámico.

UBICACIÓN: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/

--- ARCHIVO 1: EdcDataExplorer.razor ---

@page "/ecodatanet/data-explorer"
@attribute [Authorize(Policy = PolicyConstants.CanAccessEDCDataExplorer)]

Parámetros de la página (recibidos vía query string o inyectados vía estado compartido):
- [SupplyParameterFromQuery] public string? JsonData — JSON codificado en Base64 (para URLs compartibles).
- Si JsonData no viene en query string, buscar en un servicio Scoped (EdcDataExplorerStateService) que almacene el JSON temporalmente en memoria al navegar desde ConsumeData.razor.

Lógica OnInitializedAsync:
1. Obtener el JSON (de query string decodificando Base64, o del estado compartido).
2. Si no hay JSON disponible: mostrar panel informativo con mensaje "Seleccione un asset desde Consumir datos para explorar su contenido" y botón de navegación a /ecodatanet/consume-data.
3. Si hay JSON: enviar AnalyzeEdcDataQuery vía MediatR. Mostrar spinner durante el análisis.
4. Si Success=false: mostrar RadzenAlert con el error.
5. Si Success=true: renderizar el dashboard.

Layout del dashboard:
- Barra superior con metadatos: nombre del proveedor, nombre del dataset, fecha de descarga, tamaño del JSON.
- Botón "Ver JSON crudo" que abre un RadzenDialog con el JSON formateado (indent=2) en un <pre> con scroll.
- Botón "Exportar a XLSX" (fase futura, por ahora solo placeholder deshabilitado).
- Grid de widgets:
  - Contenedor CSS con display:grid, grid-template-columns: repeat(12, 1fr), gap: 1rem.
  - Cada widget ocupa grid-column: span {ColumnSpan}.
  - Iterar sobre result.Widgets ordenados por SortOrder.
  - Para cada widget, renderizar con @switch(widget.Type):
    - WidgetType.KpiCard → <DynamicKpiCard Widget="widget" />
    - WidgetType.DataTable → <DynamicDataTable Widget="widget" />
    - WidgetType.Chart → <DynamicChart Widget="widget" />
    - WidgetType.SectionHeader → <DynamicSectionHeader Widget="widget" />
    - WidgetType.KeyValueList → <DynamicKeyValueList Widget="widget" />
    - WidgetType.InfoText → <DynamicInfoText Widget="widget" />

--- ARCHIVO 2: EdcDataExplorerStateService.cs ---

Ubicación: GreenTransit.Application/Features/EcoDataNet/Services/EdcDataExplorerStateService.cs
(o en Web/Services/ si prefieres mantenerlo en la capa Web)

Servicio Scoped que almacena temporalmente el JSON + metadatos para la navegación interna:

public class EdcDataExplorerStateService
{
    public string? JsonContent { get; set; }
    public string? ProviderName { get; set; }
    public string? DatasetName { get; set; }
    
    public bool HasData => !string.IsNullOrEmpty(JsonContent);
    
    public void Clear()
    {
        JsonContent = null;
        ProviderName = null;
        DatasetName = null;
    }
}

Registrar como Scoped en DI: services.AddScoped<EdcDataExplorerStateService>();

--- ARCHIVO 3: DynamicKpiCard.razor ---

Componente que renderiza una tarjeta KPI usando RadzenCard:

Recibe [Parameter] DynamicWidgetDescriptor Widget.

Estructura:
<RadzenCard class="dynamic-kpi-card" Style="border-left: 4px solid {Widget.KpiColor}">
    <div class="kpi-card-content">
        <div class="kpi-icon">
            <RadzenIcon Icon="@Widget.KpiIcon" Style="font-size:2rem; color:{Widget.KpiColor}" />
        </div>
        <div class="kpi-data">
            <div class="kpi-value">@Widget.KpiValue <span class="kpi-unit">@Widget.KpiUnit</span></div>
            <div class="kpi-title">@Widget.Title</div>
        </div>
    </div>
</RadzenCard>

CSS (en el .razor.css acompañante):
- .kpi-card-content: display:flex, align-items:center, gap:1rem.
- .kpi-value: font-size:1.75rem, font-weight:700, color: var(--gt-graphite-black).
- .kpi-unit: font-size:1rem, font-weight:400, color: var(--gt-stone-green).
- .kpi-title: font-size:0.85rem, color: var(--gt-stone-green), text-transform:uppercase, letter-spacing:0.05em.

--- ARCHIVO 4: DynamicDataTable.razor ---

Componente que renderiza una tabla paginada usando RadzenDataGrid:

Recibe [Parameter] DynamicWidgetDescriptor Widget.

Estructura:
<RadzenCard>
    <RadzenText TextStyle="TextStyle.H6">@Widget.Title</RadzenText>
    <RadzenDataGrid Data="@Widget.TableData" TItem="Dictionary<string, object?>"
                    AllowPaging="true" PageSize="10"
                    AllowSorting="true" AllowFiltering="true"
                    FilterMode="FilterMode.Simple"
                    Style="width:100%">
        @foreach (var col in Widget.TableColumns!)
        {
            <RadzenDataGridColumn TItem="Dictionary<string, object?>"
                                  Title="@col.Title"
                                  Property="@col.PropertyName"
                                  Width="@(col.Width.HasValue ? $"{col.Width}px" : null)"
                                  Sortable="true" Filterable="true">
                <Template Context="row">
                    @FormatCellValue(row, col)
                </Template>
            </RadzenDataGridColumn>
        }
    </RadzenDataGrid>
</RadzenCard>

El método FormatCellValue(Dictionary<string,object?> row, TableColumnDescriptor col):
- Obtiene row[col.PropertyName].
- Si es null: muestra "—".
- Si DataType=="Number": formatea con col.FormatString ?? "N2" usando cultura es-ES.
- Si DataType=="Percentage": formatea como número + " %".
- Si DataType=="DateTime": intenta parsear y formatea con "dd/MM/yyyy".
- Si DataType=="Boolean": muestra "Sí"/"No".
- Default: ToString().

--- ARCHIVO 5: DynamicChart.razor ---

Componente que renderiza un gráfico Radzen según el ChartSubType:

Recibe [Parameter] DynamicWidgetDescriptor Widget.

Lógica:
- Preparar los datos: convertir Widget.ChartData a la estructura que espera RadzenChart.
  - Cada serie (ChartValueFields) se mapea a un RadzenChartSeries (Bar, Line, Area, Donut, Pie).
  - El CategoryField se usa como eje X.

Estructura con @switch(Widget.ChartType):

case ChartSubType.BarVertical:
    <RadzenChart>
        <RadzenColumnSeries Data="@chartItems" CategoryProperty="Category" ValueProperty="Value" Title="@seriesTitle" />
        <RadzenCategoryAxis />
        <RadzenValueAxis />
    </RadzenChart>

case ChartSubType.Line / Area:
    <RadzenChart>
        @foreach (var series in Widget.ChartValueFields)
        {
            <RadzenLineSeries Data="@GetSeriesData(series)" CategoryProperty="Category" ValueProperty="Value" Title="@HumanizeName(series)" />
        }
        <RadzenCategoryAxis />
        <RadzenValueAxis />
    </RadzenChart>

case ChartSubType.Donut:
    <RadzenChart>
        <RadzenDonutSeries Data="@chartItems" CategoryProperty="Category" ValueProperty="Value" Title="@Widget.Title" />
    </RadzenChart>

Para esto necesitarás un helper interno que convierta Widget.ChartData (List<Dictionary<string,object?>>) a una lista de objetos anónimos o DTOs simples con propiedades "Category" y "Value" que Radzen pueda bindear.

Crear una clase interna privada:
private class ChartDataItem
{
    public string Category { get; set; } = "";
    public double Value { get; set; }
}

Y un método que transforme los datos:
private List<ChartDataItem> GetChartItems(string categoryField, string valueField)
{
    return Widget.ChartData?.Select(row => new ChartDataItem
    {
        Category = row.ContainsKey(categoryField) ? row[categoryField]?.ToString() ?? "" : "",
        Value = row.ContainsKey(valueField) && row[valueField] is double d ? d : 0
    }).ToList() ?? new();
}

Colores de las series: usar la paleta corporativa (rotar entre las 8 series del proyecto).

Envolver en RadzenCard con título.

--- ARCHIVO 6: DynamicSectionHeader.razor ---

Componente simple:
<div class="section-header" style="grid-column: span @Widget.ColumnSpan">
    <RadzenText TextStyle="TextStyle.H5">@Widget.Title</RadzenText>
    @if (Widget.KeyValuePairs != null)
    {
        <div class="section-metadata">
            @foreach (var kv in Widget.KeyValuePairs)
            {
                <span class="metadata-item"><strong>@kv.Key:</strong> @kv.Value</span>
            }
        </div>
    }
</div>

--- ARCHIVO 7: DynamicKeyValueList.razor ---

<RadzenCard>
    <RadzenText TextStyle="TextStyle.H6">@Widget.Title</RadzenText>
    <div class="kv-list">
        @foreach (var kv in Widget.KeyValuePairs ?? new())
        {
            <div class="kv-row">
                <span class="kv-label">@kv.Key</span>
                <span class="kv-value">@kv.Value</span>
            </div>
        }
    </div>
</RadzenCard>

--- ARCHIVO 8: DynamicInfoText.razor ---

<RadzenCard>
    <RadzenText TextStyle="TextStyle.H6">@Widget.Title</RadzenText>
    <RadzenText TextStyle="TextStyle.Body1" Style="white-space:pre-wrap">@Widget.TextContent</RadzenText>
</RadzenCard>

VERIFICACIÓN: Compilar. La página /ecodatanet/data-explorer debe renderizar sin errores. Si no hay datos, muestra el panel informativo.
```

---

## DE-6 — Integración con ConsumeData.razor

```
FASE DE-6 — Botón "Explorar datos" en ConsumeData.razor

OBJETIVO: Añadir un botón junto al existente "Exportar a fichero" que navegue al Data Explorer con los datos descargados.

ARCHIVOS A MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/ConsumeData.razor

CAMBIOS:

1. Inyectar el servicio de estado:
   @inject EdcDataExplorerStateService DataExplorerState
   @inject NavigationManager NavigationManager

2. En la sección donde se muestra el contenido descargado (después de la transferencia exitosa), junto al botón "Exportar a fichero", añadir un nuevo botón:

   <RadzenButton Text="Explorar datos"
                  Icon="dashboard"
                  ButtonStyle="ButtonStyle.Secondary"
                  Click="@NavigateToDataExplorer"
                  Disabled="@(string.IsNullOrEmpty(_downloadedContent))"
                  Style="margin-left: 0.5rem" />

3. Método NavigateToDataExplorer:

   private void NavigateToDataExplorer()
   {
       // Cargar el estado compartido con los datos descargados
       DataExplorerState.JsonContent = _downloadedContent;
       DataExplorerState.ProviderName = _selectedProviderName; // variable existente con el nombre del proveedor
       DataExplorerState.DatasetName = _selectedDatasetName;   // variable existente con el nombre del dataset
       
       // Navegar al Data Explorer
       NavigationManager.NavigateTo("/ecodatanet/data-explorer");
   }

NOTA: Adaptar _downloadedContent, _selectedProviderName y _selectedDatasetName a los nombres reales de las variables que almacenan estos datos en ConsumeData.razor. Buscar en el código existente:
- El string con el contenido descargado (viene del EdcDataDownloadResponse.Content o similar).
- El nombre del proveedor seleccionado (viene del EdcProviderParsedCatalogDto o similar).
- El nombre del dataset seleccionado (viene del EdcDatasetDto.Name o similar).

VERIFICACIÓN: Al completar una transferencia exitosa, el botón "Explorar datos" está habilitado. Al pulsarlo, navega a /ecodatanet/data-explorer y muestra el dashboard generado.
```

---

## DE-7 — Menú lateral y permisos

```
FASE DE-7 — Menú lateral, policy y permisos

OBJETIVO: Registrar la nueva pantalla en el sistema de autorización y añadirla al menú lateral.

CAMBIOS:

1. **PolicyConstants.cs** (Domain/Authorization/PolicyConstants.cs):
   Añadir:
   public const string CanAccessEDCDataExplorer = "CanAccessEDCDataExplorer";

2. **Registro de la policy** (donde se registran las demás policies del módulo EcoDataNet, probablemente en Program.cs o en un método AddAuthorizationPolicies):
   Añadir la policy con el mismo patrón que CanAccessEDCConsumeData:
   options.AddPolicy(PolicyConstants.CanAccessEDCDataExplorer, policy =>
       policy.RequireAuthenticatedUser());

   NOTA: Como todas las pantallas EcoDataNet, la restricción real de acceso la controla el sistema dinámico PagePermissions. La policy es un mínimo de seguridad (usuario autenticado).

3. **NavMenu.razor** (Web/Components/Shared/NavMenu.razor o donde esté):
   En la sección del grupo "EcoDataNet", añadir un nuevo enlace DEBAJO de "Consumir datos":

   Explorar datos → /ecodatanet/data-explorer (icono: bi-graph-up o dashboard)

   Usar el mismo patrón de verificación de permisos que los otros enlaces del grupo:
   if (await PagePermissionService.CanAccessRouteAsync("/ecodatanet/data-explorer"))

4. **Actualizar _groupRoutes** en NavMenu.razor:
   _groupRoutes["EcoDataNet"] = new[] { 
       "/ecodatanet/connector-config", 
       "/ecodatanet/consume-data",
       "/ecodatanet/data-explorer"   // NUEVO
   };

5. **Configuración de permisos recomendada** (documentación):
   | Pantalla | ADMIN | SCRAP | PRODUCER | CARRIER | PLANT_OP | CAC_OP | PUBLIC_ENT | COORDINATOR | DISPATCH_OFFICE | REGULATOR | CERTIFIER |
   |---|---|---|---|---|---|---|---|---|---|---|---|
   | Explorar datos (Data Explorer) | Ambos | Lectura | Lectura | Sin acceso | Lectura | Sin acceso | Lectura | Lectura | Lectura | Lectura | Lectura |

   (Mismos permisos que "Consumir datos" — solo pueden explorar quienes pueden consumir)

VERIFICACIÓN: 
- La página /ecodatanet/data-explorer aparece en el auto-descubrimiento de PageDiscoveryService (highlighted en amarillo en /security/page-permissions hasta que el admin configure permisos).
- El enlace aparece en el menú lateral dentro del grupo EcoDataNet.
- Un usuario no autenticado es redirigido al login.
```

---

## DE-8 — Tests unitarios

```
FASE DE-8 — Tests unitarios

OBJETIVO: Verificar las heurísticas del analizador y del constructor de layout.

UBICACIÓN: GreenTransit.Tests/Features/EcoDataNet/DataExplorer/

--- ARCHIVO 1: JsonSchemaAnalyzerTests.cs ---

Tests con xUnit:

1. Analyze_ValidJsonWithScalarsAndArrays_ReturnsCorrectSchema:
   JSON: { "name": "Test", "total": 1500, "rate": 0.85, "items": [{"cat":"A","val":100},{"cat":"B","val":200}] }
   Assert: RootScalars.Count == 3, Arrays.Count == 1, Arrays[0].CategoryProperty == "cat", Arrays[0].NumericProperties contiene "val".

2. Analyze_JsonWithDateFields_DetectsTemporalProperty:
   JSON: { "data": [{"month":"2026-01","value":100},{"month":"2026-02","value":200}] }
   Assert: Arrays[0].TemporalProperty == "month".

3. Analyze_PercentageDetection_ByNameAndValue:
   JSON: { "recyclingRate": 0.87, "totalTons": 0.5, "percentage": 45.2 }
   Assert: recyclingRate → IsPercentage=true, totalTons → IsPercentage=false (nombre indica cantidad), percentage → IsPercentage=true.

4. Analyze_InvalidJson_ReturnsNull:
   JSON: "esto no es json {"
   Assert: resultado == null.

5. Analyze_EmptyObject_ReturnsEmptySchema:
   JSON: {}
   Assert: Success, RootScalars vacío, Arrays vacío.

6. Analyze_RootArray_SetsRootIsArray:
   JSON: [{"a":1},{"a":2}]
   Assert: RootIsArray == true, Arrays.Count == 1.

7. Analyze_NestedObject_CreatesObjectDescriptor:
   JSON: { "summary": { "total": 100, "average": 50.5 } }
   Assert: NestedObjects.Count == 1, NestedObjects[0].Properties.Count == 2.

--- ARCHIVO 2: DashboardLayoutBuilderTests.cs ---

1. Build_NumericRootScalars_GeneratesKpiCards:
   Schema con 3 RootScalars numéricos.
   Assert: 3 widgets de tipo KpiCard, ColumnSpan == 4.

2. Build_ArrayWithCategoryAndNumeric_GeneratesChartAndTable:
   Schema con 1 array (CategoryProperty + NumericProperties).
   Assert: al menos 1 Chart + 1 DataTable.

3. Build_ArrayWithTemporalAndNumeric_GeneratesLineChart:
   Schema con 1 array (TemporalProperty + NumericProperties).
   Assert: 1 Chart con ChartType==Line.

4. Build_NestedObject_GeneratesKeyValueList:
   Schema con 1 NestedObject con 5 propiedades.
   Assert: 1 KeyValueList.

5. Build_EmptySchema_ReturnsEmptyWidgets:
   Schema vacío.
   Assert: Widgets.Count == 0.

6. Build_WidgetsOrderedBySortOrder:
   Schema completo con KPIs + Charts + Tables.
   Assert: widgets[0].SortOrder < widgets[last].SortOrder, KPIs antes que Charts, Charts antes que Tables.

VERIFICACIÓN: Todos los tests pasan. dotnet test sin errores.
```

---

## 📁 RESUMEN DE ARCHIVOS A CREAR

| Capa | Archivo | Tipo |
|------|---------|------|
| Application | `Features/EcoDataNet/DTOs/DataExplorer/JsonPropertyDescriptor.cs` | DTO |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/JsonDataSchema.cs` | DTO |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/DynamicWidgetDescriptor.cs` | DTO |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/EdcDataExplorerResult.cs` | DTO |
| Application | `Features/EcoDataNet/Services/IJsonSchemaAnalyzer.cs` | Interfaz |
| Application | `Features/EcoDataNet/Services/JsonSchemaAnalyzer.cs` | Implementación |
| Application | `Features/EcoDataNet/Services/IDashboardLayoutBuilder.cs` | Interfaz |
| Application | `Features/EcoDataNet/Services/DashboardLayoutBuilder.cs` | Implementación |
| Application | `Features/EcoDataNet/Services/EdcDataExplorerStateService.cs` | Servicio Scoped |
| Application | `Features/EcoDataNet/Queries/AnalyzeEdcDataQuery.cs` | Query CQRS |
| Application | `Features/EcoDataNet/Queries/AnalyzeEdcDataQueryHandler.cs` | Handler |
| Domain | `Authorization/PolicyConstants.cs` | Modificar (añadir constante) |
| Web | `Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor` | Página principal |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicKpiCard.razor` | Componente |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicDataTable.razor` | Componente |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicChart.razor` | Componente |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicSectionHeader.razor` | Componente |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicKeyValueList.razor` | Componente |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicInfoText.razor` | Componente |
| Web | `Components/Pages/EcoDataNet/ConsumeData.razor` | Modificar (añadir botón) |
| Web | `Components/Shared/NavMenu.razor` | Modificar (añadir enlace) |
| Web | `Program.cs` o equivalente | Modificar (registrar DI + policy) |
| Tests | `Features/EcoDataNet/DataExplorer/JsonSchemaAnalyzerTests.cs` | Test unitario |
| Tests | `Features/EcoDataNet/DataExplorer/DashboardLayoutBuilderTests.cs` | Test unitario |

---

## 🔄 ESTRATEGIA DE EJECUCIÓN

1. **Sesión 1**: Ejecutar DE-0 + DE-1 + DE-2 (contexto + DTOs + analizador).
2. **Sesión 2**: Ejecutar DE-3 + DE-4 (builder + query CQRS + DI).
3. **Sesión 3**: Ejecutar DE-5 (todos los componentes Blazor).
4. **Sesión 4**: Ejecutar DE-6 + DE-7 (integración + menú + permisos).
5. **Sesión 5**: Ejecutar DE-8 (tests).

> Al inicio de cada sesión de Copilot, adjuntar este archivo + `COPILOT_CONTEXT.md` + `Mapa_Funcionalidades.md`.
> En la sesión 3, adjuntar también los archivos de los servicios creados en sesiones 1-2.
> En la sesión 4, adjuntar `ConsumeData.razor` y `NavMenu.razor`.

---

## ⚠️ NOTAS IMPORTANTES

1. **No se persisten datos del Data Explorer** en base de datos. Todo es procesamiento en memoria. Si el usuario cierra la página, pierde la visualización (debe volver a descargar desde ConsumeData).

2. **Límite de seguridad en el análisis**: el analizador debe limitar el procesamiento a JSONs de máximo 50 MB (comprobar `jsonContent.Length`). Si excede, devolver error descriptivo.

3. **Cultura de formato**: usar `CultureInfo("es-ES")` para formato de números (separador de miles = punto, separador decimal = coma).

4. **Los servicios IJsonSchemaAnalyzer y IDashboardLayoutBuilder** se ubican en Application (no en Infrastructure) porque son lógica pura sin dependencias de infraestructura. Solo usan System.Text.Json que es parte de .NET.

5. **Radzen Charts** requiere que las propiedades de datos sean accesibles por reflexión. Los `Dictionary<string,object?>` no funcionan directamente como DataSource de RadzenChart. El componente DynamicChart debe transformar los datos a una lista de objetos con propiedades concretas (usando `ExpandoObject` o una clase `ChartDataItem`). Verificar que la aproximación con `ChartDataItem` funciona con `RadzenColumnSeries<ChartDataItem>`.

6. **Responsive**: los ColumnSpan del grid deben adaptarse a pantallas pequeñas. Añadir media queries en el CSS del EdcDataExplorer.razor:
   - En pantallas < 768px: todos los widgets a ColumnSpan=12 (full width).
   - En pantallas 768-1024px: KpiCards a ColumnSpan=6, el resto a 12.

7. **Modo oscuro/claro**: los componentes usan variables CSS `--gt-*` que ya están definidas para ambos modos en el proyecto. No hardcodear colores; usar siempre las variables excepto para los KpiColor que vienen de la paleta.

8. **Futura mejora (NO implementar ahora)**: permitir al usuario personalizar el layout (reordenar widgets, cambiar tipo de gráfico, ocultar widgets). Esto requeriría persistencia y un editor drag-and-drop.
