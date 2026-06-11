using FluentAssertions;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Application.Features.EcoDataNet.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreenTransit.Tests.Application.EcoDataNet.DataExplorer;

/// <summary>
/// Tests del servicio JsonSchemaAnalyzer: detección de arrays, escalares,
/// objetos anidados, heurísticas de tipos y manejo de entradas inválidas.
/// </summary>
public sealed class JsonSchemaAnalyzerTests
{
    private readonly JsonSchemaAnalyzer _sut = new(NullLogger<JsonSchemaAnalyzer>.Instance);

    // ── JSON inválido / vacío ──────────────────────────────────────────────

    [Fact]
    public void Analyze_NullOrEmpty_ReturnsNull()
    {
        _sut.Analyze(null!).Should().BeNull();
        _sut.Analyze("").Should().BeNull();
        _sut.Analyze("   ").Should().BeNull();
    }

    [Fact]
    public void Analyze_InvalidJson_ReturnsNull()
    {
        _sut.Analyze("not a json at all").Should().BeNull();
        _sut.Analyze("{broken").Should().BeNull();
    }

    // ── Raíz es un array ─────────────────────────────────────────────────

    [Fact]
    public void Analyze_RootArray_SetsRootIsArray()
    {
        const string json = """
        [
            {"id": 1, "name": "Alice", "score": 95.5},
            {"id": 2, "name": "Bob",   "score": 82.0}
        ]
        """;

        var schema = _sut.Analyze(json);

        schema.Should().NotBeNull();
        schema!.RootIsArray.Should().BeTrue();
        schema.Arrays.Should().HaveCount(1);
        schema.Arrays[0].ItemProperties.Should().HaveCount(3);
    }

    [Fact]
    public void Analyze_RootArray_DetectsNumericProperties()
    {
        const string json = """
        [
            {"id": 1, "score": 95.5},
            {"id": 2, "score": 82.0}
        ]
        """;

        var schema = _sut.Analyze(json);

        var arr = schema!.Arrays[0];
        (arr.NumericProperties.Contains("score") || arr.NumericProperties.Contains("id")).Should().BeTrue();
    }

    // ── Raíz es un objeto con escalares ──────────────────────────────────

    [Fact]
    public void Analyze_RootObject_DetectsScalars()
    {
        const string json = """
        {
            "version": "1.0",
            "active": true,
            "count": 42
        }
        """;

        var schema = _sut.Analyze(json);

        schema.Should().NotBeNull();
        schema!.RootIsArray.Should().BeFalse();
        schema.RootScalars.Should().HaveCount(3);
    }

    // ── Objeto con array anidado ─────────────────────────────────────────

    [Fact]
    public void Analyze_ObjectWithNestedArray_PopulatesArrays()
    {
        const string json = """
        {
            "title": "Report",
            "items": [
                {"sku": "A1", "qty": 10, "pct": 0.25},
                {"sku": "A2", "qty": 5,  "pct": 0.50}
            ]
        }
        """;

        var schema = _sut.Analyze(json);

        schema.Should().NotBeNull();
        schema!.Arrays.Should().HaveCount(1);
        schema.Arrays[0].Name.Should().Be("items");
        schema.Arrays[0].NumericProperties.Should().Contain("qty");
    }

    // ── Detección de porcentajes ─────────────────────────────────────────

    [Fact]
    public void Analyze_PercentageProperty_MarksIsPercentage()
    {
        const string json = """
        [
            {"label": "Cat A", "percentage": 0.35},
            {"label": "Cat B", "percentage": 0.65}
        ]
        """;

        var schema = _sut.Analyze(json);

        var arr  = schema!.Arrays[0];
        var prop = arr.ItemProperties.FirstOrDefault(p => p.Name == "percentage");
        prop.Should().NotBeNull();
        prop!.IsPercentage.Should().BeTrue();
    }

    // ── Detección de propiedad temporal ─────────────────────────────────

    [Fact]
    public void Analyze_DateProperty_SetsTemporalProperty()
    {
        const string json = """
        [
            {"date": "2024-01-15", "value": 100},
            {"date": "2024-02-15", "value": 200}
        ]
        """;

        var schema = _sut.Analyze(json);

        schema!.Arrays[0].TemporalProperty.Should().NotBeNullOrEmpty();
        schema.Arrays[0].TemporalProperty.Should().Be("date");
    }

    // ── Homogeneidad ─────────────────────────────────────────────────────

    [Fact]
    public void Analyze_HomogeneousArray_SetsIsHomogeneous()
    {
        const string json = """
        [
            {"id": 1, "value": 10},
            {"id": 2, "value": 20},
            {"id": 3, "value": 30}
        ]
        """;

        var schema = _sut.Analyze(json);

        schema!.Arrays[0].IsHomogeneous.Should().BeTrue();
    }

    // ── Objeto anidado dentro de objeto ─────────────────────────────────

    [Fact]
    public void Analyze_NestedObject_PopulatesNestedObjects()
    {
        const string json = """
        {
            "name": "root",
            "metadata": {
                "author": "Alice",
                "version": 2
            }
        }
        """;

        var schema = _sut.Analyze(json);

        schema!.NestedObjects.Should().HaveCount(1);
        schema.NestedObjects[0].Name.Should().Be("metadata");
        schema.NestedObjects[0].Properties.Should().HaveCount(2);
    }

    // ── TotalPropertyCount ───────────────────────────────────────────────

    [Fact]
    public void Analyze_Object_TotalPropertyCountMatchesScalars()
    {
        const string json = """{"a": 1, "b": 2, "c": 3}""";

        var schema = _sut.Analyze(json);

        schema!.TotalPropertyCount.Should().Be(3);
    }

    // ── JSON array vacío ─────────────────────────────────────────────────

    [Fact]
    public void Analyze_EmptyArray_ReturnsSchemaWithRootIsArray()
    {
        var schema = _sut.Analyze("[]");

        schema.Should().NotBeNull();
        schema!.RootIsArray.Should().BeTrue();
        // Un array vacío no genera descriptores (no hay propiedades que inferir)
        schema.Arrays.Should().BeEmpty();
    }
}
