using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Abstracción del DbContext expuesta a la capa Application.
/// Permite que los query handlers accedan a los DbSets sin depender
/// directamente de AppDbContext (Infrastructure).
/// </summary>
public interface IApplicationDbContext
{
    // ── Catálogos ─────────────────────────────────────────────────────────────
    DbSet<BusinessEntity> BusinessEntities { get; }
    DbSet<LerCode> LerCodes { get; }
    DbSet<Residue> Residues { get; }
    DbSet<TreatmentOperation> TreatmentOperations { get; }

    // ── Contratos ─────────────────────────────────────────────────────────────
    DbSet<Agreement> Agreements { get; }
    DbSet<AgreementDocument> AgreementDocuments { get; }

    // ── Operaciones logísticas ────────────────────────────────────────────────
    DbSet<ServiceOrder> ServiceOrders { get; }
    DbSet<WasteMove> WasteMoves { get; }
    DbSet<WasteMoveResidue> WasteMoveResidues { get; }

    // ── Entradas y tratamiento ────────────────────────────────────────────────
    DbSet<EntryPlant> EntryPlants { get; }
    DbSet<EntryPlantResidue> EntryPlantResidues { get; }
    DbSet<TreatmentPlant> TreatmentPlants { get; }
    DbSet<TreatmentPlantResidue> TreatmentPlantResidues { get; }
    DbSet<EntryCAC> EntryCACs { get; }
    DbSet<EntryCACResidue> EntryCACResidues { get; }

    // ── Producto y declaraciones ──────────────────────────────────────────────
    DbSet<ProductDeclaration> ProductDeclarations { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductSpec> ProductSpecs { get; }
    DbSet<MarketShare> MarketShares { get; }

    // ── Liquidaciones ─────────────────────────────────────────────────────────
    DbSet<Settlement> Settlements { get; }
    DbSet<SettlementLine> SettlementLines { get; }

    // ── Sostenibilidad ────────────────────────────────────────────────────────
    DbSet<EmissionFactorSet> EmissionFactorSets { get; }
    DbSet<EmissionFactor> EmissionFactors { get; }
    DbSet<EcoModulationRuleSet> EcoModulationRuleSets { get; }
    DbSet<EcoModulationRule> EcoModulationRules { get; }
    DbSet<PlantEnergy> PlantEnergies { get; }
    DbSet<Incident> Incidents { get; }
    DbSet<DumZone> DumZones { get; }
    DbSet<DumRestrictionRule> DumRestrictionRules { get; }

    // ── Seguridad ─────────────────────────────────────────────────────────────
    DbSet<AppUser>     AppUsers     { get; }
    DbSet<UserProfile> UserProfiles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Desactiva el filtro global de tenant para este contexto.
    /// Usar únicamente en consultas administrativas multi-tenant.
    /// </summary>
    void IgnoreTenantFilter();

    /// <summary>Reactiva el filtro de tenant tras haberlo desactivado.</summary>
    void RestoreTenantFilter();
}
