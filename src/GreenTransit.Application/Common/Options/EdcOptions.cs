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

    /// <summary>Intervalo de polling para estado de negociación, en segundos.</summary>
    public int NegotiationPollingIntervalSeconds { get; set; } = 3;

    /// <summary>Intervalo de polling para estado de transferencia, en segundos.</summary>
    public int TransferPollingIntervalSeconds { get; set; } = 3;

    /// <summary>Máximo de intentos de polling para negociación (3s × 120 = 6 min).</summary>
    public int NegotiationPollingMaxAttempts { get; set; } = 120;

    /// <summary>Máximo de intentos de polling para transferencia (3s × 60 = 3 min).</summary>
    public int TransferPollingMaxAttempts { get; set; } = 60;
}
