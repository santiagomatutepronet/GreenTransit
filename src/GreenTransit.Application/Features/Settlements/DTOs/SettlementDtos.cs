namespace GreenTransit.Application.Features.Settlements.DTOs;

/// <summary>DTO resumido de liquidación para uso en listados y detalles de acuerdo.</summary>
public sealed record SettlementSummaryDto(
    Guid      Id,
    string    SettlementNumber,
    string    Status,
    Guid      AgreementId,
    string    AgreementNumber,
    int       Year,
    int?      Month,
    Guid?     IdScrap,
    string?   ScrapName,
    Guid?     IdPublicEntity,
    string?   PublicEntityName,
    decimal   BaseAmount,
    decimal   AdjustmentsAmount,
    decimal   TaxAmount,
    decimal   TotalAmount,
    string    Currency,
    string?   Validator,
    DateTime? ValidatedAt,
    DateTime  CreatedAt
);

/// <summary>DTO de línea de liquidación.</summary>
public sealed record SettlementLineDto(
    Guid    Id,
    int?    ProductCategory,
    Guid?   IdLERCode,
    string? LerCodeCode,
    string? LerCodeDescription,
    decimal WeightKg,
    decimal PricePerKg,
    decimal Amount,
    string? EvidenceType,
    string? SourceIdsJson
);

/// <summary>DTO de detalle completo de liquidación.</summary>
public sealed record SettlementDetailDto(
    Guid      Id,
    string    SettlementNumber,
    string    Status,
    Guid      AgreementId,
    string    AgreementNumber,
    int       Year,
    int?      Month,
    Guid?     IdScrap,
    string?   ScrapName,
    Guid?     IdPublicEntity,
    string?   PublicEntityName,
    decimal   BaseAmount,
    decimal   AdjustmentsAmount,
    decimal   TaxAmount,
    decimal   TotalAmount,
    string    Currency,
    string?   EvidenceRefsJson,
    string?   Validator,
    string?   ValidationStatus,
    DateTime? ValidatedAt,
    string?   ValidationRef,
    int       Version,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    IReadOnlyList<SettlementLineDto> Lines
);
