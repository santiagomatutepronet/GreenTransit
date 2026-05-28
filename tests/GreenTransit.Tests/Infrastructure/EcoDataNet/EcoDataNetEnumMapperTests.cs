using GreenTransit.Infrastructure.ExternalApis.EcoDataNet;
using Xunit;

namespace GreenTransit.Tests.Infrastructure.EcoDataNet;

/// <summary>
/// Tests unitarios para EcoDataNetEnumMapper.
/// Verifica que cada mapeo devuelve el entero correcto esperado por la API EcoDataNet Waste.
/// </summary>
public sealed class EcoDataNetEnumMapperTests
{
    // -- ToMeasureUnit: 1=Gr, 2=Kg, 3=Tm, 4=Ud

    [Theory]
    [InlineData("KG",         2)]
    [InlineData("kg",         2)]
    [InlineData("KILOGRAMOS", 2)]
    [InlineData("GR",         1)]
    [InlineData("gr",         1)]
    [InlineData("GRAMOS",     1)]
    [InlineData("TM",         3)]
    [InlineData("tm",         3)]
    [InlineData("TONELADAS",  3)]
    [InlineData("UD",         4)]
    [InlineData("ud",         4)]
    [InlineData("UNIDADES",   4)]
    public void ToMeasureUnit_KnownValues_ReturnsExpected(string input, int expected)
        => Assert.Equal(expected, EcoDataNetEnumMapper.ToMeasureUnit(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OTRO")]
    public void ToMeasureUnit_UnknownOrNull_ReturnsNull(string? input)
        => Assert.Null(EcoDataNetEnumMapper.ToMeasureUnit(input));

    [Fact]
    public void ToMeasureUnit_NumericString_ParsedDirectly()
        => Assert.Equal(7, EcoDataNetEnumMapper.ToMeasureUnit("7"));

    // -- ToTypeContainer (exact-match, case-sensitive)

    [Theory]
    [InlineData("Contenedor",    8)]
    [InlineData("Jaula",         11)]
    [InlineData("Prensa",        13)]
    [InlineData("Semiremolque",  14)]
    [InlineData("Balas",         2)]
    public void ToTypeContainer_KnownValues_ReturnsExpected(string input, int expected)
        => Assert.Equal(expected, EcoDataNetEnumMapper.ToTypeContainer(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contenedor")]
    [InlineData("Desconocido")]
    public void ToTypeContainer_UnknownOrNull_ReturnsNull(string? input)
        => Assert.Null(EcoDataNetEnumMapper.ToTypeContainer(input));

    [Fact]
    public void ToTypeContainer_NumericString_ParsedDirectly()
        => Assert.Equal(5, EcoDataNetEnumMapper.ToTypeContainer("5"));

    // -- ToUseProduct: 1=Domestico, 2=Profesional

    [Theory]
    [InlineData("DOMESTICO",   1)]
    [InlineData("domestico",   1)]
    [InlineData("PROFESIONAL", 2)]
    [InlineData("profesional", 2)]
    [InlineData("1",           1)]
    [InlineData("2",           2)]
    public void ToUseProduct_KnownValues_ReturnsExpected(string input, int expected)
        => Assert.Equal(expected, EcoDataNetEnumMapper.ToUseProduct(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AEE")]
    public void ToUseProduct_UnknownOrNull_ReturnsNull(string? input)
        => Assert.Null(EcoDataNetEnumMapper.ToUseProduct(input));

    // -- ToCategoryProduct: 1=AEE, 2=A1, 3=A2

    [Theory]
    [InlineData("AEE", 1)]
    [InlineData("A1",  2)]
    [InlineData("A2",  3)]
    public void ToCategoryProduct_KnownValues_ReturnsExpected(string input, int expected)
        => Assert.Equal(expected, EcoDataNetEnumMapper.ToCategoryProduct(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XXX")]
    public void ToCategoryProduct_UnknownOrNull_ReturnsNull(string? input)
        => Assert.Null(EcoDataNetEnumMapper.ToCategoryProduct(input));

    [Fact]
    public void ToCategoryProduct_NumericString_ParsedDirectly()
        => Assert.Equal(9, EcoDataNetEnumMapper.ToCategoryProduct("9"));

    // -- ToTypeThirdParty: 1=PuntoDeRecogida, 2=Gestor, 3=SCRAP, 4=OperadorTraslado

    [Theory]
    [InlineData("PuntoDeRecogida",  1)]
    [InlineData("Gestor",           2)]
    [InlineData("SCRAP",            3)]
    [InlineData("OperadorTraslado", 4)]
    public void ToTypeThirdParty_KnownValues_ReturnsExpected(string input, int expected)
        => Assert.Equal(expected, EcoDataNetEnumMapper.ToTypeThirdParty(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Desconocido")]
    public void ToTypeThirdParty_UnknownOrNull_ReturnsNull(string? input)
        => Assert.Null(EcoDataNetEnumMapper.ToTypeThirdParty(input));

    // -- Fallback numerico funciona en todos los mappers

    [Fact]
    public void AllMappers_NumericString_ReturnParsedInt()
    {
        Assert.Equal(42, EcoDataNetEnumMapper.ToMeasureUnit("42"));
        Assert.Equal(42, EcoDataNetEnumMapper.ToTypeContainer("42"));
        Assert.Equal(42, EcoDataNetEnumMapper.ToUseProduct("42"));
        Assert.Equal(42, EcoDataNetEnumMapper.ToCategoryProduct("42"));
        Assert.Equal(42, EcoDataNetEnumMapper.ToTypeThirdParty("42"));
    }

    // -- Valores desconocidos siempre null

    [Fact]
    public void AllMappers_ReturnNullForUnknownValues()
    {
        const string unknown = "VALOR_INVENTADO_XYZ_999";
        Assert.Null(EcoDataNetEnumMapper.ToMeasureUnit(unknown));
        Assert.Null(EcoDataNetEnumMapper.ToTypeContainer(unknown));
        Assert.Null(EcoDataNetEnumMapper.ToUseProduct(unknown));
        Assert.Null(EcoDataNetEnumMapper.ToCategoryProduct(unknown));
        Assert.Null(EcoDataNetEnumMapper.ToTypeThirdParty(unknown));
    }
}
