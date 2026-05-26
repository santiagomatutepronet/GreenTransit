using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Abstracción del DbContext expuesta a la capa Application.
/// Expone IQueryable&lt;T&gt; para consultas y métodos de mutación genéricos,
/// eliminando la dependencia directa de EF Core en la capa Application.
/// </summary>
public interface IApplicationDbContext
{
    // ── Catálogos ─────────────────────────────────────────────────────────────
    IQueryable<BusinessEntity> BusinessEntities { get; }
    IQueryable<LerCode> LerCodes { get; }
    IQueryable<Residue> Residues { get; }
    IQueryable<TreatmentOperation> TreatmentOperations { get; }

    // ── Contratos ─────────────────────────────────────────────────────────────
    IQueryable<Agreement> Agreements { get; }
    IQueryable<AgreementDocument> AgreementDocuments { get; }

    // ── Operaciones logísticas ────────────────────────────────────────────────
    IQueryable<ServiceOrder> ServiceOrders { get; }
    IQueryable<ServiceOrderResidue> ServiceOrderResidues { get; }
    IQueryable<WasteMove> WasteMoves { get; }
    IQueryable<WasteMoveResidue> WasteMoveResidues { get; }

    // ── Entradas y tratamiento ────────────────────────────────────────────────
    IQueryable<EntryPlant> EntryPlants { get; }
    IQueryable<EntryPlantResidue> EntryPlantResidues { get; }
    IQueryable<TreatmentPlant> TreatmentPlants { get; }
    IQueryable<TreatmentPlantResidue> TreatmentPlantResidues { get; }
    IQueryable<EntryCAC> EntryCACs { get; }
    IQueryable<EntryCACResidue> EntryCACResidues { get; }

    // ── Producto y declaraciones ──────────────────────────────────────────────
    IQueryable<ProductDeclaration> ProductDeclarations { get; }
    IQueryable<Product> Products { get; }
    IQueryable<ProductSpec> ProductSpecs { get; }
    IQueryable<MarketShare> MarketShares { get; }

    // ── Liquidaciones ─────────────────────────────────────────────────────────
    IQueryable<Settlement> Settlements { get; }
    IQueryable<SettlementLine> SettlementLines { get; }

    // ── Sostenibilidad ────────────────────────────────────────────────────────
    IQueryable<EmissionFactorSet> EmissionFactorSets { get; }
    IQueryable<EmissionFactor> EmissionFactors { get; }
    IQueryable<EcoModulationRuleSet> EcoModulationRuleSets { get; }
    IQueryable<EcoModulationRule> EcoModulationRules { get; }
    IQueryable<PlantEnergy> PlantEnergies { get; }
    IQueryable<Incident> Incidents { get; }
    IQueryable<DumZone> DumZones { get; }
    IQueryable<DumRestrictionRule> DumRestrictionRules { get; }
    IQueryable<RegulatoryTarget> RegulatoryTargets { get; }

    // ── Geografía ─────────────────────────────────────────────────────────────
    IQueryable<Country>              Countries              { get; }
    IQueryable<TerritoryState>       TerritoryStates        { get; }
    IQueryable<Province>             Provinces              { get; }
    IQueryable<Municipality>         Municipalities         { get; }
    IQueryable<MunicipalityZipCode>  MunicipalityZipCodes   { get; }

    // ── Seguridad ─────────────────────────────────────────────────────────────
    IQueryable<AppUser>     AppUsers     { get; }
    IQueryable<UserProfile> UserProfiles { get; }
    IQueryable<PageDefinition>  PageDefinitions  { get; }
    IQueryable<PagePermission>  PagePermissions  { get; }

    // ── EcoDataNet ────────────────────────────────────────────────────────────
    IQueryable<UserEDCConnector>   UserEDCConnectors   { get; }
    IQueryable<ProfileEDCConsumer> ProfileEDCConsumers { get; }

    // ── Diccionarios declaraciones ────────────────────────────────────────────
    IQueryable<DicProductDeclarationCategory> DicProductDeclarationCategories { get; }
    IQueryable<DicProductDeclarationPeriod>   DicProductDeclarationPeriods    { get; }
    IQueryable<DicProductDeclarationProduct>  DicProductDeclarationProducts   { get; }
    IQueryable<DicProductDeclarationSource>   DicProductDeclarationSources    { get; }
    IQueryable<DicProductDeclarationType>     DicProductDeclarationTypes      { get; }
    IQueryable<DicProductDeclarationUse>      DicProductDeclarationUses       { get; }

    // ── Mutaciones genéricas ──────────────────────────────────────────────────
    void Add<T>(T entity) where T : class;
    void AddRange<T>(IEnumerable<T> entities) where T : class;
    void Remove<T>(T entity) where T : class;
    void RemoveRange<T>(IEnumerable<T> entities) where T : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Desactiva el filtro global de tenant para este contexto.
    /// Usar únicamente en consultas administrativas multi-tenant.
    /// </summary>
    void IgnoreTenantFilter();

    /// <summary>Reactiva el filtro de tenant tras haberlo desactivado.</summary>
    void RestoreTenantFilter();

    /// <summary>
    /// Inicia una transacción de base de datos explícita.
    /// Usar únicamente desde <see cref="GreenTransit.Application.Common.Behaviours.TransactionBehavior{TRequest,TResponse}"/>
    /// para commands que implementen <see cref="ITransactional"/>.
    /// </summary>
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Confirma la transacción activa iniciada con <see cref="BeginTransactionAsync"/>.</summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Revierte la transacción activa iniciada con <see cref="BeginTransactionAsync"/>.</summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
