namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Provee los valores por defecto de los objetivos regulatorios cuando no hay
/// registro en <c>RegulatoryTargets</c> para el OwnerId/año/categoría solicitados.
/// Implementado en Infrastructure/Web con IConfiguration.
/// </summary>
public interface IRegulatoryTargetDefaults
{
    double DefaultMinRecyclingPercent { get; }
    double DefaultMinReusePercent     { get; }
}
