using System.Globalization;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Construye el layout de widgets dinámicos a partir de un JsonDataSchema.
/// Aplica heurísticas para asignar el tipo de widget más adecuado a cada dato.
/// </summary>
public class DashboardLayoutBuilder : IDashboardLayoutBuilder
{
    private static readonly string[] KpiColors =
    [
        "#0A404B", "#8ACCC3", "#D8B00E", "#D36F15",
        "#C13E43", "#6E4583", "#535497", "#B4B736"
    ];

    private static readonly CultureInfo EsCulture = new("es-ES");

    /// <inheritdoc />
    public List<DynamicWidgetDescriptor> Build(JsonDataSchema schema)
    {
        var widgets = new List<DynamicWidgetDescriptor>();
        int colorIndex = 0;

        // ── REGLA 1: Cabecera de contexto (SortOrder=0) ──────────────────
        var contextStrings = schema.RootScalars
            .Where(p => p.PropertyType is JsonPropertyType.String or JsonPropertyType.DateTime)
            .ToList();

        if (contextStrings.Count > 0)
        {
            var kvPairs = contextStrings.ToDictionary(
                p => p.DisplayName,
                p => p.SampleValues.FirstOrDefault() ?? string.Empty);

            widgets.Add(new DynamicWidgetDescriptor
            {
                Type         = WidgetType.SectionHeader,
                Title        = "Información del dataset",
                SortOrder    = 0,
                ColumnSpan   = 12,
                KeyValuePairs = kvPairs
            });
        }

        // ── REGLA 2: KPI Cards (SortOrder=10..19) ─────────────────────────
        var numericScalars = schema.RootScalars
            .Where(p => p.PropertyType == JsonPropertyType.Number)
            .ToList();

        if (numericScalars.Count > 0)
        {
            int kpiSpan = numericScalars.Count switch
            {
                1 => 12,
                2 => 6,
                3 => 4,
                _ => 3
            };

            for (int i = 0; i < numericScalars.Count; i++)
            {
                var prop = numericScalars[i];
                double.TryParse(prop.SampleValues.FirstOrDefault(), out var rawVal);
                var color = KpiColors[colorIndex++ % KpiColors.Length];

                widgets.Add(new DynamicWidgetDescriptor
                {
                    Type           = WidgetType.KpiCard,
                    Title          = prop.DisplayName,
                    SortOrder      = 10 + i,
                    ColumnSpan     = kpiSpan,
                    KpiNumericValue = rawVal,
                    KpiValue       = FormatKpiValue(rawVal, prop.IsPercentage),
                    KpiUnit        = prop.IsPercentage ? "%" : string.Empty,
                    KpiIcon        = prop.SuggestedIcon ?? "analytics",
                    KpiColor       = color
                });
            }
        }

        // ── REGLA 7: InfoText para strings largas del nivel raíz ──────────
        foreach (var longStr in schema.RootScalars
            .Where(p => p.PropertyType == JsonPropertyType.String
                        && (p.SampleValues.FirstOrDefault()?.Length ?? 0) > 200))
        {
            widgets.Add(new DynamicWidgetDescriptor
            {
                Type        = WidgetType.InfoText,
                Title       = longStr.DisplayName,
                SortOrder   = 60 + widgets.Count,
                ColumnSpan  = 12,
                TextContent = longStr.SampleValues.FirstOrDefault()
            });
        }

        // ── REGLAS 3-5: Arrays ────────────────────────────────────────────
        int chartOrder = 20;
        int tableOrder = 40;

        foreach (var array in schema.Arrays)
        {
            bool hasChart = false;

            // REGLA 3: Temporal + numérico → Line/Area
            if (array.TemporalProperty != null && array.NumericProperties.Count > 0)
            {
                var valueFields = array.NumericProperties.Take(3).ToList();

                // Calcular claves únicas sin truncar para decidir si conviene agrupar por mes
                int rawKeys = array.RawData
                    .Select(r => r.TryGetValue(array.TemporalProperty, out var v) ? v?.ToString() : null)
                    .Where(v => v != null)
                    .Distinct()
                    .Count();

                bool truncateToMonth = rawKeys > 30;

                var chartData = AggregateForChart(
                    array.RawData,
                    array.TemporalProperty,
                    valueFields,
                    truncateToMonth);

                widgets.Add(new DynamicWidgetDescriptor
                {
                    Type               = WidgetType.Chart,
                    Title              = array.DisplayName,
                    SortOrder          = chartOrder++,
                    ColumnSpan         = schema.Arrays.Count > 1 ? 6 : 12,
                    ChartType          = valueFields.Count == 1 ? ChartSubType.Area : ChartSubType.Line,
                    ChartCategoryField = array.TemporalProperty,
                    ChartValueFields   = valueFields,
                    ChartData          = chartData
                });
                // Poblar metadatos de campos disponibles para personalización
                PopulateChartFieldMetadata(widgets[^1], array);
                hasChart = true;
            }
            // REGLA 4: Categoría + numérico (sin temporal) → Donut/Bar
            else if (array.CategoryProperty != null && array.NumericProperties.Count > 0)
            {
                var valueFields = array.NumericProperties.Take(3).ToList();

                var chartData = AggregateForChart(
                    array.RawData,
                    array.CategoryProperty,
                    valueFields,
                    truncateToMonth: false);

                bool useDonut = chartData.Count <= 7 && valueFields.Count == 1;

                widgets.Add(new DynamicWidgetDescriptor
                {
                    Type               = WidgetType.Chart,
                    Title              = array.DisplayName,
                    SortOrder          = chartOrder++,
                    ColumnSpan         = 6,
                    ChartType          = useDonut ? ChartSubType.Donut : ChartSubType.BarVertical,
                    ChartCategoryField = array.CategoryProperty,
                    ChartValueFields   = valueFields,
                    ChartData          = chartData
                });
                // Poblar metadatos de campos disponibles para personalización
                PopulateChartFieldMetadata(widgets[^1], array);
                hasChart = true;
            }

            // REGLA 8: Arrays de valores simples sin categoría numérica
            if (!hasChart && array.ItemProperties.Count == 1)
            {
                var onlyProp = array.ItemProperties[0];
                if (onlyProp.PropertyType == JsonPropertyType.Number)
                {
                    widgets.Add(new DynamicWidgetDescriptor
                    {
                        Type               = WidgetType.Chart,
                        Title              = array.DisplayName,
                        SortOrder          = chartOrder++,
                        ColumnSpan         = 6,
                        ChartType          = ChartSubType.BarVertical,
                        ChartCategoryField = "index",
                        ChartValueFields   = ["value"],
                        ChartData          = array.RawData.Select((r, i) =>
                        {
                            var d = new Dictionary<string, object?>(r) { ["index"] = i.ToString() };
                            return d;
                        }).ToList()
                    });
                }
                else if (onlyProp.PropertyType == JsonPropertyType.String)
                {
                    var values = array.RawData
                        .Select(r => r.TryGetValue("value", out var v) ? v?.ToString() : null)
                        .Where(v => v != null);

                    widgets.Add(new DynamicWidgetDescriptor
                    {
                        Type        = WidgetType.InfoText,
                        Title       = array.DisplayName,
                        SortOrder   = 60 + widgets.Count,
                        ColumnSpan  = 12,
                        TextContent = string.Join(", ", values)
                    });
                }
            }

            // REGLA 5: Tabla para todos los arrays homogéneos
            if (array.IsHomogeneous && array.ItemProperties.Count > 0)
            {
                var columns = array.ItemProperties.Select(p => new TableColumnDescriptor
                {
                    PropertyName = p.Name,
                    Title        = p.DisplayName,
                    DataType     = GetColumnDataType(p),
                    FormatString = GetFormatString(p)
                }).ToList();

                widgets.Add(new DynamicWidgetDescriptor
                {
                    Type         = WidgetType.DataTable,
                    Title        = array.DisplayName,
                    SortOrder    = tableOrder++,
                    ColumnSpan   = 12,
                    TableColumns = columns,
                    TableData    = array.RawData
                });
            }
            else if (!array.IsHomogeneous && array.ItemProperties.Count > 0)
            {
                // Array no homogéneo → tabla con superconjunto de propiedades
                var allKeys = array.RawData
                    .SelectMany(r => r.Keys)
                    .Distinct()
                    .ToList();

                var columns = allKeys.Select(k => new TableColumnDescriptor
                {
                    PropertyName = k,
                    Title        = JsonSchemaAnalyzer.HumanizePropertyName(k),
                    DataType     = "String"
                }).ToList();

                widgets.Add(new DynamicWidgetDescriptor
                {
                    Type         = WidgetType.DataTable,
                    Title        = array.DisplayName,
                    SortOrder    = tableOrder++,
                    ColumnSpan   = 12,
                    TableColumns = columns,
                    TableData    = array.RawData
                });
            }
        }

        // ── REGLA 6: Objetos anidados → KeyValueList o DataTable ──────────
        int kvOrder = 50;
        foreach (var obj in schema.NestedObjects)
        {
            if (obj.Properties.Count <= 10)
            {
                var kvPairs = obj.Properties.ToDictionary(
                    p => p.DisplayName,
                    p => p.SampleValues.FirstOrDefault() ?? string.Empty);

                widgets.Add(new DynamicWidgetDescriptor
                {
                    Type          = WidgetType.KeyValueList,
                    Title         = obj.DisplayName,
                    SortOrder     = kvOrder++,
                    ColumnSpan    = 6,
                    KeyValuePairs = kvPairs
                });
            }
            else
            {
                // Más de 10 propiedades → tabla de 2 columnas
                var rows = obj.Properties.Select(p => new Dictionary<string, object?>
                {
                    ["Propiedad"] = p.DisplayName,
                    ["Valor"]     = p.SampleValues.FirstOrDefault() ?? string.Empty
                }).ToList();

                widgets.Add(new DynamicWidgetDescriptor
                {
                    Type         = WidgetType.DataTable,
                    Title        = obj.DisplayName,
                    SortOrder    = kvOrder++,
                    ColumnSpan   = 6,
                    TableColumns =
                    [
                        new TableColumnDescriptor { PropertyName = "Propiedad", Title = "Propiedad", DataType = "String" },
                        new TableColumnDescriptor { PropertyName = "Valor",     Title = "Valor",     DataType = "String" }
                    ],
                    TableData = rows
                });
            }

            // Sub-arrays del objeto anidado
            foreach (var childArray in obj.ChildArrays)
            {
                if (childArray.IsHomogeneous && childArray.ItemProperties.Count > 0)
                {
                    var columns = childArray.ItemProperties.Select(p => new TableColumnDescriptor
                    {
                        PropertyName = p.Name,
                        Title        = p.DisplayName,
                        DataType     = GetColumnDataType(p),
                        FormatString = GetFormatString(p)
                    }).ToList();

                    widgets.Add(new DynamicWidgetDescriptor
                    {
                        Type         = WidgetType.DataTable,
                        Title        = $"{obj.DisplayName} — {childArray.DisplayName}",
                        SortOrder    = tableOrder++,
                        ColumnSpan   = 12,
                        TableColumns = columns,
                        TableData    = childArray.RawData
                    });
                }
            }
        }

        // ── REGLA 9: Ordenar y asignar WidgetIds determinísticos ─────────
        var ordered = widgets.OrderBy(w => w.SortOrder).ToList();

        // Asignar IDs determinísticos únicos resolviendo colisiones por baseId
        var idCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var w in ordered)
        {
            var baseId = GenerateDeterministicId(w);
            if (!idCounters.TryGetValue(baseId, out int count))
            {
                w.WidgetId = baseId;
                idCounters[baseId] = 1;
            }
            else
            {
                w.WidgetId = $"{baseId}_{count}";
                idCounters[baseId] = count + 1;
            }
        }

