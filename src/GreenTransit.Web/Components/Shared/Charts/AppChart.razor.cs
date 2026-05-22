using Microsoft.AspNetCore.Components;
using Radzen.Blazor;

namespace GreenTransit.Web.Components.Shared.Charts;

public partial class AppChart
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    [Parameter] public string? Title { get; set; }

    [Parameter] public int Height { get; set; } = 280;

    [Parameter] public bool ShowLegend { get; set; } = true;

    [Parameter] public LegendPosition LegendPosition { get; set; } = LegendPosition.Bottom;

    [Parameter] public string? CategoryAxisTitle { get; set; }

    [Parameter] public string? ValueAxisTitle { get; set; }

    /// <summary>
    /// Formato de los valores del eje de categorías (ej. "dd/MM/yyyy").
    /// </summary>
    [Parameter] public string? CategoryFormatString { get; set; }

    /// <summary>
    /// Formato de los valores del eje de valor (ej. "{0:N2}").
    /// </summary>
    [Parameter] public string? ValueFormatString { get; set; }
}
