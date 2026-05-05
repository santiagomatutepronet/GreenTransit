namespace GreenTransit.Application.Features.ProductDeclarations.DTOs;

// ── Listado ───────────────────────────────────────────────────────────────────

/// <summary>DTO de lectura para listado de declaraciones de producción.</summary>
public sealed record ProductDeclarationDto(
    Guid       Id,
    Guid?      OwnerId,
    int?       Period,
    int?       Year,
    int?       Month,
    string?    Currency,
    string?    State,
    DateTime?  DateCreate,
    DateTime?  DateEmit,
    string?    Reference,
    Guid?      IdProducer,
    string?    ProducerName,
    decimal?   Amount,
    string?    Type,
    DateTime?  DateCreateSys,
    DateTime?  DateModifiedSys
);

// ── Detalle ───────────────────────────────────────────────────────────────────

/// <summary>DTO de lectura para detalle de una declaración de producción (incluye líneas).</summary>
public sealed record ProductDeclarationDetailDto(
    Guid       Id,
    Guid?      OwnerId,
    int?       Period,
    int?       Year,
    int?       Month,
    string?    Currency,
    string?    State,
    DateTime?  DateCreate,
    DateTime?  DateEmit,
    string?    Reference,
    Guid?      IdProducer,
    string?    ProducerName,
    string?    ProducerNationalId,
    string?    ProducerCenterCode,
    decimal?   Amount,
    string?    Type,
    DateTime?  DateCreateSys,
    DateTime?  DateModifiedSys,
    IReadOnlyList<ProductLineDto> Products
);

// ── Línea de producto ─────────────────────────────────────────────────────────

/// <summary>DTO de lectura para una línea de producto de la declaración.</summary>
public sealed record ProductLineDto(
    Guid     Id,
    Guid     IdProductDeclaration,
    Guid?    IdResidue,
    string?  ResidueName,
    string?  ResidueReference,
    string?  ResidueCategory,
    string?  Reference,
    string?  Source,
    string?  SourceDescription,
    string?  ProductUse,
    string?  ProductCategory,
    decimal? Quantity,
    string?  MeasureUnit,
    int?     Units,
    decimal? Price
);

// ── Dashboard ─────────────────────────────────────────────────────────────────

/// <summary>KPIs del panel de declaraciones de producción.</summary>
public sealed record ProductDeclarationDashboardDto(
    Dictionary<string, int> DeclarationsByState,
    decimal                 TotalDeclaredAmount,
    decimal                 TotalDeclaredQuantity,
    IReadOnlyList<TopProductDto> TopProducts,
    int                     ProducersWithoutDeclaration
);

/// <summary>Ranking de productos declarados por volumen.</summary>
public sealed record TopProductDto(
    Guid?    IdResidue,
    string?  ResidueName,
    decimal  TotalQuantity
);
