using GreenTransit.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Ecomodulation.Services;

/// <summary>
/// Motor de recomendaciones de ecodiseño basado en las fichas técnicas de los productos.
/// Las recomendaciones se generan en el backend y se entregan como lista de strings.
/// </summary>
public sealed class EcomodulationRecommendationEngine
{
    private readonly IConfiguration _config;

    public EcomodulationRecommendationEngine(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Genera recomendaciones a partir de los residuos de tipo ProductSpec.
    /// </summary>
    public IReadOnlyList<string> GenerateFromResidues(IEnumerable<Residue> residues)
    {
        var list = residues.ToList();
        if (list.Count == 0) return [];

        var recommendations = new List<string>();

        // Cobertura de índice de reparabilidad
        var pctNoRepair = list.Count(r => !r.ReparabilityIndex.HasValue);
        if (pctNoRepair > 0)
            recommendations.Add($"{pctNoRepair} producto(s) sin índice de reparabilidad informado. Completar este campo mejora el score DPP y el índice de circularidad.");

        // Cobertura de contenido reciclado
        var pctNoRecycled = list.Count(r => !r.RecycledContentPercent.HasValue);
        if (pctNoRecycled > 0)
            recommendations.Add($"{pctNoRecycled} producto(s) sin porcentaje de contenido reciclado. Este dato es clave para las reglas de eco-modulación.");

        // Productos con bajo contenido reciclado
        var minRecycled = double.TryParse(_config["Ecomodulation:Thresholds:MinRecycledContentPct"],
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var _mr) ? _mr : 30.0;
        var lowRecycled = list.Count(r => r.RecycledContentPercent.HasValue && (double)r.RecycledContentPercent < minRecycled);
        if (lowRecycled > 0)
            recommendations.Add($"{lowRecycled} producto(s) con contenido reciclado inferior al {minRecycled}%. Se recomienda revisar el proceso de fabricación para incrementar el uso de materiales reciclados.");

        // Productos sin composición JSON
        var noComposition = list.Count(r => string.IsNullOrEmpty(r.CompositionJson));
        if (noComposition > 0)
            recommendations.Add($"{noComposition} producto(s) sin composición de materiales declarada. Este dato es imprescindible para el Pasaporte Digital de Producto (DPP).");

        // Productos peligrosos sin código de peligrosidad
        var hazWithoutCode = list.Count(r => r.ContainsHazardous == true && string.IsNullOrEmpty(r.DangerousCode));
        if (hazWithoutCode > 0)
            recommendations.Add($"{hazWithoutCode} producto(s) marcado(s) como peligrosos sin código de peligrosidad (DangerousCode). Completar antes de publicar en el Data Space.");

        // Productos sin facilidad de desmontaje
        var noDisassembly = list.Count(r => string.IsNullOrEmpty(r.DisassemblyEase));
        if (noDisassembly > 0)
            recommendations.Add($"{noDisassembly} producto(s) sin facilidad de desmontaje declarada. Este campo impacta directamente en el índice de circularidad.");

        // Productos sin código LER potencial
        var noLerCodes = list.Count(r => string.IsNullOrEmpty(r.PotentialLERCodesJson));
        if (noLerCodes > 0)
            recommendations.Add($"{noLerCodes} producto(s) sin códigos LER potenciales al fin de vida. Declararlos facilita la trazabilidad de residuos y el cumplimiento normativo.");

        if (recommendations.Count == 0)
            recommendations.Add("¡Excelente! Todos los productos tienen las fichas de ecodiseño completas y cumplen los umbrales mínimos recomendados.");

        return recommendations;
    }
}
