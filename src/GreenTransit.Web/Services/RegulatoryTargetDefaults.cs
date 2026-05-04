using GreenTransit.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Web.Services;

/// <summary>
/// Lee los valores por defecto de objetivos regulatorios desde appsettings.json.
/// Fallback: 55 % reciclaje, 5 % reutilización (mínimos Ley 7/2022 para RAEE/envases).
/// </summary>
public sealed class RegulatoryTargetDefaults : IRegulatoryTargetDefaults
{
    public double DefaultMinRecyclingPercent { get; }
    public double DefaultMinReusePercent     { get; }

    public RegulatoryTargetDefaults(IConfiguration config)
    {
        DefaultMinRecyclingPercent = config.GetValue<double>(
            "RegulatoryTargets:DefaultMinRecyclingPercent", 55.0);
        DefaultMinReusePercent = config.GetValue<double>(
            "RegulatoryTargets:DefaultMinReusePercent", 5.0);
    }
}
