namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Servicio para reconstruir los datos de un gráfico cuando el usuario cambia los campos de categoría o valores.
/// </summary>
public interface IChartDataRebuilder
{
    /// <summary>
    /// Reconstruye los datos de un gráfico a partir de los datos crudos, aplicando agregación por categoría.
    /// </summary>
    /// <param name="rawData">Datos crudos del array fuente.</param>
    /// <param name="categoryField">Campo a usar como eje de categorías (eje X o segmentos).</param>
    /// <param name="valueFields">Campos numéricos a sumar (eje Y o series).</param>
    /// <param name="truncateToMonth">Si true, agrupa fechas por mes (yyyy-MM).</param>
    /// <returns>Lista de diccionarios con los datos agregados para el gráfico.</returns>
    List<Dictionary<string, object?>> RebuildChartData(
        List<Dictionary<string, object?>> rawData,
        string categoryField,
        List<string> valueFields,
        bool truncateToMonth = false);
}
