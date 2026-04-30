namespace GreenTransit.Application.Features.DumZones.DTOs;

/// <summary>DTO para el listado de zonas DUM.</summary>
public sealed record DumZoneDto(
    Guid    Id,
    string  ZoneCode,
    string  Name,
    string? Description,
    string  Status,
    int     RulesCount,
    string? MostRestrictiveAction
);

/// <summary>DTO de regla de restricción DUM.</summary>
public sealed record DumRestrictionRuleDto(
    Guid      Id,
    string    RuleCode,
    string    Status,
    DateTime  ValidFrom,
    DateTime? ValidTo,
    string    ConditionsJson,
    string    ActionType,
    string?   ActionReason
);

/// <summary>DTO de detalle completo de una zona DUM con sus reglas.</summary>
public sealed record DumZoneDetailDto(
    Guid                           Id,
    string                         ZoneCode,
    string                         Name,
    string?                        Description,
    string                         Status,
    string                         GeometryJson,
    int                            Version,
    DateTime                       CreatedAt,
    DateTime                       UpdatedAt,
    IReadOnlyList<DumRestrictionRuleDto> Rules
);

/// <summary>Resultado del simulador de restricciones DUM.</summary>
public sealed record DumSimulationResultDto(
    string                            ActionType,
    string?                           Reason,
    string[]                          ZoneCodes,
    IReadOnlyList<ActiveRuleApplied>  ActiveRulesApplied
);

public sealed record ActiveRuleApplied(
    string ZoneCode,
    string RuleCode,
    string ActionType,
    string? ActionReason,
    string ConditionsJson
);
