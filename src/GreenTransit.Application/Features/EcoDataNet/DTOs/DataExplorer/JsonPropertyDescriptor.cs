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

    /// <summary>Icono Material Design sugerido para KPI cards.</summary>
    public string? SuggestedIcon { get; set; }
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
