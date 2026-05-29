namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Implementación de IChartDataRebuilder.
/// Reutiliza la lógica de agregación del DashboardLayoutBuilder para reconstruir datos de gráficos.
/// </summary>
public class ChartDataRebuilder : IChartDataRebuilder
{
    private const int MaxChartPoints = 40;

    /// <inheritdoc />
    public List<Dictionary<string, object?>> RebuildChartData(
        List<Dictionary<string, object?>> rawData,
        string categoryField,
        List<string> valueFields,
        bool truncateToMonth = false)
    {
        if (rawData.Count == 0 || valueFields.Count == 0)
            return rawData;

        // Función de clave: truncar a mes si procede
        string KeyOf(Dictionary<string, object?> row)
        {
            var raw = row.TryGetValue(categoryField, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
            if (!truncateToMonth) return raw;

            // Intentar parsear como fecha ISO y devolver "yyyy-MM"
            if (raw.Length >= 7 && raw[4] == '-')
                return raw[..7];   // "2024-05-12" → "2024-05"
            if (DateTime.TryParse(raw, out var dt))
                return dt.ToString("yyyy-MM");
            return raw;
        }

        // Agrupar y sumar
        var groups = rawData
            .GroupBy(KeyOf)
            .Select(g =>
            {
                var row = new Dictionary<string, object?> { [categoryField] = g.Key };
                foreach (var field in valueFields)
                {
                    var sum = g.Sum(r =>
                    {
                        if (!r.TryGetValue(field, out var fv)) return 0.0;
                        return fv is double d ? d : double.TryParse(fv?.ToString(), out var p) ? p : 0.0;
                    });
                    row[field] = sum;
                }
                return row;
            })
            .OrderBy(r => r[categoryField]?.ToString())
            .Take(MaxChartPoints)
            .ToList();

        return groups;
    }
}
