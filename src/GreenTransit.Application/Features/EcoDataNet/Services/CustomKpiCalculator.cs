using System.Globalization;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <inheritdoc />
public class CustomKpiCalculator : ICustomKpiCalculator
{
    private static readonly CultureInfo SpanishCulture = new("es-ES");

    /// <inheritdoc />
    public (double? Value, string Display) Calculate(
        CustomKpiDefinition definition,
        IReadOnlyList<Dictionary<string, object?>> rawData)
    {
        if (rawData.Count == 0)
            return (null, "—");

        double? result = definition.Operation switch
        {
            KpiOperation.Count      => rawData.Count,
            KpiOperation.Sum        => SumField(rawData, definition.PrimaryField),
            KpiOperation.Average    => AverageField(rawData, definition.PrimaryField),
            KpiOperation.Min        => MinField(rawData, definition.PrimaryField),
            KpiOperation.Max        => MaxField(rawData, definition.PrimaryField),
            KpiOperation.Percentage => CalcPercentage(rawData, definition.PrimaryField, definition.SecondaryField),
            _                       => null
        };

        if (result is null)
            return (null, "—");

        return (result.Value, FormatValue(result.Value, definition));
    }

    // ── Operaciones ──────────────────────────────────────────────────────────

    private static double? SumField(IReadOnlyList<Dictionary<string, object?>> rows, string field)
    {
        double sum = 0;
        bool any = false;
        foreach (var row in rows)
        {
            if (TryGetDouble(row, field, out var v)) { sum += v; any = true; }
        }
        return any ? sum : null;
    }

    private static double? AverageField(IReadOnlyList<Dictionary<string, object?>> rows, string field)
    {
        double sum = 0;
        int count = 0;
        foreach (var row in rows)
        {
            if (TryGetDouble(row, field, out var v)) { sum += v; count++; }
        }
        return count > 0 ? sum / count : null;
    }

    private static double? MinField(IReadOnlyList<Dictionary<string, object?>> rows, string field)
    {
        double? min = null;
        foreach (var row in rows)
        {
            if (TryGetDouble(row, field, out var v) && (min is null || v < min))
                min = v;
        }
        return min;
    }

    private static double? MaxField(IReadOnlyList<Dictionary<string, object?>> rows, string field)
    {
        double? max = null;
        foreach (var row in rows)
        {
            if (TryGetDouble(row, field, out var v) && (max is null || v > max))
                max = v;
        }
        return max;
    }

    private static double? CalcPercentage(
        IReadOnlyList<Dictionary<string, object?>> rows,
        string primaryField,
        string? secondaryField)
    {
        if (string.IsNullOrEmpty(secondaryField))
            return null;

        double numerator = 0, denominator = 0;
        foreach (var row in rows)
        {
            if (TryGetDouble(row, primaryField, out var p)) numerator += p;
            if (TryGetDouble(row, secondaryField, out var s)) denominator += s;
        }

        return denominator == 0 ? null : numerator / denominator * 100;
    }

    // ── Formato ──────────────────────────────────────────────────────────────

    private static string FormatValue(double value, CustomKpiDefinition definition)
    {
        int decimals = Math.Clamp(definition.DecimalPlaces, 0, 4);
        string numberStr = value.ToString($"N{decimals}", SpanishCulture);

        // Sufijo personalizado tiene prioridad
        if (!string.IsNullOrWhiteSpace(definition.CustomSuffix))
            return $"{numberStr} {definition.CustomSuffix}";

        return definition.DisplayFormat switch
        {
            KpiDisplayFormat.Percent  => $"{numberStr} %",
            KpiDisplayFormat.Currency => $"{numberStr} €",
            _                         => numberStr
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryGetDouble(Dictionary<string, object?> row, string field, out double value)
    {
        value = 0;
        if (!row.TryGetValue(field, out var raw) || raw is null)
            return false;

        if (raw is double d)  { value = d; return true; }
        if (raw is float f)   { value = f; return true; }
        if (raw is int i)     { value = i; return true; }
        if (raw is long l)    { value = l; return true; }
        if (raw is decimal dc){ value = (double)dc; return true; }

        if (double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
