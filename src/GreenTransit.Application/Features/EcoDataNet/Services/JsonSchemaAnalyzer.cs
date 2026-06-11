using System.Text.Json;
using System.Text.RegularExpressions;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Implementación de IJsonSchemaAnalyzer usando System.Text.Json.
/// Analiza la estructura de un JSON arbitrario y produce un JsonDataSchema.
/// </summary>
public class JsonSchemaAnalyzer : IJsonSchemaAnalyzer
{
    private readonly ILogger<JsonSchemaAnalyzer> _logger;

    public JsonSchemaAnalyzer(ILogger<JsonSchemaAnalyzer> logger)
    {
        _logger = logger;
    }

    private static readonly Regex IsoDateRegex = new(@"^\d{4}-\d{2}(-\d{2})?", RegexOptions.Compiled);
    /// <summary>Máx. elementos del array usados para inferir el esquema y datos del gráfico.</summary>
    private const int MaxArrayItems = 500;
    /// <summary>Máx. filas que se materializan en RawData para la tabla (paginación en cliente).</summary>
    private const int MaxTableRows  = 500;

    private static readonly string[] TemporalKeywords =
        ["date", "fecha", "month", "mes", "year", "año", "period", "periodo",
         "quarter", "trimestre", "time", "timestamp", "week", "semana"];

