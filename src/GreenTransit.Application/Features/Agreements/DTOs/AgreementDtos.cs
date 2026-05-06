namespace GreenTransit.Application.Features.Agreements.DTOs;

// ── Listado ───────────────────────────────────────────────────────────────────

/// <summary>DTO de lectura para listado de acuerdos.</summary>
public sealed record AgreementDto(
    Guid       Id,
    Guid?      OwnerId,
    string     AgreementNumber,
    string     Status,
    DateTime   EffectiveFrom,
    DateTime?  EffectiveTo,
    Guid?      IdScrap,
    string?    ScrapName,
    Guid?      IdPublicEntity,
    string?    PublicEntityName,
    Guid?      IdCoordinator,
    string?    CoordinatorName,
    string?    WasteStream,
    string?    SubStream,
    string?    AutonomousCommunity,
    string?    TariffModelType,
    string?    Currency,
    int        Version,
    DateTime   CreatedAt,
    DateTime   UpdatedAt
);

// ── Detalle ───────────────────────────────────────────────────────────────────

/// <summary>DTO de detalle de un acuerdo (incluye documentos).</summary>
public sealed record AgreementDetailDto(
    Guid       Id,
    Guid?      OwnerId,
    string     AgreementNumber,
    string     Status,
    DateTime   EffectiveFrom,
    DateTime?  EffectiveTo,
    Guid?      IdScrap,
    string?    ScrapName,
    Guid?      IdPublicEntity,
    string?    PublicEntityName,
    Guid?      IdCoordinator,
    string?    CoordinatorName,
    string?    WasteStream,
    string?    SubStream,
    string?    AutonomousCommunity,
    string?    ProvinceCode,
    string?    MunicipalityCode,
    string?    CoveredMethodsJson,
    string?    TariffModelType,
    string?    Currency,
    string?    TariffRulesJson,
    string?    MinimumsJson,
    string?    ObligationsJson,
    string?    Hash,
    int        Version,
    DateTime   CreatedAt,
    DateTime   UpdatedAt,
    IReadOnlyList<AgreementDocumentDto> Documents
);

// ── Documento ─────────────────────────────────────────────────────────────────

/// <summary>DTO de documento adjunto a un acuerdo.</summary>
public sealed record AgreementDocumentDto(
    Guid       Id,
    Guid       AgreementId,
    string     DocumentType,
    string?    DocumentId,
    string?    DocumentHash,
    DateTime?  SignedAt,
    string?    SignatureProvider
);

// ── Próximos a vencer ─────────────────────────────────────────────────────────

/// <summary>DTO para alertas de acuerdos próximos a vencer.</summary>
public sealed record ExpiringAgreementDto(
    Guid      Id,
    string    AgreementNumber,
    string?   ScrapName,
    string?   PublicEntityName,
    DateTime  EffectiveTo,
    int       DaysRemaining
);
