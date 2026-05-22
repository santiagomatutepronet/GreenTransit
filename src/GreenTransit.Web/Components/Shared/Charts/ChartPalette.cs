namespace GreenTransit.Web.Components.Shared.Charts;

public static class ChartPalette
{
    public static readonly string[] CorporateColors =
    [
        "#0A404B", "#8ACCC3", "#D8B00E", "#D36F15",
        "#C13E43", "#6E4583", "#535497", "#B4B736"
    ];

    public static string GetColor(int seriesIndex)
        => CorporateColors[seriesIndex % CorporateColors.Length];
}
