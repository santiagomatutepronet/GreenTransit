namespace GreenTransit.Application.Features.Ecomodulation.DTOs;

// ── EcoModulationRuleSet — DTOs de gestión CRUD ───────────────────────────────

/// <summary>DTO de listado de conjuntos de reglas de ecomodulación.</summary>
public sealed record EcoModulationRuleSetDto(
    Guid      Id,
    string    RuleSetName,
    string    Version,
    string    Status,
    DateTime  ValidFrom,
    DateTime? ValidTo,
    string?   PublisherName,
    string?   PublisherNationalId,
    string?   PublisherCenterCode,
    int       RuleCount,
    DateTime  CreatedAt
);

/// <summary>DTO de detalle completo de un conjunto de reglas (incluye líneas).</summary>
public sealed record EcoModulationRuleSetDetailDto(
    Guid      Id,
    string    RuleSetName,
    string    Version,
    string    Status,
    DateTime  ValidFrom,
    DateTime? ValidTo,
    string?   PublisherName,
    string?   PublisherNationalId,
    string?   PublisherCenterCode,
    DateTime  CreatedAt,
    IReadOnlyList<EcoModulationRuleDto> Rules
);

/// <summary>DTO de una regla individual de ecomodulación.</summary>
public sealed record EcoModulationRuleDto(
    Guid    Id,
    string  RuleCode,
    int?    ProductCategory,
    string  CriteriaJson,
    string  FeeImpactType,
    decimal FeeImpactValue
);

/// <summary>Línea de regla para los comandos de creación/actualización.</summary>
public sealed record EcoModulationRuleLineDto(
    string  RuleCode,
    int?    ProductCategory,
    string  CriteriaJson,
    string  FeeImpactType,
    decimal FeeImpactValue
);
