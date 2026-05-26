namespace GreenTransit.Application.Features.EcoDataNet.DTOs;

/// <summary>Datos del conector EDC de un usuario.</summary>
public class UserEDCConnectorDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string EDCServerName { get; set; } = string.Empty;
    public string EDCConnectorId { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
}

/// <summary>Usuario listado para la vista ADMIN de configuración de conector EDC.</summary>
public class UserForEDCListDto
{
    public int Id { get; set; }
    public string CompleteName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string ProfileReference { get; set; } = string.Empty;
    public bool HasEDCConnector { get; set; }
}

/// <summary>Perfil consumible en el dataspace EDC.</summary>
public class ProfileEDCConsumerDto
{
    public int ProfileId { get; set; }
    public string ProfileReference { get; set; } = string.Empty;
    public string ProfileDescription { get; set; } = string.Empty;
}

// ── DTOs para el descubrimiento de catálogo EDC ───────────────────────────────

/// <summary>Resultado de una solicitud de catálogo contra un conector EDC proveedor.</summary>
public class EdcCatalogResult
{
    public bool Success { get; set; }
    public string? RawJson { get; set; }
    public int DatasetCount { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
}

/// <summary>Estado de la consulta de catálogo para un proveedor concreto.</summary>
public enum EdcProviderStatus
{
    Ok,
    Error,
    NoConnector,
    Timeout
}

/// <summary>Resultado por usuario proveedor en el descubrimiento de catálogo EDC.</summary>
public class EdcProviderCatalogResult
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string? EDCServerName { get; set; }
    public string? EDCConnectorId { get; set; }
    public EdcProviderStatus Status { get; set; }
    public EdcCatalogResult? CatalogResult { get; set; }
}

/// <summary>Respuesta agregada del command RequestEdcCatalogCommand.</summary>
public class RequestEdcCatalogResponse
{
    public string ConsumerServerName { get; set; } = string.Empty;
    public int TotalProviders { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int NoConnectorCount { get; set; }
    public List<EdcProviderCatalogResult> Results { get; set; } = new();
}

// ── DTOs del catálogo DCAT/ODRL parseado ─────────────────────────────────────

/// <summary>Catálogo DCAT parseado de un proveedor EDC.</summary>
public class EdcCatalogDto
{
    public string CatalogId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public List<EdcDatasetDto> Datasets { get; set; } = new();
}

/// <summary>Dataset individual del catálogo DCAT.</summary>
public class EdcDatasetDto
{
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? ContentType { get; set; }
    public string? Description { get; set; }
    public EdcOfferDto? Offer { get; set; }
    public List<EdcDistributionDto> Distributions { get; set; } = new();
}

/// <summary>
/// Oferta ODRL asociada a un dataset.
/// Contiene el OfferId que se usa para la negociación (odrl:hasPolicy.@id).
/// </summary>
public class EdcOfferDto
{
    public string OfferId { get; set; } = string.Empty;
    public List<EdcPermissionDto> Permissions { get; set; } = new();
    public List<EdcProhibitionDto> Prohibitions { get; set; } = new();
    public List<EdcObligationDto> Obligations { get; set; } = new();
}

/// <summary>Permiso ODRL con su acción y restricciones.</summary>
public class EdcPermissionDto
{
    public string Action { get; set; } = string.Empty;
    public List<EdcConstraintDto> Constraints { get; set; } = new();
}

/// <summary>Restricción ODRL (leftOperand operator rightOperand).</summary>
public class EdcConstraintDto
{
    public string LeftOperand { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string RightOperand { get; set; } = string.Empty;
}

/// <summary>Prohibición ODRL.</summary>
public class EdcProhibitionDto
{
    public string Action { get; set; } = string.Empty;
    public List<EdcConstraintDto> Constraints { get; set; } = new();
}

/// <summary>Obligación ODRL.</summary>
public class EdcObligationDto
{
    public string Action { get; set; } = string.Empty;
    public List<EdcConstraintDto> Constraints { get; set; } = new();
}

/// <summary>Distribución DCAT del dataset (informativa).</summary>
public class EdcDistributionDto
{
    public string Format { get; set; } = string.Empty;
    public string? AccessServiceId { get; set; }
    public string? EndpointUrl { get; set; }
}

/// <summary>Catálogo parseado de un proveedor específico, con datos del usuario proveedor.</summary>
public class EdcProviderParsedCatalogDto
{
    public int ProviderUserId { get; set; }
    public string ProviderUserName { get; set; } = string.Empty;
    public string ProviderUserLogin { get; set; } = string.Empty;
    public string? ProviderServerName { get; set; }
    public string? ProviderConnectorId { get; set; }
    public string ProviderParticipantId { get; set; } = string.Empty;
    public string ProviderProtocolEndpoint { get; set; } = string.Empty;
    public EdcCatalogDto? Catalog { get; set; }
    public bool ParseSuccess { get; set; }
    public string? ParseError { get; set; }
}

/// <summary>
/// Estado de selección de oferta listo para iniciar negociación.
/// Se guarda en el componente Blazor y se usará en el futuro paso de negociación.
/// </summary>
public class EdcNegotiationSelection
{
    public string SelectedDatasetId { get; set; } = string.Empty;
    public string SelectedOfferId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
    public string ProviderProtocolEndpoint { get; set; } = string.Empty;
    public string DatasetName { get; set; } = string.Empty;
    public string ProviderUserName { get; set; } = string.Empty;
}
