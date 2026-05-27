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
