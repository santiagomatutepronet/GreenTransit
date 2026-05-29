using FluentAssertions;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Application.Features.EcoDataNet.Services;

namespace GreenTransit.Tests.Application.EcoDataNet.DataExplorer;

/// <summary>
/// Tests del servicio DashboardLayoutBuilder: generación de widgets a partir de JsonDataSchema.
/// </summary>
public sealed class DashboardLayoutBuilderTests
{
    private readonly DashboardLayoutBuilder _sut = new();

    // ── Helper: schema con escalares numéricos ────────────────────────────

    private static JsonDataSchema SchemaWithNumericScalars(params (string name, double value)[] scalars)
    {
        var schema = new JsonDataSchema();
        foreach (var (name, value) in scalars)
        {
            schema.RootScalars.Add(new JsonPropertyDescriptor
            {
                Name         = name,
                DisplayName  = name,
                PropertyType = JsonPropertyType.Number,
                SampleValues = [value.ToString()]
            });
        }
        return schema;
    }

    // ── Schema vacío ─────────────────────────────────────────────────────

    [Fact]
    public void Build_EmptySchema_ReturnsEmptyList()
    {
        var result = _sut.Build(new JsonDataSchema());
        result.Should().NotBeNull();
    }

    // ── KPI cards ────────────────────────────────────────────────────────

    [Fact]
    public void Build_SingleNumericScalar_ProducesOneKpiCard()
    {
        var schema = SchemaWithNumericScalars(("total", 42));

        var widgets = _sut.Build(schema);

        widgets.Should().Contain(w => w.Type == WidgetType.KpiCard);
        widgets.Count(w => w.Type == WidgetType.KpiCard).Should().Be(1);
    }

    [Fact]
    public void Build_MultipleNumericScalars_ProducesMultipleKpiCards()
    {
        var schema = SchemaWithNumericScalars(("a", 1), ("b", 2), ("c", 3));

        var widgets = _sut.Build(schema);

        widgets.Count(w => w.Type == WidgetType.KpiCard).Should().Be(3);
    }

    [Fact]
    public void Build_KpiCard_HasNonEmptyTitleAndColor()
    {
        var schema = SchemaWithNumericScalars(("revenue", 10000));
        var kpi    = _sut.Build(schema).First(w => w.Type == WidgetType.KpiCard);

        kpi.Title.Should().NotBeNullOrWhiteSpace();
        kpi.KpiColor.Should().NotBeNullOrWhiteSpace();
        kpi.KpiColor.Should().StartWith("#");
    }

    // ── Section header ───────────────────────────────────────────────────

    [Fact]
    public void Build_StringScalars_ProducesSectionHeader()
    {
        var schema = new JsonDataSchema();
        schema.RootScalars.Add(new JsonPropertyDescriptor
        {
            Name         = "title",
            DisplayName  = "Title",
            PropertyType = JsonPropertyType.String,
            SampleValues = ["My Dataset"]
        });

        var widgets = _sut.Build(schema);

        widgets.Should().Contain(w => w.Type == WidgetType.SectionHeader);
    }

    // ── Tabla a partir de array ───────────────────────────────────────────

    [Fact]
    public void Build_HomogeneousArray_ProducesDataTable()
    {
        var schema = new JsonDataSchema();
        var array  = new JsonArrayDescriptor
        {
            Name          = "items",
            IsHomogeneous = true,
            RawData       = [
                new Dictionary<string, object?> { ["id"] = (object)1, ["name"] = "Alice" },
                new Dictionary<string, object?> { ["id"] = (object)2, ["name"] = "Bob" }
            ]
        };
        array.ItemProperties.Add(new JsonPropertyDescriptor
        {
            Name         = "id",
            DisplayName  = "Id",
            PropertyType = JsonPropertyType.Number
        });
        array.ItemProperties.Add(new JsonPropertyDescriptor
        {
            Name         = "name",
            DisplayName  = "Name",
            PropertyType = JsonPropertyType.String
        });
        schema.Arrays.Add(array);

        var widgets = _sut.Build(schema);

        widgets.Should().Contain(w => w.Type == WidgetType.DataTable);
    }

    // ── Gráfico temporal ─────────────────────────────────────────────────