    /// <summary>
    /// Propiedades de infraestructura/tenant que se omiten siempre del análisis visual.
    /// La comparación es case-insensitive.
    /// </summary>
    private static readonly HashSet<string> ExcludedProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "OwnerId", "TenantId", "CreatedBy", "UpdatedBy",
            "CreatedAt", "UpdatedAt", "DeletedAt", "IsDeleted",
            "RowVersion", "ConcurrencyStamp"
        };

    private static readonly string[] PercentageKeywords =
        ["rate", "ratio", "percent", "percentage", "tasa", "porcentaje", "pct", "share"];

    private static readonly string[] AbsoluteQuantityKeywords =
        ["count", "total", "amount", "quantity", "tons", "kg", "units"];

    /// <inheritdoc />
    public JsonDataSchema? Analyze(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var schema = new JsonDataSchema();

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                schema.RootIsArray = true;
                var arrayDesc = ProcessArray("root", "root", doc.RootElement);
                if (arrayDesc != null)
                    schema.Arrays.Add(arrayDesc);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                ProcessObject(doc.RootElement, schema, "root", depth: 0);
            }

            schema.TotalPropertyCount =
                schema.RootScalars.Count
                + schema.Arrays.Sum(a => a.ItemProperties.Count)
                + schema.NestedObjects.Sum(o => o.Properties.Count);

            return schema;
        }
        catch (Exception ex)
        {
            var head = jsonContent.Length > 200 ? jsonContent[..200] : jsonContent;
            var tail = jsonContent.Length > 200 ? jsonContent[^Math.Min(100, jsonContent.Length)..] : string.Empty;
            _logger.LogWarning(ex,
                "JsonSchemaAnalyzer: fallo ({ExType}) — inicio: {Head} | fin: {Tail}",
                ex.GetType().Name, head, tail);
            return null;
        }
    }

    // ── Procesamiento de objeto ────────────────────────────────────────────

    private void ProcessObject(JsonElement element, JsonDataSchema schema, string path, int depth)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (ExcludedProperties.Contains(prop.Name)) continue;

            var propPath = $"{path}.{prop.Name}";
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    schema.RootScalars.Add(BuildScalarDescriptor(prop.Name, propPath, prop.Value));
                    break;

                case JsonValueKind.Number:
                    schema.RootScalars.Add(BuildScalarDescriptor(prop.Name, propPath, prop.Value));
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    schema.RootScalars.Add(new JsonPropertyDescriptor
                    {
                        Name = prop.Name,
                        DisplayName = HumanizePropertyName(prop.Name),
                        JsonPath = propPath,
                        PropertyType = JsonPropertyType.Boolean,
                        SuggestedIcon = "toggle_on"
                    });
                    break;

                case JsonValueKind.Array when depth < 2:
                    var arrayDesc = ProcessArray(prop.Name, propPath, prop.Value);
                    if (arrayDesc != null)
                        schema.Arrays.Add(arrayDesc);
                    break;

                case JsonValueKind.Object when depth < 2:
                    var objDesc = ProcessNestedObject(prop.Name, propPath, prop.Value, depth + 1);
                    if (objDesc != null)
                        schema.NestedObjects.Add(objDesc);
                    break;
            }
        }
    }

    // ── Construcción de descriptor escalar ────────────────────────────────

    private static JsonPropertyDescriptor BuildScalarDescriptor(string name, string path, JsonElement value)
    {
        var desc = new JsonPropertyDescriptor
        {
            Name = name,
            DisplayName = HumanizePropertyName(name),
            JsonPath = path,
            SuggestedIcon = SuggestIcon(name)
        };

        if (value.ValueKind == JsonValueKind.String)
        {
            var strVal = value.GetString() ?? string.Empty;
            desc.IsDate = IsoDateRegex.IsMatch(strVal);
            desc.PropertyType = desc.IsDate ? JsonPropertyType.DateTime : JsonPropertyType.String;
            desc.SampleValues.Add(strVal);
        }
        else if (value.ValueKind == JsonValueKind.Number)
        {
            desc.PropertyType = JsonPropertyType.Number;
            value.TryGetDouble(out var numVal);
            desc.SampleValues.Add(numVal.ToString());
            desc.IsPercentage = DetectPercentage(name, numVal);
        }

        return desc;
    }

    // ── Procesamiento de array ─────────────────────────────────────────────

    private static JsonArrayDescriptor? ProcessArray(string name, string path, JsonElement arrayElement)
    {
        var items = arrayElement.EnumerateArray().Take(MaxArrayItems).ToList();
        if (items.Count == 0)
            return null;

        var desc = new JsonArrayDescriptor
        {
            Name = name,
            DisplayName = HumanizePropertyName(name),
            JsonPath = path,
            ItemCount = arrayElement.GetArrayLength()
        };

        // Array de valores simples
        if (items[0].ValueKind != JsonValueKind.Object)
        {
            var simpleProp = new JsonPropertyDescriptor
            {
                Name = "value",
                DisplayName = "Value",
                JsonPath = $"{path}[].value",
                PropertyType = items[0].ValueKind == JsonValueKind.Number ? JsonPropertyType.Number : JsonPropertyType.String
            };
            desc.ItemProperties.Add(simpleProp);
            desc.IsHomogeneous = true;
            desc.RawData = items.Select((v, i) =>
                new Dictionary<string, object?> { ["value"] = GetRawValue(v) }).ToList();

            if (simpleProp.PropertyType == JsonPropertyType.Number)
                desc.NumericProperties.Add("value");

            return desc;
        }

        // Array de objetos — analizar esquema del primer elemento
        var firstProps = items[0].EnumerateObject().Select(p => p.Name).ToHashSet();

        // Verificar homogeneidad con los siguientes 10 elementos (ignorar items no-objeto, p.ej. null)
        var sampleCount = Math.Min(items.Count, 10);
        var matchCount = items.Take(sampleCount).Count(item =>
        {
            if (item.ValueKind != JsonValueKind.Object) return false;
            var itemProps = item.EnumerateObject().Select(p => p.Name).ToHashSet();
            return itemProps.Count == firstProps.Count && firstProps.All(p => itemProps.Contains(p));
        });
        desc.IsHomogeneous = sampleCount == 0 || (double)matchCount / sampleCount >= 0.8;

        // Construir esquema de propiedades del item
        foreach (var prop in items[0].EnumerateObject())
        {
            if (ExcludedProperties.Contains(prop.Name)) continue;

            var itemPropPath = $"{path}[].{prop.Name}";
            var itemPropDesc = new JsonPropertyDescriptor
            {
                Name = prop.Name,
                DisplayName = HumanizePropertyName(prop.Name),
                JsonPath = itemPropPath,
                SuggestedIcon = SuggestIcon(prop.Name)
            };

            // Usar el primer valor no-nulo del sample para inferir el tipo real del campo
            JsonElement firstNonNull = prop.Value;
            foreach (var sampleItem in items.Take(sampleCount))
            {
                if (sampleItem.ValueKind != JsonValueKind.Object) continue;
                if (sampleItem.TryGetProperty(prop.Name, out var candidate)
                    && candidate.ValueKind != JsonValueKind.Null
                    && candidate.ValueKind != JsonValueKind.Undefined)
                {
                    firstNonNull = candidate;
                    break;
                }
            }

            if (firstNonNull.ValueKind == JsonValueKind.Number)
            {
                itemPropDesc.PropertyType = JsonPropertyType.Number;
                firstNonNull.TryGetDouble(out var dVal);
                itemPropDesc.IsPercentage = DetectPercentage(prop.Name, dVal);
            }
            else if (firstNonNull.ValueKind == JsonValueKind.String)
            {
                var sv = firstNonNull.GetString() ?? string.Empty;
                itemPropDesc.IsDate = IsoDateRegex.IsMatch(sv);
                itemPropDesc.PropertyType = itemPropDesc.IsDate ? JsonPropertyType.DateTime : JsonPropertyType.String;
            }
            else if (firstNonNull.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                itemPropDesc.PropertyType = JsonPropertyType.Boolean;
            }
            else if (firstNonNull.ValueKind == JsonValueKind.Null)
            {
                // Campo siempre nulo en todos los samples: no lo clasificamos como String
                itemPropDesc.PropertyType = JsonPropertyType.Null;
            }

            // Hasta 5 valores de ejemplo
            itemPropDesc.SampleValues = items
                .Take(5)
                .Where(i => i.ValueKind == JsonValueKind.Object && i.TryGetProperty(prop.Name, out _))
                .Select(i => { i.TryGetProperty(prop.Name, out var pv); return pv.ToString(); })
                .ToList();

            desc.ItemProperties.Add(itemPropDesc);
        }

        // Identificar propiedades especiales
        desc.NumericProperties = desc.ItemProperties
            .Where(p => p.PropertyType == JsonPropertyType.Number)
            .Select(p => p.Name)
            .ToList();

        desc.TemporalProperty = desc.ItemProperties.FirstOrDefault(p =>
            p.PropertyType == JsonPropertyType.DateTime
            || TemporalKeywords.Any(kw => p.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            ?.Name;

        // UniqueValueCount para strings (candidatos a categoría)
        foreach (var strProp in desc.ItemProperties.Where(p => p.PropertyType == JsonPropertyType.String))
        {
            var uniqueVals = items
                .Where(i => i.ValueKind == JsonValueKind.Object && i.TryGetProperty(strProp.Name, out _))
                .Select(i => { i.TryGetProperty(strProp.Name, out var pv); return pv.GetString(); })
                .Distinct()
                .Count();
            strProp.UniqueValueCount = uniqueVals;
        }

        desc.CategoryProperty = desc.ItemProperties
            .Where(p => p.PropertyType == JsonPropertyType.String && p.UniqueValueCount is <= 20)
            .FirstOrDefault()?.Name;

        // RawData: limitado a MaxTableRows para no materializar miles de diccionarios
        desc.RawData = items.Take(MaxTableRows)
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item =>
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in item.EnumerateObject())
                {
                    if (!ExcludedProperties.Contains(prop.Name))
                        dict[prop.Name] = GetRawValue(prop.Value);
                }
                return dict;
            }).ToList();

        // Detección de coordenadas geográficas (lat/lon)
        DetectGeoCoordinates(desc, items);

        return desc;
    }

    // ── Detección de coordenadas geográficas ─────────────────────────────

    private static void DetectGeoCoordinates(JsonArrayDescriptor desc, List<JsonElement> items)
    {
        string? latCandidate = null;
        string? lonCandidate = null;

        foreach (var prop in desc.ItemProperties.Where(p =>
            p.PropertyType == JsonPropertyType.Number ||
            p.PropertyType == JsonPropertyType.String))
        {
            var lower = prop.Name.ToLowerInvariant();

            if (latCandidate == null && IsLatitudeField(lower))
            {
                // Validar rango con muestras
                if (ValidateGeoRange(prop.Name, items, -90, 90))
                    latCandidate = prop.Name;
            }
            else if (lonCandidate == null && IsLongitudeField(lower))
            {
                if (ValidateGeoRange(prop.Name, items, -180, 180))
                    lonCandidate = prop.Name;
            }
        }

        desc.LatitudeProperty  = latCandidate;
        desc.LongitudeProperty = lonCandidate;
    }

    private static bool IsLatitudeField(string lower)
    {
        // Nombres exactos
        if (lower is "lat" or "latitude" or "latitud") return true;
        // Empieza por "lat" seguido de "_", "i" o "u" (latitud, latitude, lat_deg…) pero no "lateral", "platform"
        if (lower.StartsWith("lat") && lower.Length > 3)
        {
            char next = lower[3];
            return next == '_' || next == 'i' || next == 'u';
        }
        return false;
    }

    private static bool IsLongitudeField(string lower)
    {
        if (lower is "lon" or "lng" or "longitude" or "longitud") return true;
        if (lower.StartsWith("lon") && lower.Length > 3)
        {
            char next = lower[3];
            return next == '_' || next == 'g'; // "lon_" o "long…" (longitude)
        }
        return false;
    }

    private static bool ValidateGeoRange(string propName, List<JsonElement> items, double min, double max)
    {
        var samples = new List<double>();
        foreach (var item in items.Take(20))
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (item.TryGetProperty(propName, out var pv))
            {
                double d;
                if (pv.ValueKind == JsonValueKind.Number && pv.TryGetDouble(out d))
                {
                    samples.Add(d);
                }
                else if (pv.ValueKind == JsonValueKind.String
                    && double.TryParse(pv.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out d))
                {
                    samples.Add(d);
                }
            }
        }

        if (samples.Count == 0) return true; // sin muestras, asumir válido

        int inRange = samples.Count(v => v >= min && v <= max);
        return (double)inRange / samples.Count >= 0.5;
    }

    // ── Procesamiento de objeto anidado ───────────────────────────────────

    private static JsonObjectDescriptor? ProcessNestedObject(string name, string path, JsonElement element, int depth)
    {
        var objDesc = new JsonObjectDescriptor
        {
            Name = name,
            DisplayName = HumanizePropertyName(name),
            JsonPath = path
        };

        foreach (var prop in element.EnumerateObject())
        {
            var propPath = $"{path}.{prop.Name}";
            if (prop.Value.ValueKind == JsonValueKind.Array && depth < 2)
            {
                var childArray = ProcessArray(prop.Name, propPath, prop.Value);
                if (childArray != null)
                    objDesc.ChildArrays.Add(childArray);
            }
            else if (prop.Value.ValueKind != JsonValueKind.Object && prop.Value.ValueKind != JsonValueKind.Array)
            {
                objDesc.Properties.Add(BuildScalarDescriptor(prop.Name, propPath, prop.Value));
            }
        }

        return objDesc;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    internal static string HumanizePropertyName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // snake_case → separar por _
        name = name.Replace('_', ' ');

        // camelCase / PascalCase → insertar espacio antes de mayúsculas
        name = Regex.Replace(name, @"([a-z])([A-Z])", "$1 $2");
        name = Regex.Replace(name, @"([A-Z]+)([A-Z][a-z])", "$1 $2");

        // Title Case
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLower());
    }

    private static bool DetectPercentage(string name, double value)
    {
        var nameLower = name.ToLowerInvariant();

        // Nombre contiene palabras de porcentaje
        if (PercentageKeywords.Any(kw => nameLower.Contains(kw)))
            return true;

        // Valor entre 0 y 1, nombre no indica cantidad absoluta
        if (value is >= 0.0 and <= 1.0
            && !AbsoluteQuantityKeywords.Any(kw => nameLower.Contains(kw)))
            return true;

        return false;
    }

    private static string SuggestIcon(string name)
    {
        var lower = name.ToLowerInvariant();
        if (ContainsAny(lower, "ton", "weight", "peso", "kg"))  return "scale";
        if (ContainsAny(lower, "rate", "percent", "tasa"))       return "percent";
        if (ContainsAny(lower, "co2", "carbon", "emission"))     return "eco";
        if (ContainsAny(lower, "cost", "price", "amount", "importe")) return "euro";
        if (ContainsAny(lower, "count", "total", "number", "cantidad")) return "tag";
        if (ContainsAny(lower, "date", "time", "fecha"))         return "calendar_today";
        return "analytics";
    }

    private static bool ContainsAny(string source, params string[] keywords)
        => keywords.Any(kw => source.Contains(kw, StringComparison.OrdinalIgnoreCase));

    private static object? GetRawValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number   => element.TryGetDouble(out var d) ? d : (object?)null,
        JsonValueKind.String   => element.GetString(),
        JsonValueKind.True     => true,
        JsonValueKind.False    => false,
        JsonValueKind.Null     => null,
        _                      => element.ToString()
    };

    /// <inheritdoc />
    public string ComputeSchemaHash(JsonDataSchema schema)
    {
        // Serializar solo la estructura (nombres + tipos) para detectar cambios estructurales
        var sb = new System.Text.StringBuilder();

        foreach (var s in schema.RootScalars.OrderBy(p => p.Name))
            sb.Append($"{s.Name}:{s.PropertyType}|");

        foreach (var a in schema.Arrays.OrderBy(a => a.Name))
        {
            sb.Append($"[{a.Name}:");
            foreach (var p in a.ItemProperties.OrderBy(p => p.Name))
                sb.Append($"{p.Name}:{p.PropertyType},");
            sb.Append("]|");
        }

        foreach (var o in schema.NestedObjects.OrderBy(o => o.Name))
        {
            sb.Append($"{{{o.Name}:");
            foreach (var p in o.Properties.OrderBy(p => p.Name))
                sb.Append($"{p.Name}:{p.PropertyType},");
            sb.Append("}|");
        }

        using var md5 = System.Security.Cryptography.MD5.Create();
        var hashBytes = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
