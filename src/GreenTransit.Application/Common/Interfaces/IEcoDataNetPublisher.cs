using GreenTransit.Application.Common.Models;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Publica todos los datos operativos de GreenTransit a la API EcoDataNet Waste.
/// </summary>
public interface IEcoDataNetPublisher
{
    /// <summary>
    /// Publica los 16 endpoints en orden. El callback opcional reporta progreso.
    /// </summary>
    Task<PublishSummary> PublishAllAsync(
        Action<string, int, int>? onProgress = null,
        CancellationToken ct = default);
}

/// <summary>Resumen agregado de la publicación completa.</summary>
public class PublishSummary
{
    public List<EndpointResult> Results        { get; set; } = [];
    public int      TotalEndpoints             => Results.Count;
    public int      TotalItemsSent             => Results.Sum(r => r.TotalSent);
    public int      TotalSuccess               => Results.Sum(r => r.SuccessCount);
    public int      TotalErrors                => Results.Sum(r => r.ErrorCount);
    public TimeSpan Duration                   { get; set; }
}
