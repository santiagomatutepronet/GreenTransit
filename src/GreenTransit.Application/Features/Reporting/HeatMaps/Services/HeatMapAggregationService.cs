using GreenTransit.Application.Features.Reporting.HeatMaps.DTOs;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Reporting.HeatMaps.Services;

/// <summary>
/// Servicio de agregaciÃ³n y generaciÃ³n de alertas para los dashboards de Mapas de Calor.
/// La lÃ³gica de negocio se calcula en el backend, nunca en el cliente.
/// </summary>
public sealed class HeatMapAggregationService
{
    // Umbrales cacheados en el constructor para evitar parsear IConfiguration en cada llamada
    private readonly int     _maxDaysWithoutPickup;
    private readonly decimal _overloadThresholdKg;
    private readonly double  _frequencyDropPct;

    public HeatMapAggregationService(IConfiguration config)
    {
        _maxDaysWithoutPickup = int.TryParse(config["HeatMaps:Alerts:MaxDaysWithoutPickup"], out var d) ? d : 30;
        _overloadThresholdKg  = decimal.TryParse(config["HeatMaps:Alerts:OverloadThresholdKg"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 5000m;
        _frequencyDropPct     = double.TryParse(config["HeatMaps:Alerts:FrequencyDropPercent"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fpVal) ? fpVal : 30.0;
    }

    // â”€â”€ Alertas de acumulaciÃ³n â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Genera alertas de acumulaciÃ³n a partir de la lista de puntos con sus Ãºltimas recogidas.
    /// Los umbrales se leen de appsettings.json (HeatMaps:Alerts:*).
    /// </summary>
    public IReadOnlyList<AccumulationAlertDto> GenerateAccumulationAlerts(
        IEnumerable<PointAlertInput>  points,
        IEnumerable<ZoneAlertInput>   zones,
        IEnumerable<FreqAlertInput>   frequencyData)
    {
        var maxDaysWithoutPickup = _maxDaysWithoutPickup;
        var overloadThresholdKg  = _overloadThresholdKg;
        var frequencyDropPct     = _frequencyDropPct;

        var alerts = new List<AccumulationAlertDto>();
        var now    = DateTime.UtcNow;

        foreach (var p in points)
        {
            if (p.LastPickup.HasValue
                && (now - p.LastPickup.Value).TotalDays > maxDaysWithoutPickup
                && p.AccumulatedKg > overloadThresholdKg)
            {
                alerts.Add(new AccumulationAlertDto(
                    AlertType        : "OverloadPoint",
                    Severity         : p.AccumulatedKg > overloadThresholdKg * 2 ? "High" : "Medium",
                    EntityOrZoneName : p.EntityName,
                    Message          : $"Punto {p.EntityName} en {p.Municipality} acumula {p.AccumulatedKg:N0} kg sin recogida desde {p.LastPickup:dd/MM/yyyy}.",
                    GeneratedAt      : now));
            }
        }

        foreach (var z in zones)
        {
            if (z.IsAbovePercentile95)
            {
                alerts.Add(new AccumulationAlertDto(
                    AlertType        : "HighDensityMunicipality",
                    Severity         : "High",
                    EntityOrZoneName : z.ZoneName,
                    Message          : $"Municipio {z.ZoneName} presenta concentraciÃ³n anormalmente alta: {z.TotalKg:N0} kg.",
                    GeneratedAt      : now));
            }
        }

        foreach (var f in frequencyData)
        {
            if (f.CurrentFreq > 0 && f.PreviousFreq > 0)
            {
                var drop = (f.PreviousFreq - f.CurrentFreq) / f.PreviousFreq * 100.0;
                if (drop >= frequencyDropPct)
                {
                    alerts.Add(new AccumulationAlertDto(
                        AlertType        : "ReducedFrequency",
                        Severity         : "Low",
                        EntityOrZoneName : f.ZoneName,
                        Message          : $"Frecuencia de recogida reducida en {f.ZoneName}: {drop:N0}% menos que el periodo anterior.",
                        GeneratedAt      : now));
                }
            }
        }

        return alerts.OrderByDescending(a => a.Severity).ToList();
    }

    // â”€â”€ Ãndice de concentraciÃ³n (coeficiente de Gini simplificado) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Calcula un Ã­ndice de concentraciÃ³n Gini sobre la distribuciÃ³n de kg por punto.</summary>
    public static double CalculateConcentrationIndex(IEnumerable<decimal> kgPerPoint)
    {
        var sorted = kgPerPoint.OrderBy(x => x).ToArray();
        if (sorted.Length == 0) return 0;

        var n     = sorted.Length;
        var total = (double)sorted.Sum();
        if (total == 0) return 0;

        double sumNumerator = 0;
        for (int i = 0; i < n; i++)
            sumNumerator += (2 * (i + 1) - n - 1) * (double)sorted[i];

        return Math.Round(sumNumerator / (n * total), 4);
    }

    // â”€â”€ Media mÃ³vil de 3 meses â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public static IReadOnlyList<decimal> ComputeMovingAverage3M(IReadOnlyList<decimal> values)
    {
        var result = new decimal[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            int   count = 0;
            decimal sum = 0;
            for (int j = Math.Max(0, i - 2); j <= i; j++) { sum += values[j]; count++; }
            result[i] = count > 0 ? Math.Round(sum / count, 2) : 0;
        }
        return result;
    }

    // â”€â”€ SemÃ¡foro por percentil â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public static string ComputeTrafficLight(decimal value, IReadOnlyList<decimal> allValues)
    {
        if (allValues.Count == 0) return "Green";
        var sorted = allValues.OrderBy(x => x).ToArray();
        var p75    = Percentile(sorted, 75);
        var p90    = Percentile(sorted, 90);
        if (value >= p90) return "Red";
        if (value >= p75) return "Orange";
        return "Green";
    }

    private static decimal Percentile(decimal[] sorted, int pct)
    {
        var index = (pct / 100.0) * (sorted.Length - 1);
        var lower = (int)index;
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + (decimal)(index - lower) * (sorted[upper] - sorted[lower]);
    }
}

// â”€â”€ Inputs de alerta (tipos internos) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public sealed record PointAlertInput(
    string    EntityName,
    string?   Municipality,
    decimal   AccumulatedKg,
    DateTime? LastPickup
);

public sealed record ZoneAlertInput(
    string  ZoneName,
    decimal TotalKg,
    bool    IsAbovePercentile95
);

public sealed record FreqAlertInput(
    string ZoneName,
    double CurrentFreq,
    double PreviousFreq
);
