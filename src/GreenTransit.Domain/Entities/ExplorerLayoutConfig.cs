namespace GreenTransit.Domain.Entities;

/// <summary>
/// Configuración personalizada del layout del Data Explorer, vinculada a un asset EDC.
/// Cada combinación OwnerId + UserId + AssetId + ProviderParticipantId tiene como máximo una configuración.
/// </summary>
public class ExplorerLayoutConfig
{
    /// <summary>PK auto-incremental.</summary>
    public int Id { get; set; }

    /// <summary>Tenant al que pertenece la configuración.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>ID del usuario que creó/modificó la configuración.</summary>
    public int UserId { get; set; }

    /// <summary>
    /// Identificador del asset EDC en el catálogo DCAT del proveedor.
    /// Corresponde a EdcDatasetDto.DatasetId.
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Identificador del participante proveedor del asset (para distinguir assets
    /// con mismo ID de distintos proveedores).
    /// Corresponde a EdcNegotiationSelection.ProviderParticipantId.
    /// </summary>
    public string ProviderParticipantId { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo del dataset (para UI, no como clave).
    /// </summary>
    public string? DatasetName { get; set; }

    /// <summary>
    /// JSON serializado con la configuración de widgets personalizada.
    /// Contiene un array de WidgetLayoutOverride serializado.
    /// </summary>
    public string LayoutConfigJson { get; set; } = "[]";

    /// <summary>
    /// Hash MD5 del esquema JSON detectado la última vez que se guardó.
    /// Permite detectar si la estructura del asset ha cambiado.
    /// </summary>
    public string? SchemaHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