    [Fact]
    public void Build_ArrayWithTemporalProperty_ProducesChart()
    {
        var schema = new JsonDataSchema();
        var temporal = new JsonPropertyDescriptor
        {
            Name         = "date",
            DisplayName  = "Date",
            PropertyType = JsonPropertyType.DateTime
        };
        var numeric = new JsonPropertyDescriptor
        {
            Name         = "value",
            DisplayName  = "Value",
            PropertyType = JsonPropertyType.Number
        };

        var array = new JsonArrayDescriptor
        {
            Name             = "series",
            IsHomogeneous    = true,
            TemporalProperty = "date",
            RawData          = [
                new Dictionary<string, object?> { ["date"] = "2024-01", ["value"] = (object)100 }
            ]
        };
        array.ItemProperties.Add(temporal);
        array.ItemProperties.Add(numeric);
        array.NumericProperties.Add("value");
        schema.Arrays.Add(array);

        var widgets = _sut.Build(schema);

        widgets.Should().Contain(w => w.Type == WidgetType.Chart);
    }

    [Fact]
    public void Build_ChartWidget_PopulatesAvailableFields()
    {
        var schema = new JsonDataSchema();
        var array = new JsonArrayDescriptor
        {
            Name = "wasteByCategory",
            DisplayName = "Waste By Category",
            IsHomogeneous = true,
            CategoryProperty = "category",
            RawData =
            [
                new Dictionary<string, object?>
                {
                    ["category"] = "RAEE",
                    ["region"] = "Norte",
                    ["tons"] = (object)3200,
                    ["pct"] = (object)22.5,
                    ["cost"] = (object)45000
                }
            ]
        };
        array.ItemProperties.Add(new JsonPropertyDescriptor { Name = "category", DisplayName = "Category", PropertyType = JsonPropertyType.String });
        array.ItemProperties.Add(new JsonPropertyDescriptor { Name = "region", DisplayName = "Region", PropertyType = JsonPropertyType.String });
        array.ItemProperties.Add(new JsonPropertyDescriptor { Name = "tons", DisplayName = "Tons", PropertyType = JsonPropertyType.Number });
        array.ItemProperties.Add(new JsonPropertyDescriptor { Name = "pct", DisplayName = "Pct", PropertyType = JsonPropertyType.Number });
        array.ItemProperties.Add(new JsonPropertyDescriptor { Name = "cost", DisplayName = "Cost", PropertyType = JsonPropertyType.Number });
        array.NumericProperties.AddRange(["tons", "pct", "cost"]);
        schema.Arrays.Add(array);

        var widgets = _sut.Build(schema);
        var chart = widgets.Single(w => w.Type == WidgetType.Chart);

        chart.AvailableCategoryFields.Should().Equal("category", "region");
        chart.AvailableValueFields.Should().Equal("cost", "pct", "tons");
        chart.FieldDisplayNames.Should().ContainKeys("category", "region", "tons", "pct", "cost");
        chart.SourceArrayName.Should().Be("wasteByCategory");
    }

    // ── SortOrder creciente ──────────────────────────────────────────────

    [Fact]
    public void Build_ProducesWidgetsWithSortOrder()
    {
        var schema = new JsonDataSchema();
        schema.RootScalars.Add(new JsonPropertyDescriptor
        {
            Name         = "label",
            DisplayName  = "Label",
            PropertyType = JsonPropertyType.String,
            SampleValues = ["Test"]
        });
        schema.RootScalars.Add(new JsonPropertyDescriptor
        {
            Name         = "count",
            DisplayName  = "Count",
            PropertyType = JsonPropertyType.Number,
            SampleValues = ["5"]
        });

        var widgets = _sut.Build(schema);

        widgets.Select(w => w.SortOrder).Should().BeInAscendingOrder();
    }

    // ── Key-value list para objetos anidados ─────────────────────────────

    [Fact]
    public void Build_NestedObject_ProducesKeyValueListOrSectionHeader()
    {
        var schema = new JsonDataSchema();
        var nested = new JsonObjectDescriptor { Name = "metadata" };
        nested.Properties.Add(new JsonPropertyDescriptor
        {
            Name         = "author",
            DisplayName  = "Author",
            PropertyType = JsonPropertyType.String,
            SampleValues = ["Alice"]
        });
        schema.NestedObjects.Add(nested);

        var widgets = _sut.Build(schema);

        widgets.Should().Contain(w =>
            w.Type == WidgetType.KeyValueList || w.Type == WidgetType.SectionHeader);
    }
}
