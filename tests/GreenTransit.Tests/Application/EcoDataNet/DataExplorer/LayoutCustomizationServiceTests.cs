using FluentAssertions;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Application.Features.EcoDataNet.Services;

namespace GreenTransit.Tests.Application.EcoDataNet.DataExplorer;

/// <summary>
/// Tests del servicio LayoutCustomizationService: merge de overrides sobre widgets automáticos.
/// </summary>
public sealed class LayoutCustomizationServiceTests
{
    private readonly LayoutCustomizationService _sut = new();

    private static DynamicWidgetDescriptor Widget(string id, int sortOrder = 0) => new()
    {
        WidgetId   = id,
        Title      = id,
        SortOrder  = sortOrder,
        ColumnSpan = 6
    };

    private static DynamicWidgetDescriptor ChartWidget(ChartSubType chartType = ChartSubType.BarVertical) => new()
    {
        WidgetId = "chart1",
        Type = WidgetType.Chart,
        Title = "Chart 1",
        SortOrder = 0,
        ColumnSpan = 6,
        ChartType = chartType,
        ChartCategoryField = "category",
        ChartValueFields = ["tons", "percentage"],
        AvailableCategoryFields = ["category", "region"],
        AvailableValueFields = ["tons", "percentage", "cost"]
    };

    private static DynamicWidgetDescriptor TableWidget() => new()
    {
        WidgetId = "table1",
        Type = WidgetType.DataTable,
        Title = "Table 1",
        SortOrder = 0,
        ColumnSpan = 12,
        TableColumns =
        [
            new TableColumnDescriptor { PropertyName = "category", Title = "Category", DataType = "String" },
            new TableColumnDescriptor { PropertyName = "tons", Title = "Tons", DataType = "Number" },
            new TableColumnDescriptor { PropertyName = "percentage", Title = "Percentage", DataType = "Percentage", Width = 100 }
        ]
    };

    // ── Sin overrides ─────────────────────────────────────────────────────

    [Fact]
    public void ApplyOverrides_NoOverrides_ReturnsAutoWidgets()
    {
        var widgets = new List<DynamicWidgetDescriptor> { Widget("w1"), Widget("w2") };

        var result = _sut.ApplyOverrides(widgets, [], null, "hash1");

        result.Widgets.Should().HaveCount(2);
        result.SchemaChanged.Should().BeFalse();
        result.NewWidgetIds.Should().BeEmpty();
        result.ObsoleteWidgetIds.Should().BeEmpty();
    }

    // ── Override de título ────────────────────────────────────────────────

