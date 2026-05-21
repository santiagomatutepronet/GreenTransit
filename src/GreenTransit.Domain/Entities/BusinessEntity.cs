using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Actores del sistema (SCRAP, Carrier, Plant, CAC, Producer…). Tabla: Entities</summary>
public class BusinessEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? NationalId { get; set; }
    public string? CenterCode { get; set; }
    /// <summary>Source | Destination | Carrier | OperatorTransfer | SCRAP | Producer | Plant | CAC | PublicEntity | Coordinator | Other</summary>
    public string EntityRole { get; set; } = null!;
    public string? TypeThirdParty { get; set; }
    public string? InscriptionType { get; set; }
    public string? InscriptionNumber { get; set; }
    public string? CountryCode { get; set; }
    public string? StateCode { get; set; }
    public string? ZipCode { get; set; }
    public string? ProvinceCode { get; set; }
    public string? MunicipalityCode { get; set; }
    public string? Address { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? ContactPerson { get; set; }
    public string? EconomicActivity { get; set; }
    public string? EntityType { get; set; }
    public bool IsActive { get; set; } = true;
    public string? SourceSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    // Navegaciones inversas
    public ICollection<Residue> Residues { get; set; } = [];
    public ICollection<Agreement> AgreementsAsScrap { get; set; } = [];
    public ICollection<Agreement> AgreementsAsPublicEntity { get; set; } = [];
    public ICollection<Agreement> AgreementsAsCoordinator { get; set; } = [];
    public ICollection<ServiceOrder> ServiceOrdersAsIssuedBy { get; set; } = [];
    public ICollection<ServiceOrder> ServiceOrdersAsPickupPoint { get; set; } = [];
    public ICollection<ServiceOrder> ServiceOrdersAsCarrier { get; set; } = [];
    public ICollection<ServiceOrder> ServiceOrdersAsPlannedPlant { get; set; } = [];
    public ICollection<WasteMove> WasteMovesAsScrap { get; set; } = [];
    public ICollection<WasteMove> WasteMovesAsSource { get; set; } = [];
    public ICollection<WasteMove> WasteMovesAsDestination { get; set; } = [];
    public ICollection<WasteMoveResidue> WasteMoveResiduesAsCarrier { get; set; } = [];
    public ICollection<ProductDeclaration> ProductDeclarations { get; set; } = [];
    public ICollection<ProductSpec> ProductSpecs { get; set; } = [];
    public ICollection<Settlement> SettlementsAsScrap { get; set; } = [];
    public ICollection<Settlement> SettlementsAsPublicEntity { get; set; } = [];
    public ICollection<MarketShare> MarketShares { get; set; } = [];
}
