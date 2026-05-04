using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>
/// Objetivos normativos de reciclaje/reutilización configurables por tenant, categoría y año.
/// Si no existe registro para un OwnerId concreto el sistema usa los valores por defecto de appsettings.
/// Tabla: RegulatoryTargets
/// </summary>
public class RegulatoryTarget : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }

    /// <summary>Categoría de residuo (null = aplica a todas las categorías del tenant).</summary>
    public string? Category { get; set; }

    public int Year { get; set; }

    /// <summary>Porcentaje mínimo de reciclaje exigido (ej. 55 → 55 %).</summary>
    public double MinRecyclingPercent { get; set; }

    /// <summary>Porcentaje mínimo de preparación para reutilización (ej. 5 → 5 %).</summary>
    public double MinReusePercent { get; set; }
}