    [Fact]
    public void ApplyOverrides_CustomTitle_OverridesTitle()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1") };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "w1", CustomTitle = "Mi Título" }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single(w => w.WidgetId == "w1").Title.Should().Be("Mi Título");
    }

    // ── Override de orden ─────────────────────────────────────────────────

    [Fact]
    public void ApplyOverrides_CustomSortOrder_ReordersWidgets()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1", 0), Widget("w2", 1) };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "w1", CustomSortOrder = 10 },
            new() { WidgetId = "w2", CustomSortOrder = 1  }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets[0].WidgetId.Should().Be("w2");
        result.Widgets[1].WidgetId.Should().Be("w1");
    }

    // ── Override de visibilidad ───────────────────────────────────────────

    [Fact]
    public void ApplyOverrides_IsHidden_HidesWidget()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1"), Widget("w2") };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "w1", IsHidden = true }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single(w => w.WidgetId == "w1").IsHidden.Should().BeTrue();
        result.Widgets.Single(w => w.WidgetId == "w2").IsHidden.Should().BeFalse();
    }

    // ── Override de ColumnSpan ────────────────────────────────────────────

    [Fact]
    public void ApplyOverrides_CustomColumnSpan_UpdatesColumnSpan()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1") };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "w1", CustomColumnSpan = 12 }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ColumnSpan.Should().Be(12);
    }

    // ── Detección de cambio de esquema ────────────────────────────────────

    [Fact]
    public void ApplyOverrides_DifferentSchemaHash_SetsSchemaChanged()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1") };
        var overrides = new List<WidgetLayoutOverride> { new() { WidgetId = "w1" } };

        var result = _sut.ApplyOverrides(widgets, overrides, "old_hash", "new_hash");

        result.SchemaChanged.Should().BeTrue();
    }

    [Fact]
    public void ApplyOverrides_SameSchemaHash_SchemaChangedFalse()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1") };
        var overrides = new List<WidgetLayoutOverride> { new() { WidgetId = "w1" } };

        var result = _sut.ApplyOverrides(widgets, overrides, "same_hash", "same_hash");

        result.SchemaChanged.Should().BeFalse();
    }

    // ── Widgets nuevos y obsoletos ────────────────────────────────────────

    [Fact]
    public void ApplyOverrides_NewWidget_AppearsInNewWidgetIds()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1"), Widget("w_new") };
        var overrides = new List<WidgetLayoutOverride> { new() { WidgetId = "w1" } };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.NewWidgetIds.Should().Contain("w_new");
    }

    [Fact]
    public void ApplyOverrides_ObsoleteOverride_AppearsInObsoleteWidgetIds()
    {
        var widgets   = new List<DynamicWidgetDescriptor> { Widget("w1") };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "w1"      },
            new() { WidgetId = "w_gone"  }   // ya no existe
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.ObsoleteWidgetIds.Should().Contain("w_gone");
    }

    [Fact]
    public void ApplyOverrides_CustomCategoryField_Valid_AppliesField()
    {
        var widgets = new List<DynamicWidgetDescriptor> { ChartWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "chart1", CustomChartBinding = new ChartFieldBinding { CustomCategoryField = "region" } }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ChartCategoryField.Should().Be("region");
    }

    [Fact]
    public void ApplyOverrides_CustomCategoryField_Invalid_IgnoresField()
    {
        var widgets = new List<DynamicWidgetDescriptor> { ChartWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "chart1", CustomChartBinding = new ChartFieldBinding { CustomCategoryField = "nonExistentField" } }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ChartCategoryField.Should().Be("category");
    }

    [Fact]
    public void ApplyOverrides_CustomValueFields_Valid_AppliesFields()
    {
        var widgets = new List<DynamicWidgetDescriptor> { ChartWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "chart1", CustomChartBinding = new ChartFieldBinding { CustomValueFields = ["cost", "tons"] } }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ChartValueFields.Should().Equal("cost", "tons");
    }

    [Fact]
    public void ApplyOverrides_CustomValueFields_PartiallyValid_AppliesOnlyValid()
    {
        var widgets = new List<DynamicWidgetDescriptor> { ChartWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "chart1", CustomChartBinding = new ChartFieldBinding { CustomValueFields = ["tons", "obsoleteField"] } }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ChartValueFields.Should().Equal("tons");
    }

    [Fact]
    public void ApplyOverrides_CustomValueFields_AllObsolete_KeepsDefaults()
    {
        var widgets = new List<DynamicWidgetDescriptor> { ChartWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "chart1", CustomChartBinding = new ChartFieldBinding { CustomValueFields = ["foo", "bar"] } }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ChartValueFields.Should().Equal("tons", "percentage");
    }

    [Fact]
    public void ApplyOverrides_DonutChart_MultipleValueFields_TakesFirst()
    {
        var widgets = new List<DynamicWidgetDescriptor> { ChartWidget(ChartSubType.Donut) };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "chart1", CustomChartBinding = new ChartFieldBinding { CustomValueFields = ["cost", "tons"] } }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ChartValueFields.Should().Equal("cost");
    }

    [Fact]
    public void ApplyOverrides_NoChartBinding_NoChange()
    {
        var widgets = new List<DynamicWidgetDescriptor> { ChartWidget() };
        var overrides = new List<WidgetLayoutOverride> { new() { WidgetId = "chart1" } };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().ChartCategoryField.Should().Be("category");
        result.Widgets.Single().ChartValueFields.Should().Equal("tons", "percentage");
    }

    [Fact]
    public void ApplyOverrides_HiddenColumn_SetsIsHidden()
    {
        var widgets = new List<DynamicWidgetDescriptor> { TableWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "table1", CustomTableColumns = [new TableColumnOverride { PropertyName = "tons", IsHidden = true }] }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().TableColumns!.Single(c => c.PropertyName == "tons").IsHidden.Should().BeTrue();
        result.Widgets.Single().TableColumns!.Where(c => c.PropertyName != "tons").Should().OnlyContain(c => !c.IsHidden);
    }

    [Fact]
    public void ApplyOverrides_CustomColumnWidth_AppliesWidth()
    {
        var widgets = new List<DynamicWidgetDescriptor> { TableWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "table1", CustomTableColumns = [new TableColumnOverride { PropertyName = "category", CustomWidth = 200 }] }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().TableColumns!.Single(c => c.PropertyName == "category").Width.Should().Be(200);
    }

    [Fact]
    public void ApplyOverrides_HiddenAndResizedColumn_AppliesBoth()
    {
        var widgets = new List<DynamicWidgetDescriptor> { TableWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "table1", CustomTableColumns = [new TableColumnOverride { PropertyName = "percentage", IsHidden = true, CustomWidth = 150 }] }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");
        var column = result.Widgets.Single().TableColumns!.Single(c => c.PropertyName == "percentage");

        column.IsHidden.Should().BeTrue();
        column.Width.Should().Be(150);
    }

    [Fact]
    public void ApplyOverrides_ObsoleteColumnOverride_Ignored()
    {
        var widgets = new List<DynamicWidgetDescriptor> { TableWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new() { WidgetId = "table1", CustomTableColumns = [new TableColumnOverride { PropertyName = "nonExistent", IsHidden = true }] }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().TableColumns!.Should().OnlyContain(c => !c.IsHidden);
    }

    [Fact]
    public void ApplyOverrides_NoTableColumnOverrides_ColumnsUnchanged()
    {
        var widgets = new List<DynamicWidgetDescriptor> { TableWidget() };
        var overrides = new List<WidgetLayoutOverride> { new() { WidgetId = "table1" } };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");

        result.Widgets.Single().TableColumns!.Should().OnlyContain(c => !c.IsHidden);
        result.Widgets.Single().TableColumns!.Single(c => c.PropertyName == "percentage").Width.Should().Be(100);
    }

    [Fact]
    public void ApplyOverrides_MultipleColumnOverrides_AppliedCorrectly()
    {
        var widgets = new List<DynamicWidgetDescriptor> { TableWidget() };
        var overrides = new List<WidgetLayoutOverride>
        {
            new()
            {
                WidgetId = "table1",
                CustomTableColumns =
                [
                    new TableColumnOverride { PropertyName = "category", IsHidden = true },
                    new TableColumnOverride { PropertyName = "tons", CustomWidth = 180 },
                    new TableColumnOverride { PropertyName = "percentage", IsHidden = true, CustomWidth = 150 }
                ]
            }
        };

        var result = _sut.ApplyOverrides(widgets, overrides, "hash1", "hash1");
        var columns = result.Widgets.Single().TableColumns!;

        columns.Single(c => c.PropertyName == "category").IsHidden.Should().BeTrue();
        columns.Single(c => c.PropertyName == "tons").Width.Should().Be(180);
        columns.Single(c => c.PropertyName == "percentage").IsHidden.Should().BeTrue();
        columns.Single(c => c.PropertyName == "percentage").Width.Should().Be(150);
    }
}
