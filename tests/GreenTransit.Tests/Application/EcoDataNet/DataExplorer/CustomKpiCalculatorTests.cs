using FluentAssertions;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Application.Features.EcoDataNet.Services;

namespace GreenTransit.Tests.Application.EcoDataNet.DataExplorer;

/// <summary>
/// Tests del servicio CustomKpiCalculator:
/// operaciones Sum, Count, Average, Min, Max y Percentage, y formateo.
/// </summary>
public sealed class CustomKpiCalculatorTests
{
    private readonly CustomKpiCalculator _sut = new();

    private static List<Dictionary<string, object?>> Rows(params (string key, object? value)[][] rows)
        => rows.Select(r => r.ToDictionary(kv => kv.key, kv => kv.value)).ToList();

    // ── Sum ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_Sum_ReturnsCorrectValue()
    {
        var data = Rows(
            [("tons", (object?)10.0)],
            [("tons", (object?)20.0)],
            [("tons", (object?)30.0)]);

        var def = new CustomKpiDefinition { Operation = KpiOperation.Sum, PrimaryField = "tons" };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().BeApproximately(60.0, 0.001);
    }

    // ── Count ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_Count_ReturnsNumberOfRows()
    {
        var data = Rows(
            [("x", (object?)1.0)],
            [("x", (object?)2.0)],
            [("x", (object?)3.0)]);

        var def = new CustomKpiDefinition { Operation = KpiOperation.Count, PrimaryField = "x" };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().Be(3);
    }

    // ── Average ───────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_Average_ReturnsArithmeticMean()
    {
        var data = Rows(
            [("v", (object?)10.0)],
            [("v", (object?)20.0)],
            [("v", (object?)30.0)]);

        var def = new CustomKpiDefinition { Operation = KpiOperation.Average, PrimaryField = "v" };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().BeApproximately(20.0, 0.001);
    }

    // ── Min / Max ─────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_Min_ReturnsMinimumValue()
    {
        var data = Rows(
            [("v", (object?)5.0)],
            [("v", (object?)2.0)],
            [("v", (object?)9.0)]);

        var def = new CustomKpiDefinition { Operation = KpiOperation.Min, PrimaryField = "v" };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().Be(2.0);
    }

    [Fact]
    public void Calculate_Max_ReturnsMaximumValue()
    {
        var data = Rows(
            [("v", (object?)5.0)],
            [("v", (object?)2.0)],
            [("v", (object?)9.0)]);

        var def = new CustomKpiDefinition { Operation = KpiOperation.Max, PrimaryField = "v" };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().Be(9.0);
    }

    // ── Percentage ────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_Percentage_ReturnsCorrectPercent()
    {
        var data = Rows(
            [("recycled", (object?)25.0), ("total", (object?)100.0)],
            [("recycled", (object?)25.0), ("total", (object?)100.0)]);

        var def = new CustomKpiDefinition
        {
            Operation      = KpiOperation.Percentage,
            PrimaryField   = "recycled",
            SecondaryField = "total"
        };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().BeApproximately(25.0, 0.001);
    }

    [Fact]
    public void Calculate_Percentage_ZeroDenominator_ReturnsNull()
    {
        var data = Rows([("recycled", (object?)25.0), ("total", (object?)0.0)]);

        var def = new CustomKpiDefinition
        {
            Operation      = KpiOperation.Percentage,
            PrimaryField   = "recycled",
            SecondaryField = "total"
        };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().BeNull();
    }

    // ── Formato ───────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_PercentFormat_DisplayIncludesPercentSign()
    {
        var data = Rows([("v", (object?)42.5)]);
        var def = new CustomKpiDefinition
        {
            Operation     = KpiOperation.Sum,
            PrimaryField  = "v",
            DisplayFormat = KpiDisplayFormat.Percent,
            DecimalPlaces = 1
        };
        var (_, display) = _sut.Calculate(def, data);

        display.Should().Contain("%");
    }

    [Fact]
    public void Calculate_CustomSuffix_DisplayIncludesSuffix()
    {
        var data = Rows([("v", (object?)100.0)]);
        var def = new CustomKpiDefinition
        {
            Operation    = KpiOperation.Sum,
            PrimaryField = "v",
            CustomSuffix = "kg"
        };
        var (_, display) = _sut.Calculate(def, data);

        display.Should().EndWith("kg");
    }

    [Fact]
    public void Calculate_EmptyData_ReturnsNullAndDash()
    {
        var def = new CustomKpiDefinition { Operation = KpiOperation.Sum, PrimaryField = "v" };
        var (value, display) = _sut.Calculate(def, new List<Dictionary<string, object?>>());

        value.Should().BeNull();
        display.Should().Be("—");
    }

    // ── Retrocompatibilidad: campo ausente ────────────────────────────────────

    [Fact]
    public void Calculate_Sum_FieldMissingInSomeRows_SkipsMissingRows()
    {
        var data = new List<Dictionary<string, object?>>
        {
            new() { ["v"] = (object?)10.0 },
            new() { ["other"] = (object?)99.0 },  // sin campo "v"
            new() { ["v"] = (object?)20.0 }
        };
        var def = new CustomKpiDefinition { Operation = KpiOperation.Sum, PrimaryField = "v" };
        var (value, _) = _sut.Calculate(def, data);

        value.Should().BeApproximately(30.0, 0.001);
    }
}
