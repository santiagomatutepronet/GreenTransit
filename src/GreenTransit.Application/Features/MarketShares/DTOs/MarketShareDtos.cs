namespace GreenTransit.Application.Features.MarketShares.DTOs;

/// <summary>DTO resumido de cuota de mercado para listados.</summary>
public sealed record MarketShareDto(
    Guid     Id,
    Guid?    IdScrap,
    string?  ScrapName,
    string   Category,
    string?  AutonomousCommunity,
    int      Year,
    decimal  Weight,
    int?     Period,
    string?  FlowType,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo
);

/// <summary>DTO de cumplimiento de cuota de mercado.</summary>
public sealed record MarketShareComplianceDto(
    Guid     MarketShareId,
    Guid?    IdScrap,
    string?  ScrapName,
    string   Category,
    string?  AutonomousCommunity,
    int      Year,
    int?     Period,
    decimal  ObjectiveKg,
    decimal  AchievedKg,
    decimal  CompliancePercent,
    bool     IsAtRisk
);
