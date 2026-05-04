using System.Reflection;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService _currentUserService;
    private bool _ignoreTenantFilter;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Desactiva el filtro global de tenant para el scope de este contexto.
    /// Usar únicamente en consultas administrativas que requieran visibilidad multi-tenant.
    /// </summary>
    public void IgnoreTenantFilter() => _ignoreTenantFilter = true;

    /// <summary>Reactiva el filtro de tenant tras haberlo desactivado.</summary>
    public void RestoreTenantFilter() => _ignoreTenantFilter = false;

    // ── Catálogos ─────────────────────────────────────────────────────────────
    public DbSet<BusinessEntity> BusinessEntities => Set<BusinessEntity>();
    public DbSet<LerCode> LerCodes => Set<LerCode>();
    public DbSet<Residue> Residues => Set<Residue>();
    public DbSet<TreatmentOperation> TreatmentOperations => Set<TreatmentOperation>();

    // ── Contratos ─────────────────────────────────────────────────────────────
    public DbSet<Agreement> Agreements => Set<Agreement>();
    public DbSet<AgreementDocument> AgreementDocuments => Set<AgreementDocument>();

    // ── Operaciones de recogida y traslado ────────────────────────────────────
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceOrderResidue> ServiceOrderResidues => Set<ServiceOrderResidue>();
    public DbSet<WasteMove> WasteMoves => Set<WasteMove>();
    public DbSet<WasteMoveResidue> WasteMoveResidues => Set<WasteMoveResidue>();

    // ── Operaciones en planta ─────────────────────────────────────────────────
    public DbSet<EntryPlant> EntryPlants => Set<EntryPlant>();
    public DbSet<EntryPlantResidue> EntryPlantResidues => Set<EntryPlantResidue>();
    public DbSet<TreatmentPlant> TreatmentPlants => Set<TreatmentPlant>();
    public DbSet<TreatmentPlantResidue> TreatmentPlantResidues => Set<TreatmentPlantResidue>();

    // ── Centros de acopio ─────────────────────────────────────────────────────
    public DbSet<EntryCAC> EntryCACs => Set<EntryCAC>();
    public DbSet<EntryCACResidue> EntryCACResidues => Set<EntryCACResidue>();

    // ── Declaraciones y mercado ───────────────────────────────────────────────
    public DbSet<ProductDeclaration> ProductDeclarations => Set<ProductDeclaration>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductSpec> ProductSpecs => Set<ProductSpec>();
    public DbSet<MarketShare> MarketShares => Set<MarketShare>();

    // ── Liquidaciones ─────────────────────────────────────────────────────────
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementLine> SettlementLines => Set<SettlementLine>();

    // ── Sostenibilidad y logística ────────────────────────────────────────────
    public DbSet<EmissionFactorSet> EmissionFactorSets => Set<EmissionFactorSet>();
    public DbSet<EmissionFactor> EmissionFactors => Set<EmissionFactor>();
    public DbSet<EcoModulationRuleSet> EcoModulationRuleSets => Set<EcoModulationRuleSet>();
    public DbSet<EcoModulationRule> EcoModulationRules => Set<EcoModulationRule>();
    public DbSet<PlantEnergy> PlantEnergies => Set<PlantEnergy>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<DumZone> DumZones => Set<DumZone>();
    public DbSet<DumRestrictionRule> DumRestrictionRules => Set<DumRestrictionRule>();
    public DbSet<RegulatoryTarget> RegulatoryTargets => Set<RegulatoryTarget>();

    // ── Geografía ─────────────────────────────────────────────────────────────
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<TerritoryState> TerritoryStates => Set<TerritoryState>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<MunicipalityPopulation> MunicipalityPopulations => Set<MunicipalityPopulation>();
    public DbSet<MunicipalityZipCode> MunicipalityZipCodes => Set<MunicipalityZipCode>();

    // ── Diccionarios ──────────────────────────────────────────────────────────
    public DbSet<DicProductDeclarationCategory> DicProductDeclarationCategories => Set<DicProductDeclarationCategory>();
    public DbSet<DicProductDeclarationPeriod> DicProductDeclarationPeriods => Set<DicProductDeclarationPeriod>();
    public DbSet<DicProductDeclarationProduct> DicProductDeclarationProducts => Set<DicProductDeclarationProduct>();
    public DbSet<DicProductDeclarationSource> DicProductDeclarationSources => Set<DicProductDeclarationSource>();
    public DbSet<DicProductDeclarationType> DicProductDeclarationTypes => Set<DicProductDeclarationType>();
    public DbSet<DicProductDeclarationUse> DicProductDeclarationUses => Set<DicProductDeclarationUse>();
    public DbSet<DocState> DocStates => Set<DocState>();

    // ── Seguridad ─────────────────────────────────────────────────────────────
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserSharePointCredential> UserSharePointCredentials => Set<UserSharePointCredential>();

    // ─────────────────────────────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Carga todas las IEntityTypeConfiguration<T> del assembly de Infrastructure
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Aplica filtro global de OwnerId a todas las entidades que implementan ITenantEntity
        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Aplica HasQueryFilter en todas las entidades que implementan ITenantEntity.
    /// Si OwnerId del servicio es null (admin/sistema), devuelve todos los registros.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var tenantFilterMethod = typeof(AppDbContext)
            .GetMethod(nameof(ConfigureTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            tenantFilterMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
    {
        // _ignoreTenantFilter: bypass para consultas de administración.
        // !IsAuthenticated: sin sesión activa no se aplica filtro (seeds, migraciones).
        // OwnerId: filtro estricto de tenant para usuarios autenticados.
        modelBuilder.Entity<T>().HasQueryFilter(
            e => _ignoreTenantFilter
                 || !_currentUserService.IsAuthenticated
                 || e.OwnerId == _currentUserService.OwnerId);
    }

    // ── Auditoría automática de CreatedAt / UpdatedAt ─────────────────────────

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditTimestamps();
        return base.SaveChanges();
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    // Preserva CreatedAt original; solo actualiza UpdatedAt
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
