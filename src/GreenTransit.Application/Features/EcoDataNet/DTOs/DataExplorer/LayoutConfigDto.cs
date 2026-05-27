namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// DTO completo de configuración de layout para transporte entre capas.
/// </summary>
public class LayoutConfigDto
{
    /// <summary>ID del registro en BD (0 si es nueva).</summary>
    public int Id { get; set; }

    /// <summary>AssetId del catálogo EDC.</summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>ID del participante proveedor.</summary>
    public string ProviderParticipantId { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo del dataset.</summary>
    public string? DatasetName { get; set; }

    /// <summary>Lista de overrides por widget.</summary>
    public List<WidgetLayoutOverride> Overrides { get; set; } = new();

    /// <summary>Hash del esquema cuando se guardó.</summary>
    public string? SchemaHash { get; set; }

    /// <summary>True si existe una configuración guardada para este asset.</summary>
    public bool HasSavedConfig { get; set; }

    /// <summary>Fecha de la última modificación.</summary>
    public DateTime? LastUpdated { get; set; }
}
