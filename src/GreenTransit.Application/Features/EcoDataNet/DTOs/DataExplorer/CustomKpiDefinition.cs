namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Define un KPI calculado por el usuario a partir de datos de un array del JSON.
/// Se almacena como parte de CustomWidgetDefinition.KpiDefinition.
/// </summary>
public class CustomKpiDefinition
{
    /// <summary>
    /// Nombre del array fuente del JSON del que se extraen los datos para el cálculo.
    /// Corresponde a JsonArrayDescriptor.Name.
    /// </summary>
    public string SourceArrayName { get; set; } = string.Empty;

    /// <summary>Operación a aplicar sobre el campo primario.</summary>
    public KpiOperation Operation { get; set; }

    /// <summary>
    /// Campo numérico principal sobre el que se aplica la operación.
    /// Debe ser un nombre de propiedad numérica existente en el array fuente.
    /// </summary>
    public string PrimaryField { get; set; } = string.Empty;

    /// <summary>
    /// Campo numérico secundario, solo para operaciones de porcentaje.
    /// En operación Percentage: resultado = SUM(PrimaryField) / SUM(SecondaryField) * 100.
    /// Null si la operación no es Percentage.
    /// </summary>
    public string? SecondaryField { get; set; }

    /// <summary>Formato de presentación del resultado.</summary>
    public KpiDisplayFormat DisplayFormat { get; set; } = KpiDisplayFormat.Number;

    /// <summary>Número de decimales a mostrar (0-4). Default: 2.</summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Sufijo personalizado para el valor (ej: "t", "€", "kg", "%").
    /// Para porcentaje: si es null, se añade "%" automáticamente.
    /// </summary>
    public string? CustomSuffix { get; set; }

    /// <summary>
    /// Icono Material Design personalizado (ej: "calculate", "percent", "euro").
    /// Si es null, se asigna uno por defecto según la operación.
    /// </summary>
    public string? CustomIcon { get; set; }
}

/// <summary>Operaciones disponibles para KPIs calculados.</summary>
public enum KpiOperation
{
    /// <summary>Suma de todos los valores del campo en el array.</summary>
    Sum,
    /// <summary>Número de elementos en el array.</summary>
    Count,
    /// <summary>Media aritmética del campo en el array.</summary>
    Average,
    /// <summary>Valor mínimo del campo en el array.</summary>
    Min,
    /// <summary>Valor máximo del campo en el array.</summary>
    Max,
    /// <summary>Porcentaje: SUM(PrimaryField) / SUM(SecondaryField) * 100.</summary>
    Percentage
}

/// <summary>Formato de presentación del KPI calculado.</summary>
public enum KpiDisplayFormat
{
    /// <summary>Número con separador de miles y decimales (es-ES).</summary>
    Number,
    /// <summary>Porcentaje (añade sufijo "%").</summary>
    Percent,
    /// <summary>Moneda (añade sufijo "€").</summary>
    Currency
}
