namespace GreenTransit.Application.Common.Options;

/// <summary>
/// Opciones de configuración para la integración con la Management API de EDC.
/// Sección en appsettings: "EcoDataNet:Edc"
/// </summary>
public class EdcOptions
{
    public const string SectionName = "EcoDataNet:Edc";

    /// <summary>Máximo de solicitudes de catálogo simultáneas.</summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>Timeout por solicitud individual, en segundos.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>API Key para la Management API (header X-Api-Key). Vacío = no enviar header.</summary>
    public string ManagementApiKey { get; set; } = string.Empty;
}
