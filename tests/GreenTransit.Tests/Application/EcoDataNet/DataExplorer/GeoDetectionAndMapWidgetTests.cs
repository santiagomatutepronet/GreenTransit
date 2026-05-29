using FluentAssertions;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Application.Features.EcoDataNet.Services;
using System.Globalization;

namespace GreenTransit.Tests.Application.EcoDataNet.DataExplorer;

/// <summary>
/// Tests de detección de coordenadas geográficas en JsonSchemaAnalyzer
/// y generación de widgets Map en DashboardLayoutBuilder.
/// </summary>
public sealed class GeoDetectionAndMapWidgetTests
{
    private readonly JsonSchemaAnalyzer _analyzer = new();
    private readonly DashboardLayoutBuilder _builder = new();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildJsonArray(IEnumerable<(double lat, double lon, string name)> items)
    {
        var rows = string.Join(",\n", items.Select(i =>
            string.Format(CultureInfo.InvariantCulture,
                "{{\"name\":\"{0}\",\"lat\":{1},\"lon\":{2}}}",
                i.name, i.lat, i.lon)));
        return $"[{rows}]";
    }

    // ── JsonSchemaAnalyzer: detección de coordenadas ──────────────────────

    [Fact]
    public void Analyze_ArrayWithLatLon_DetectsGeoCoordinates()
    {
        var json = BuildJsonArray([
            (40.4168, -3.7038, "Madrid"),
            (41.3851, 2.1734,  "Barcelona"),
            (37.3891, -5.9845, "Sevilla")
        ]);

        var schema = _analyzer.Analyze(json);

        schema.Should().NotBeNull();
        schema!.Arrays.Should().ContainSingle();
        var arr = schema.Arrays[0];
        arr.LatitudeProperty.Should().NotBeNullOrEmpty();
        arr.LongitudeProperty.Should().NotBeNullOrEmpty();
        arr.HasGeoCoordinates.Should().BeTrue();
    }

    [Fact]
    public void Analyze_ArrayWithoutGeoFields_NoGeoCoordinates()
    {
        var json = "[{\"vehicle\":\"Bus\",\"km\":150},{\"vehicle\":\"Tram\",\"km\":80}]";
        var schema = _analyzer.Analyze(json);

        schema.Should().NotBeNull();
        schema!.Arrays.Should().ContainSingle();
        schema.Arrays[0].HasGeoCoordinates.Should().BeFalse();
    }

    [Fact]
    public void Analyze_ArrayWithOutOfRangeValues_NoGeoCoordinates()
    {
        // Valores fuera de rango de lat/lon real → no debe detectarse como geo
        var json = "[{\"lat\":999,\"lon\":999},{\"lat\":888,\"lon\":888}]";
        var schema = _analyzer.Analyze(json);

        schema.Should().NotBeNull();
        var arr = schema!.Arrays[0];
        arr.HasGeoCoordinates.Should().BeFalse();
    }

    // ── DashboardLayoutBuilder: generación de widget Map ─────────────────

    [Fact]
    public void Build_ArrayWithGeoCoordinates_GeneratesMapWidget()
    {
        var json = BuildJsonArray([
            (40.4168, -3.7038, "Madrid"),
            (41.3851, 2.1734,  "Barcelona")
        ]);
        var schema = _analyzer.Analyze(json);
        schema.Should().NotBeNull();

        var widgets = _builder.Build(schema!);

        widgets.Should().Contain(w => w.Type == WidgetType.Map);
        var mapWidget = widgets.First(w => w.Type == WidgetType.Map);
        mapWidget.MapLatitudeField.Should().NotBeNullOrEmpty();
        mapWidget.MapLongitudeField.Should().NotBeNullOrEmpty();
        mapWidget.MapData.Should().NotBeEmpty();
        mapWidget.ColumnSpan.Should().Be(12);
    }

    [Fact]
    public void Build_ArrayWithGeoCoordinates_MapWidgetHasFieldLists()
    {
        var json = BuildJsonArray([
            (40.4168, -3.7038, "A"),
            (41.3851, 2.1734,  "B")
        ]);
        var schema = _analyzer.Analyze(json);
        schema.Should().NotBeNull();
        var widgets = _builder.Build(schema!);

        var mapWidget = widgets.First(w => w.Type == WidgetType.Map);
        mapWidget.MapAvailableAllFields.Should().NotBeNull().And.NotBeEmpty();
    }

    [Fact]
    public void Build_ArrayWithoutGeoCoordinates_NoMapWidget()
    {
        var json = "[{\"vehicle\":\"Bus\",\"km\":150},{\"vehicle\":\"Tram\",\"km\":80}]";
        var schema = _analyzer.Analyze(json);
        schema.Should().NotBeNull();

        var widgets = _builder.Build(schema!);

        widgets.Should().NotContain(w => w.Type == WidgetType.Map);
    }

    [Fact]
    public void Analyze_ArrayWithNullLatLonInFirstRows_StillDetectsGeoCoordinates()
    {
        // Primera fila con nulos en lat/lon; las siguientes tienen valores reales
        var json = """
            [
              {"LATITUDE": null, "LONGITUDE": null, "Name": "Unknown"},
              {"LATITUDE": null, "LONGITUDE": null, "Name": "Unknown2"},
              {"LATITUDE": 37.9922, "LONGITUDE": -1.1307, "Name": "Murcia"},
              {"LATITUDE": 43.2630, "LONGITUDE": -2.9350, "Name": "Bilbao"}
            ]
            """;

        var schema = _analyzer.Analyze(json);

        schema.Should().NotBeNull();
        var arr = schema!.Arrays[0];
        arr.NumericProperties.Should().Contain("LATITUDE");
        arr.NumericProperties.Should().Contain("LONGITUDE");
        arr.HasGeoCoordinates.Should().BeTrue();
    }

    }
