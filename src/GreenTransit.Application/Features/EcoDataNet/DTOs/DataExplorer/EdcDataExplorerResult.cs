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
