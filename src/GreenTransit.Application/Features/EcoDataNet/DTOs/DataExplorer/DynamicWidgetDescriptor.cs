namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Describe un widget individual a renderizar en el dashboard dinámico.
/// </summary>
public class DynamicWidgetDescriptor
{
    /// <summary>Identificador único del widget. Se asigna de forma determinística en DashboardLayoutBuilder.</summary>
    public string WidgetId { get; set; } = string.Empty;

    /// <summary>Ruta JSON del dato fuente (para generar WidgetId determinístico).</summary>
    public string SourceJsonPath { get; set; } = string.Empty;

    /// <summary>True si el usuario ha ocultado este widget. La UI decide si lo oculta o lo muestra en gris.</summary>
    public bool IsHidden { get; set; }

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

    // --- Metadatos de campos disponibles para personalización de gráfico ---

    /// <summary>
    /// Nombres de todas las propiedades de tipo String o DateTime del array fuente,
    /// candidatas a usarse como eje de categorías (eje X, segmentos).
    /// Se puebla en DashboardLayoutBuilder. Usado por WidgetConfigPanel para el dropdown.
    /// </summary>
    public List<string>? AvailableCategoryFields { get; set; }

    /// <summary>
    /// Nombres de todas las propiedades numéricas del array fuente,
    /// candidatas a usarse como eje de valores (eje Y, series, tamaño de segmento).
    /// Se puebla en DashboardLayoutBuilder. Usado por WidgetConfigPanel para el dropdown.
    /// </summary>
    public List<string>? AvailableValueFields { get; set; }

    /// <summary>
    /// Nombres humanizados de las propiedades disponibles, indexados por nombre de propiedad.
    /// Permite mostrar "Total Tons Processed" en lugar de "totalTonsProcessed" en los dropdowns.
    /// </summary>
    public Dictionary<string, string>? FieldDisplayNames { get; set; }

    /// <summary>
    /// Nombre del array fuente del JSON que alimenta este gráfico.
    /// Se usa para identificar qué array se debe leer si se cambian los campos.
    /// </summary>
    public string? SourceArrayName { get; set; }

    // --- Datos para Mapa ---

    /// <summary>Nombre de la propiedad del array que contiene la latitud.</summary>
    public string? MapLatitudeField { get; set; }

    /// <summary>Nombre de la propiedad del array que contiene la longitud.</summary>
    public string? MapLongitudeField { get; set; }

    /// <summary>
    /// Nombre de la propiedad del array que se usa como título del marcador/punto en el mapa.
    /// Null = usar el primer campo string disponible, o el índice del elemento.
    /// </summary>
    public string? MapTitleField { get; set; }

    /// <summary>
    /// Lista de nombres de propiedades del array que se muestran en el tooltip/popup al hacer clic en un punto.
    /// Null o vacío = mostrar todos los campos del elemento.
    /// </summary>
    public List<string>? MapTooltipFields { get; set; }

    /// <summary>
    /// Datos del mapa: lista de diccionarios con al menos las propiedades MapLatitudeField y MapLongitudeField.
    /// </summary>
    public List<Dictionary<string, object?>>? MapData { get; set; }

    /// <summary>
    /// Lista de todos los campos string del array fuente, para que la UI muestre opciones
    /// de "campo título" y "campos tooltip" en el panel de configuración.
    /// </summary>
    public List<string>? MapAvailableStringFields { get; set; }

    /// <summary>
    /// Lista de todos los campos del array fuente (string + numéricos), para opciones de tooltip.
    /// </summary>
    public List<string>? MapAvailableAllFields { get; set; }

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
    InfoText,
    /// <summary>Mapa de puntos geográficos (lat/lon) extraídos del JSON.</summary>
    Map
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

    /// <summary>True si el usuario ha ocultado esta columna. La UI no la renderiza.</summary>
    public bool IsHidden { get; set; }
}
