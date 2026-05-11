using GreenTransit.Application.Features.Mobility.DTOs;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Mobility.Services;

/// <summary>
/// Motor de reglas que genera recomendaciones automáticas de movilidad basadas en umbrales
/// configurados en appsettings.json (sección MobilitySettings).
/// </summary>
public interface IMobilityRecommendationEngine
{
    IReadOnlyList<MobilityRecommendationDto> Generate(
        IEnumerable<MunicipalConflictIndexDto> conflictData);
}

public sealed class MobilityRecommendationEngine : IMobilityRecommendationEngine
{
    private readonly double _peakHourThreshold;
    private readonly double _dumOutsideThreshold;
    private readonly double _conflictIndexCritical;

    public MobilityRecommendationEngine(IConfiguration configuration)
    {
        var s = configuration.GetSection("MobilitySettings");
        _peakHourThreshold     = ParseDouble(s["PeakHourAlertThresholdPercent"],    30.0);
        _dumOutsideThreshold   = ParseDouble(s["DumOutsideAlertThresholdPercent"],  20.0);
        _conflictIndexCritical = ParseDouble(s["ConflictIndexCriticalThreshold"],   70.0);
    }

    public IReadOnlyList<MobilityRecommendationDto> Generate(
        IEnumerable<MunicipalConflictIndexDto> conflictData)
    {
        var results = new List<MobilityRecommendationDto>();

        foreach (var m in conflictData)
        {
            if (m.PeakHourPercent > _peakHourThreshold)
            {
                results.Add(new MobilityRecommendationDto(
                    Severity:         "Warning",
                    MunicipalityCode: m.MunicipalityCode,
                    MunicipalityName: m.MunicipalityName,
                    Message: $"Considerar redistribuir las recogidas del municipio {m.MunicipalityName ?? m.MunicipalityCode} " +
                             $"fuera de la franja horaria de mayor tráfico " +
                             $"({m.PeakHourPercent:F1}% de recogidas en hora pico)."));
            }

            if (m.OutsideDumWindowPercent > _dumOutsideThreshold)
            {
                results.Add(new MobilityRecommendationDto(
                    Severity:         "Warning",
                    MunicipalityCode: m.MunicipalityCode,
                    MunicipalityName: m.MunicipalityName,
                    Message: $"Revisar la planificación de rutas en {m.MunicipalityName ?? m.MunicipalityCode}: " +
                             $"{m.OutsideDumWindowPercent:F1}% de recogidas fuera de ventana DUM."));
            }

            if (m.ConflictIndex > _conflictIndexCritical)
            {
                results.Add(new MobilityRecommendationDto(
                    Severity:         "Critical",
                    MunicipalityCode: m.MunicipalityCode,
                    MunicipalityName: m.MunicipalityName,
                    Message: $"Municipio {m.MunicipalityName ?? m.MunicipalityCode} requiere intervención " +
                             $"prioritaria de coordinación (índice de conflicto: {m.ConflictIndex:F0}/100)."));
            }
        }

        return results;
    }

    private static double ParseDouble(string? value, double defaultValue)
        => double.TryParse(value,
               System.Globalization.NumberStyles.Any,
               System.Globalization.CultureInfo.InvariantCulture,
               out var v) ? v : defaultValue;
}
