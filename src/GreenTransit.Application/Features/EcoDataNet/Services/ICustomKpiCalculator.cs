using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Calcula el valor de un KPI definido por el usuario a partir de los datos brutos del array.
/// </summary>
public interface ICustomKpiCalculator
{
    /// <summary>
    /// Calcula el resultado del KPI y devuelve el valor formateado.
    /// </summary>
    /// <param name="definition">Definición del KPI a calcular.</param>
    /// <param name="rawData">Filas del array fuente (clave = nombre de campo).</param>
    /// <returns>Tupla (valor numérico, valor formateado para mostrar).</returns>
    (double? Value, string Display) Calculate(
        CustomKpiDefinition definition,
        IReadOnlyList<Dictionary<string, object?>> rawData);
}
