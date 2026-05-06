namespace GreenTransit.Domain.Constants;

/// <summary>
/// Catálogo cerrado y controlado de flujos y subflujos de residuos.
/// Fuente de verdad única — no duplicar en otros sitios.
/// </summary>
public static class WasteFlowCatalog
{
    public const string TypeRap       = "RAP";
    public const string TypeOperativo = "OPERATIVO";

    public sealed record WasteFlow(string Code, string Name, string Type, IReadOnlyList<string> Subflows);

    public static readonly IReadOnlyList<WasteFlow> All =
    [
        // ── RAP ──────────────────────────────────────────────────────────────
        new("RAEE", "Residuos Aparatos Eléctricos y Electrónicos", TypeRap,
        [
            "Grandes electrodomésticos",
            "Pequeños electrodomésticos",
            "Informática y telecomunicaciones",
            "Electrónica de consumo",
            "Aparatos de alumbrado",
            "Herramientas eléctricas",
            "Juguetes/deporte",
            "Equipos médicos",
            "Instrumentos de control",
            "Máquinas expendedoras",
            "Paneles fotovoltaicos"
        ]),
        new("BAT", "Pilas y acumuladores", TypeRap,
        [
            "Pilas alcalinas/salinas",
            "Pilas botón",
            "Baterías portátiles",
            "Baterías industriales",
            "Baterías automoción"
        ]),
        new("NFU", "Neumáticos fuera de uso", TypeRap,
        [
            "Turismo",
            "Camión/industrial",
            "Agrícola",
            "OTR",
            "Bicicleta"
        ]),
        new("OIL", "Aceites industriales usados", TypeRap,
        [
            "Aceite motor",
            "Aceite hidráulico/industrial",
            "Aceites contaminados"
        ]),
        new("PACK", "Envases", TypeRap,
        [
            "Domésticos ligeros",
            "Domésticos papel-cartón",
            "Domésticos vidrio",
            "Comerciales",
            "Industriales",
            "Agrarios",
            "Fitosanitarios",
            "Medicamentos",
            "Un solo uso",
            "Reutilizable"
        ]),

        // ── Operativos ────────────────────────────────────────────────────────
        new("BIO", "Biorresiduos", TypeOperativo,
        [
            "Orgánica doméstica",
            "Orgánica comercial",
            "Restos vegetales"
        ]),
        new("PAPER", "Papel y cartón (no envase)", TypeOperativo,
        [
            "Papel oficina",
            "Cartón comercial",
            "Mezcla"
        ]),
        new("GLASS", "Vidrio (no envase)", TypeOperativo,
        [
            "Vidrio plano",
            "Vidrio industrial",
            "Mezcla"
        ]),
        new("PLASTIC", "Plásticos (no envase)", TypeOperativo,
        [
            "Film",
            "Rígido",
            "Mezcla"
        ]),
        new("METAL", "Metales", TypeOperativo,
        [
            "Férricos",
            "No férricos",
            "Chatarra"
        ]),
        new("WOOD", "Madera", TypeOperativo,
        [
            "Natural",
            "Palets",
            "Tratada"
        ]),
        new("TEXTILE", "Textil", TypeOperativo,
        [
            "Reutilizable",
            "Reciclaje",
            "Rechazo"
        ]),
        new("RCD", "Construcción y demolición", TypeOperativo,
        [
            "Inertes",
            "Mezcla",
            "Peligrosos"
        ]),
        new("HAZ", "Residuos peligrosos", TypeOperativo,
        [
            "Disolventes",
            "Pinturas",
            "Envases contaminados",
            "Reactivos químicos"
        ]),
        new("SLUDGE", "Lodos", TypeOperativo,
        [
            "Industriales",
            "EDAR",
            "Peligrosos"
        ]),
    ];

    /// <summary>Devuelve el flujo que coincide con el código dado, o null si no existe.</summary>
    public static WasteFlow? FindByCode(string? code)
        => string.IsNullOrEmpty(code)
            ? null
            : All.FirstOrDefault(f => f.Code == code);

    /// <summary>
    /// Valida que la combinación flujo+subflujo sea correcta.
    /// Devuelve true si subflujo es nulo/vacío (campo opcional).
    /// </summary>
    public static bool IsValidCombination(string? flowCode, string? subflow)
    {
        if (string.IsNullOrEmpty(subflow)) return true;
        var flow = FindByCode(flowCode);
        return flow is not null && flow.Subflows.Contains(subflow);
    }
}