        return ordered;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string FormatKpiValue(double value, bool isPercentage)
    {
        if (isPercentage)
        {
            double displayVal = value is >= 0.0 and <= 1.0 ? value * 100 : value;
            return displayVal.ToString("N1", EsCulture);
        }

        if (value == Math.Floor(value) && value > 1000)
            return ((long)value).ToString("N0", EsCulture);

        return value.ToString("N2", EsCulture);
    }

    private static string GetColumnDataType(JsonPropertyDescriptor prop)
    {
        if (prop.IsPercentage)     return "Percentage";
        if (prop.PropertyType == JsonPropertyType.Number)   return "Number";
        if (prop.PropertyType == JsonPropertyType.DateTime) return "DateTime";
        if (prop.PropertyType == JsonPropertyType.Boolean)  return "Boolean";
        return "String";
    }

    private static string? GetFormatString(JsonPropertyDescriptor prop)
    {
        if (prop.IsPercentage)                              return "N1";
        if (prop.PropertyType == JsonPropertyType.Number)  return "N2";
        if (prop.PropertyType == JsonPropertyType.DateTime) return "dd/MM/yyyy";
        return null;
    }

    // ── Agregación de datos para gráficos ─────────────────────────────────

    private const int MaxChartPoints = 40;

    /// <summary>
    /// Agrupa las filas por <paramref name="categoryField"/> sumando los campos numéricos.
    /// Si el número de claves distintas supera <paramref name="truncateToMonth"/>,
    /// la clave se trunca a "yyyy-MM" para agrupar por mes.
    /// El resultado se ordena y limita a <see cref="MaxChartPoints"/>.
    /// </summary>
    private static List<Dictionary<string, object?>> AggregateForChart(
        List<Dictionary<string, object?>> rawData,
        string categoryField,
        List<string> valueFields,
        bool truncateToMonth = false)
    {
        if (rawData.Count == 0) return rawData;

        // Función de clave: truncar a mes si procede
        string KeyOf(Dictionary<string, object?> row)
        {
            var raw = row.TryGetValue(categoryField, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
            if (!truncateToMonth) return raw;

            // Intentar parsear como fecha ISO y devolver "yyyy-MM"
            if (raw.Length >= 7 && raw[4] == '-')
                return raw[..7];   // "2024-05-12" → "2024-05"
            if (DateTime.TryParse(raw, out var dt))
                return dt.ToString("yyyy-MM");
            return raw;
        }

        // Agrupar y sumar
        var groups = rawData
            .GroupBy(KeyOf)
            .Select(g =>
            {
                var row = new Dictionary<string, object?> { [categoryField] = g.Key };
                foreach (var field in valueFields)
                {
                    var sum = g.Sum(r =>
                    {
                        if (!r.TryGetValue(field, out var fv)) return 0.0;
                        return fv is double d ? d : double.TryParse(fv?.ToString(), out var p) ? p : 0.0;
                    });
                    row[field] = sum;
                }
                return row;
            })
            .OrderBy(r => r[categoryField]?.ToString())
            .Take(MaxChartPoints)
            .ToList();

        return groups;
    }

    /// <summary>
    /// Genera un identificador determinístico para un widget a partir de su tipo y título.
    /// </summary>
    private static string GenerateDeterministicId(DynamicWidgetDescriptor widget)
    {
        var slug = widget.Title
            .ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("/", "_")
            .Replace("(", "")
            .Replace(")", "");

        // Limitar longitud del slug
        if (slug.Length > 40) slug = slug[..40];

        return widget.Type switch
        {
            WidgetType.KpiCard       => $"kpi_{slug}",
            WidgetType.Chart         => $"chart_{slug}",
            WidgetType.DataTable     => $"table_{slug}",
            WidgetType.SectionHeader => "header_root",
            WidgetType.KeyValueList  => $"kvlist_{slug}",
            WidgetType.InfoText      => $"info_{slug}",
            _                        => $"widget_{slug}"
        };
    }

    /// <summary>
    /// Construye los metadatos de campos disponibles para la personalización de un widget Chart.
    /// Pobla AvailableCategoryFields, AvailableValueFields, FieldDisplayNames y SourceArrayName.
    /// </summary>
    private static void PopulateChartFieldMetadata(
        DynamicWidgetDescriptor widget,
        JsonArrayDescriptor array)
    {
        widget.SourceArrayName = array.Name;

        // Campos candidatos a categoría: strings y temporales (incluyendo strings con formato de fecha)
        var categoryFields = array.ItemProperties
            .Where(p => p.PropertyType is JsonPropertyType.String or JsonPropertyType.DateTime)
            .Select(p => p.Name)
            .ToList();

        // Ordenar: el campo actual de categoría primero, luego el resto alfabéticamente
        if (widget.ChartCategoryField != null && categoryFields.Contains(widget.ChartCategoryField))
        {
            categoryFields = categoryFields
                .OrderBy(f => f == widget.ChartCategoryField ? 0 : 1)
                .ThenBy(f => f)
                .ToList();
        }
        else
        {
            categoryFields = categoryFields.OrderBy(f => f).ToList();
        }
        widget.AvailableCategoryFields = categoryFields;

        // Campos candidatos a valores: numéricos
        var valueFields = array.ItemProperties
            .Where(p => p.PropertyType == JsonPropertyType.Number)
            .Select(p => p.Name)
            .ToList();

        // Ordenar: campos ya usados como series primero, luego el resto
        var currentValueFields = widget.ChartValueFields ?? [];
        valueFields = valueFields
            .OrderBy(f => currentValueFields.Contains(f) ? 0 : 1)
            .ThenBy(f => f)
            .ToList();
        widget.AvailableValueFields = valueFields;

        // Nombres humanizados de todas las propiedades (categoría + valor)
        widget.FieldDisplayNames = array.ItemProperties.ToDictionary(
            p => p.Name,
            p => p.DisplayName);
    }
}
