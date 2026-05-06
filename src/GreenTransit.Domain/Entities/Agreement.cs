using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Contratos marco entre SCRAP, entidades públicas y coordinadores. Tabla: Agreements</summary>
public class Agreement : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string AgreementNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public Guid? IdScrap { get; set; }
    public Guid? IdPublicEntity { get; set; }
    public Guid? IdCoordinator { get; set; }
    public string? WasteStream { get; set; }
    public string? SubStream { get; set; }
    public string? AutonomousCommunity { get; set; }
    public string? ProvinceCode { get; set; }
    public string? MunicipalityCode { get; set; }
    public string? CoveredMethodsJson { get; set; }
    public string? TariffModelType { get; set; }
    public string? Currency { get; set; }
    public string? TariffRulesJson { get; set; }
    public string? MinimumsJson { get; set; }
    public string? ObligationsJson { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    public BusinessEntity? Scrap { get; set; }
    public BusinessEntity? PublicEntity { get; set; }
    public BusinessEntity? Coordinator { get; set; }
    public ICollection<AgreementDocument> AgreementDocuments { get; set; } = [];
    public ICollection<Settlement> Settlements { get; set; } = [];

    public static class Statuses
    {
        public const string Draft     = "Draft";
        public const string Active    = "Active";
        public const string Expired   = "Expired";
        public const string Cancelled = "Cancelled";

        public static readonly IReadOnlyList<string> All = [Draft, Active, Expired, Cancelled];
    }
}
