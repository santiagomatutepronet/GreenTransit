namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet;

/// <summary>
/// Conversores de strings/enums de GreenTransit a los valores int de la API EcoDataNet Waste.
/// </summary>
public static class EcoDataNetEnumMapper
{
    // MeasureUnitEnum: 1=Gr, 2=Kg, 3=Tm, 4=Ud
    public static int? ToMeasureUnit(string? value) => value?.ToUpperInvariant() switch
    {
        "GR" or "GRAMOS"     => 1,
        "KG" or "KILOGRAMOS" => 2,
        "TM" or "TONELADAS"  => 3,
        "UD" or "UNIDADES"   => 4,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // TypeContainerEnum: 1..17
    public static int? ToTypeContainer(string? value) => value switch
    {
        "Auto_Compactador"           => 1,
        "Balas"                      => 2,
        "Barca"                      => 3,
        "Barca_5_m3"                 => 4,
        "Barca_9_m3"                 => 5,
        "Barca_con_tapa"             => 6,
        "Caja_Compactador_Estático"  => 7,
        "Contenedor"                 => 8,
        "Contenedor_1100L"           => 9,
        "Contenedor_con_Tapa"        => 10,
        "Jaula"                      => 11,
        "Jaula_Doble"                => 12,
        "Prensa"                     => 13,
        "Semiremolque"               => 14,
        "Volteador"                  => 15,
        "Contenedor_C30"             => 16,
        "Contenedor_C20"             => 17,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // UseProductEnum: 1=Domestico, 2=Profesional
    public static int? ToUseProduct(string? value) => value?.ToUpperInvariant() switch
    {
        "DOMESTICO"   or "1" => 1,
        "PROFESIONAL" or "2" => 2,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // CategoryProductEnum: 1=AEE, 2=A1, 3=A2, …
    // CategoryProductEnum: 1=AEE/RAEE, 2=A1/Envases, 3=A2/Voluminosos, 4=Pilas, 5=Neumáticos
    public static int? ToCategoryProduct(string? value) => value switch
    {
        "AEE"  or "RAEE"        => 1,
        "A1"   or "Envases"     => 2,
        "A2"   or "Voluminosos" => 3,
        "Pilas"                 => 4,
        "Neumáticos"            => 5,
        _ => int.TryParse(value, out var v) ? v : null
    };

    // TypeThirdPartyEnum: 1=PuntoDeRecogida, 2=Gestor, 3=SCRAP, 4=OperadorTraslado
    public static int? ToTypeThirdParty(string? value) => value switch
    {
        "PuntoDeRecogida"  => 1,
        "Gestor"           => 2,
        "SCRAP"            => 3,
        "OperadorTraslado" => 4,
        _ => int.TryParse(value, out var v) ? v : null
    };
}
