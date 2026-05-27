namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Servicio Scoped que almacena temporalmente el JSON descargado y sus metadatos
/// para la navegación interna desde ConsumeData hacia el Data Explorer.
/// Los datos se pierden al cerrar la página (sin persistencia en BD).
/// </summary>
public class EdcDataExplorerStateService
{
    /// <summary>Contenido JSON descargado de la transferencia EDC.</summary>
    public string? JsonContent { get; set; }

    /// <summary>Nombre del proveedor del asset.</summary>
    public string? ProviderName { get; set; }

    /// <summary>Nombre del dataset/asset.</summary>
    public string? DatasetName { get; set; }

    /// <summary>Identificador del asset EDC en el catálogo DCAT del proveedor.</summary>
    public string? AssetId { get; set; }

    /// <summary>Identificador del participante proveedor del asset.</summary>
    public string? ProviderParticipantId { get; set; }

    /// <summary>True si hay datos disponibles para explorar.</summary>
    public bool HasData => !string.IsNullOrEmpty(JsonContent);

    /// <summary>Limpia el estado almacenado.</summary>
    public void Clear()
    {
        JsonContent          = null;
        ProviderName         = null;
        DatasetName          = null;
        AssetId              = null;
        ProviderParticipantId = null;
    }
}
