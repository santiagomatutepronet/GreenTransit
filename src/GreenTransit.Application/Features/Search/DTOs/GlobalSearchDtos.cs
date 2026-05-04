namespace GreenTransit.Application.Features.Search.DTOs;

/// <summary>Tipo de resultado del buscador global. Determina el ícono en la UI.</summary>
public enum GlobalSearchItemType
{
    WasteMove,
    ServiceOrder,
    EntryPlant,
    Agreement,
    Entity
}

/// <summary>Resultado individual del buscador global.</summary>
public sealed record GlobalSearchItemDto(
    string               Id,
    string               DisplayText,
    string               SecondaryText,
    GlobalSearchItemType Type,
    string               NavigationUrl
);

/// <summary>Resultado agregado del buscador global, con una lista por tipo (máx. 5 por tipo).</summary>
public sealed record GlobalSearchResultDto(
    IReadOnlyList<GlobalSearchItemDto> Items
);
