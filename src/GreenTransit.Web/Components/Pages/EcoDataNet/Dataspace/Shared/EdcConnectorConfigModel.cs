using System.ComponentModel.DataAnnotations;

namespace GreenTransit.Web.Components.Pages.EcoDataNet.Dataspace.Shared;

public class EdcConnectorConfigModel
{
    public string? ConnectorName { get; set; }
    public string? ConnectorDns { get; set; }

    [Required(ErrorMessage = "La URL de Management es obligatoria")]
    [Url(ErrorMessage = "Formato de URL no válido")]
    public string ManagementUrl { get; set; } = string.Empty;

    public string? DefaultUrl { get; set; }

    [Required(ErrorMessage = "La URL de Protocol es obligatoria")]
    [Url(ErrorMessage = "Formato de URL no válido")]
    public string ProtocolUrl { get; set; } = string.Empty;

    public bool FederatedCatalogEnabled { get; set; }
    public string? FederatedCatalogUrl { get; set; }

    [Required(ErrorMessage = "El API Token es obligatorio")]
    public string ApiToken { get; set; } = string.Empty;

    // TODO: En la fase de integración real, este token se enviará como cabecera:
    // httpClient.DefaultRequestHeaders.Add("X-API-Key", model.ApiToken);

    public string? Did { get; set; }
    public string? KeycloakTokenUrl { get; set; }
    public string? KeycloakJwksUrl { get; set; }
    public string? KeycloakAudience { get; set; }
    public string? PublicApiBaseUrl { get; set; }
    public string? Notes { get; set; }
}
