namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet;

/// <summary>
/// Opciones de configuración para la integración con la API EcoDataNet Waste.
/// Sección en appsettings: "EcoDataNetWaste"
/// </summary>
public class EcoDataNetOptions
{
    public const string SectionName = "EcoDataNetWaste";

    public string BaseUrl        { get; set; } = string.Empty;
    public string Username       { get; set; } = string.Empty;
    public string Password       { get; set; } = string.Empty;
    public int    BatchSize      { get; set; } = 100;
    public int    TimeoutSeconds { get; set; } = 120;
    public int    MaxRetries     { get; set; } = 3;
}
