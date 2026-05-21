using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreenTransit.Infrastructure.Persistence.Configurations;

// ── Contratos ─────────────────────────────────────────────────────────────────

public class AgreementConfiguration : IEntityTypeConfiguration<Agreement>
{
    public void Configure(EntityTypeBuilder<Agreement> builder)
    {
        builder.ToTable("Agreements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AgreementNumber).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.WasteStream).HasMaxLength(32);
        builder.Property(e => e.SubStream).HasMaxLength(32);
        builder.Property(e => e.AutonomousCommunity).HasMaxLength(64);
        builder.Property(e => e.ProvinceCode).HasMaxLength(16);
        builder.Property(e => e.MunicipalityCode).HasMaxLength(16);
        builder.Property(e => e.TariffModelType).HasMaxLength(64);
        builder.Property(e => e.Currency).HasMaxLength(8);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.EffectiveFrom).HasColumnType("datetime2(0)");
        builder.Property(e => e.EffectiveTo).HasColumnType("datetime2(0)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.Version).HasDefaultValue(1);

        // ── 3 FK a BusinessEntity: obligatorio especificar inverse navigation ──
        builder.HasOne(e => e.Scrap)
            .WithMany(b => b.AgreementsAsScrap)
            .HasForeignKey(e => e.IdScrap)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PublicEntity)
            .WithMany(b => b.AgreementsAsPublicEntity)
            .HasForeignKey(e => e.IdPublicEntity)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Coordinator)
            .WithMany(b => b.AgreementsAsCoordinator)
            .HasForeignKey(e => e.IdCoordinator)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.AgreementNumber).IsUnique().HasDatabaseName("UX_Agreements_Number");
        builder.HasIndex(e => e.IdScrap).HasDatabaseName("IX_Agreements_IdScrap");
        builder.HasIndex(e => e.IdCoordinator).HasDatabaseName("IX_Agreements_IdCoordinator");
        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_Agreements_OwnerId");
    }
}

public class AgreementDocumentConfiguration : IEntityTypeConfiguration<AgreementDocument>
{
    public void Configure(EntityTypeBuilder<AgreementDocument> builder)
    {
        builder.ToTable("AgreementDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DocumentType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DocumentId).HasMaxLength(128);
        builder.Property(e => e.DocumentHash).HasMaxLength(128);
        builder.Property(e => e.SignatureProvider).HasMaxLength(64);
        builder.Property(e => e.SignedAt).HasColumnType("datetime2(0)");

        builder.HasOne(e => e.Agreement)
            .WithMany(a => a.AgreementDocuments)
            .HasForeignKey(e => e.AgreementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.AgreementId).HasDatabaseName("IX_AgreementDocuments_AgreementId");
    }
}

// ── Liquidaciones ─────────────────────────────────────────────────────────────

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SettlementNumber).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(8).HasDefaultValue("EUR");
        builder.Property(e => e.BaseAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.AdjustmentsAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.Validator).HasMaxLength(64);
        builder.Property(e => e.ValidationStatus).HasMaxLength(32);
        builder.Property(e => e.ValidationRef).HasMaxLength(128);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.Version).HasDefaultValue(1);

        builder.HasOne(e => e.Agreement)
            .WithMany(a => a.Settlements)
            .HasForeignKey(e => e.AgreementId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── 2 FK a BusinessEntity: obligatorio especificar inverse navigation ──
        builder.HasOne(e => e.Scrap)
            .WithMany(b => b.SettlementsAsScrap)
            .HasForeignKey(e => e.IdScrap)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PublicEntity)
            .WithMany(b => b.SettlementsAsPublicEntity)
            .HasForeignKey(e => e.IdPublicEntity)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SettlementNumber).IsUnique().HasDatabaseName("UX_Settlements_Number");
        builder.HasIndex(e => new { e.AgreementId, e.Year, e.Month }).HasDatabaseName("IX_Settlements_Agreement_Period");
    }
}

public class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    public void Configure(EntityTypeBuilder<SettlementLine> builder)
    {
        builder.ToTable("SettlementLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WeightKg).HasColumnType("decimal(18,3)").HasDefaultValue(0m);
        builder.Property(e => e.PricePerKg).HasColumnType("decimal(18,6)").HasDefaultValue(0m);
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
        builder.Property(e => e.EvidenceType).HasMaxLength(64);

        builder.HasOne(e => e.Settlement)
            .WithMany(s => s.SettlementLines)
            .HasForeignKey(e => e.SettlementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.LerCode)
            .WithMany(l => l.SettlementLines)
            .HasForeignKey(e => e.IdLERCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SettlementId).HasDatabaseName("IX_SettlementLines_SettlementId");
    }
}

// ── Órdenes de servicio ───────────────────────────────────────────────────────

public class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
{
    public void Configure(EntityTypeBuilder<ServiceOrder> builder)
    {
        builder.ToTable("ServiceOrders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ServiceOrderNumber).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Priority).HasMaxLength(16).IsRequired().HasDefaultValue("Normal");
        builder.Property(e => e.WasteStream).HasMaxLength(32);
        builder.Property(e => e.SubStream).HasMaxLength(32);
        builder.Property(e => e.WasteMoveReference).HasMaxLength(128);
        builder.Property(e => e.TicketScalePlanned).HasMaxLength(128);
        builder.Property(e => e.VehicleRegistration).HasMaxLength(32);
        builder.Property(e => e.VehicleType).HasMaxLength(32);
        builder.Property(e => e.FuelType).HasMaxLength(32);
        builder.Property(e => e.EuroClass).HasMaxLength(16);
        builder.Property(e => e.IssuedByName).HasMaxLength(256);
        builder.Property(e => e.IssuedByNationalId).HasMaxLength(32);
        builder.Property(e => e.IssuedByCenterCode).HasMaxLength(64);
        builder.Property(e => e.EstimatedWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.TransportDistanceKm).HasColumnType("decimal(18,3)");
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.Version).HasDefaultValue(1);

        // ── 4 FK a BusinessEntity: obligatorio especificar inverse navigation ──
        builder.HasOne(e => e.IssuedBy)
            .WithMany(b => b.ServiceOrdersAsIssuedBy)
            .HasForeignKey(e => e.IdIssuedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PickupPoint)
            .WithMany(b => b.ServiceOrdersAsPickupPoint)
            .HasForeignKey(e => e.IdPickupPoint)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Carrier)
            .WithMany(b => b.ServiceOrdersAsCarrier)
            .HasForeignKey(e => e.IdCarrier)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PlannedPlant)
            .WithMany(b => b.ServiceOrdersAsPlannedPlant)
            .HasForeignKey(e => e.IdPlannedPlant)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.LerCode)
            .WithMany(l => l.ServiceOrders)
            .HasForeignKey(e => e.IdLERCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ServiceOrderNumber).IsUnique().HasDatabaseName("UX_ServiceOrders_Number");
        builder.HasIndex(e => e.WasteMoveReference).HasDatabaseName("IX_ServiceOrders_WasteMoveRef");
        builder.HasIndex(e => e.IdCarrier).HasDatabaseName("IX_ServiceOrders_IdCarrier");
        builder.HasIndex(e => e.IdPlannedPlant).HasDatabaseName("IX_ServiceOrders_IdPlannedPlant");
        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_ServiceOrders_OwnerId");
        builder.HasIndex(e => e.IdIssuedBy).HasDatabaseName("IX_ServiceOrders_IdIssuedBy");
        builder.HasIndex(e => new { e.OwnerId, e.IdIssuedBy }).HasDatabaseName("IX_ServiceOrders_OwnerId_IssuedBy");
        builder.HasIndex(e => e.PlannedPickupStart).HasDatabaseName("IX_ServiceOrders_PlannedPickupStart");
        builder.HasIndex(e => e.IdPickupPoint).HasDatabaseName("IX_ServiceOrders_IdPickupPoint");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_ServiceOrders_Status");
    }
}

public class ServiceOrderResidueConfiguration : IEntityTypeConfiguration<ServiceOrderResidue>
{
    public void Configure(EntityTypeBuilder<ServiceOrderResidue> builder)
    {
        builder.ToTable("ServiceOrderResidues");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EstimatedWeight).HasColumnType("decimal(18,3)");

        builder.HasOne(e => e.ServiceOrder)
            .WithMany(s => s.Residues)
            .HasForeignKey(e => e.IdServiceOrder)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.LerCode)
            .WithMany()
            .HasForeignKey(e => e.IdLERCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdServiceOrder).HasDatabaseName("IX_ServiceOrderResidues_IdServiceOrder");
    }
}

// ── Traslados// ── Traslados

public class WasteMoveConfiguration : IEntityTypeConfiguration<WasteMove>
{
    public void Configure(EntityTypeBuilder<WasteMove> builder)
    {
        builder.ToTable("WasteMoves");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WasteMoveReference).HasMaxLength(128);
        builder.Property(e => e.Lot).HasMaxLength(64);
        builder.Property(e => e.SignatureStatus).HasMaxLength(32);
        builder.Property(e => e.ServiceStatus).HasMaxLength(32);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.DocumentId).HasMaxLength(128);
        builder.Property(e => e.DocumentHash).HasMaxLength(128);
        builder.Property(e => e.Version).HasDefaultValue(1);
        builder.Property(e => e.DateCreateSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.DateModifiedSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        // ── FK a BusinessEntity con inverse navigations ────────────────────────
        builder.HasOne(e => e.Scrap)
            .WithMany(b => b.WasteMovesAsScrap)
            .HasForeignKey(e => e.IdScrap)
            .OnDelete(DeleteBehavior.Restrict);

        // IdScrap2 no tiene inverse collection en BusinessEntity
        builder.HasOne(e => e.Scrap2)
            .WithMany()
            .HasForeignKey(e => e.IdScrap2)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Source)
            .WithMany(b => b.WasteMovesAsSource)
            .HasForeignKey(e => e.IdSource)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Destination)
            .WithMany(b => b.WasteMovesAsDestination)
            .HasForeignKey(e => e.IdDestination)
            .OnDelete(DeleteBehavior.Restrict);

        // IdOperatorTransfer no tiene inverse collection en BusinessEntity
        builder.HasOne(e => e.OperatorTransfer)
            .WithMany()
            .HasForeignKey(e => e.IdOperatorTransfer)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceOrder)
            .WithMany()
            .HasForeignKey(e => e.ServiceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.WasteMoveReference).HasDatabaseName("IX_WasteMoves_Reference");
        builder.HasIndex(e => e.IdScrap).HasDatabaseName("IX_WasteMoves_IdScrap");
        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_WasteMoves_OwnerId");
        builder.HasIndex(e => e.ServiceStatus).HasDatabaseName("IX_WasteMoves_ServiceStatus");
        builder.HasIndex(e => e.ActualPickupStart).HasDatabaseName("IX_WasteMoves_ActualPickupStart");
        builder.HasIndex(e => e.PlannedPickupStart).HasDatabaseName("IX_WasteMoves_PlannedPickupStart");
        builder.HasIndex(e => new { e.OwnerId, e.ServiceStatus }).HasDatabaseName("IX_WasteMoves_OwnerId_Status");
        builder.HasIndex(e => new { e.OwnerId, e.PlannedPickupStart }).HasDatabaseName("IX_WasteMoves_OwnerId_PlannedPickupStart");
        builder.HasIndex(e => e.IdSource).HasDatabaseName("IX_WasteMoves_IdSource");
        builder.HasIndex(e => e.IdDestination).HasDatabaseName("IX_WasteMoves_IdDestination");
        builder.HasIndex(e => e.ServiceOrderId).HasFilter("[ServiceOrderId] IS NOT NULL").HasDatabaseName("IX_WasteMoves_ServiceOrderId");
    }
}

public class WasteMoveResidueConfiguration : IEntityTypeConfiguration<WasteMoveResidue>
{
    public void Configure(EntityTypeBuilder<WasteMoveResidue> builder)
    {
        builder.ToTable("WasteMoveResidues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Weight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.UnitPriceKg).HasColumnType("decimal(18,6)");
        builder.Property(e => e.TransportInfo_TransportDuration).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TransportInfo_TransportDistance).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TransportInfo_TransportCarbonEmissions).HasColumnType("decimal(18,2)");
        builder.Property(e => e.MeasureUnit).HasMaxLength(64);
        builder.Property(e => e.NTNumber).HasMaxLength(64);
        builder.Property(e => e.DINumber).HasMaxLength(64);
        builder.Property(e => e.DIPhase).HasMaxLength(12);
        builder.Property(e => e.VehicleType).HasMaxLength(32);
        builder.Property(e => e.FuelType).HasMaxLength(32);
        builder.Property(e => e.EuroClass).HasMaxLength(16);
        builder.Property(e => e.EmissionFactorVersion).HasMaxLength(32);
        builder.Property(e => e.TransportInfo_VehicleRegistration).HasMaxLength(256);
        builder.Property(e => e.TransportInfo_VehicleRegistrationTrailer).HasMaxLength(256);

        builder.HasOne(e => e.WasteMove)
            .WithMany(w => w.WasteMoveResidues)
            .HasForeignKey(e => e.IdWasteMove)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Residue)
            .WithMany(r => r.WasteMoveResidues)
            .HasForeignKey(e => e.IdResidue)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TreatmentOperationDestiny)
            .WithMany(t => t.WasteMoveResidues)
            .HasForeignKey(e => e.IdTreatmentOperationDestiny)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Carrier)
            .WithMany(b => b.WasteMoveResiduesAsCarrier)
            .HasForeignKey(e => e.IdCarrier)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EmissionFactorSet)
            .WithMany()
            .HasForeignKey(e => e.EmissionFactorSetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdWasteMove).HasDatabaseName("IX_WasteMoveResidues_IdWasteMove");
        builder.HasIndex(e => e.IdCarrier).HasDatabaseName("IX_WasteMoveResidues_IdCarrier");
        builder.HasIndex(e => e.IdLerCode).HasDatabaseName("IX_WasteMoveResidues_IdLerCode");
    }
}

// ── Entradas en planta

public class EntryPlantConfiguration : IEntityTypeConfiguration<EntryPlant>
{
    public void Configure(EntityTypeBuilder<EntryPlant> builder)
    {
        builder.ToTable("EntryPlants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WasteMoveReference).HasMaxLength(128);
        builder.Property(e => e.TicketScale).HasMaxLength(128);
        builder.Property(e => e.TypeContainer).HasMaxLength(256);
        builder.Property(e => e.PriceContainer).HasColumnType("decimal(18,2)");
        builder.Property(e => e.GrossWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.TareWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.NetWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.WeighbridgeId).HasMaxLength(64);
        builder.Property(e => e.DateCreateSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.DateModifiedSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.WasteMove)
            .WithMany()
            .HasForeignKey(e => e.IdWasteMove)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceOrder)
            .WithMany()
            .HasForeignKey(e => e.ServiceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdWasteMove).HasDatabaseName("IX_EntryPlants_IdWasteMove");
        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_EntryPlants_OwnerId");
    }
}

public class EntryPlantResidueConfiguration : IEntityTypeConfiguration<EntryPlantResidue>
{
    public void Configure(EntityTypeBuilder<EntryPlantResidue> builder)
    {
        builder.ToTable("EntryPlantResidues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Weight).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PriceWeight).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PriceUnit).HasColumnType("decimal(18,2)");
        builder.Property(e => e.MeasureUnit).HasMaxLength(64);

        builder.HasOne(e => e.EntryPlant)
            .WithMany(ep => ep.EntryPlantResidues)
            .HasForeignKey(e => e.IdEntryPlant)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Residue)
            .WithMany(r => r.EntryPlantResidues)
            .HasForeignKey(e => e.IdResidue)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdEntryPlant).HasDatabaseName("IX_EntryPlantResidues_IdEntryPlant");
    }
}

// ── Tratamiento en planta ─────────────────────────────────────────────────────

public class TreatmentPlantConfiguration : IEntityTypeConfiguration<TreatmentPlant>
{
    public void Configure(EntityTypeBuilder<TreatmentPlant> builder)
    {
        builder.ToTable("TreatmentPlants");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WasteMoveReference).HasMaxLength(128);
        builder.Property(e => e.TicketScale).HasMaxLength(128);
        builder.Property(e => e.TypeContainer).HasMaxLength(256);
        builder.Property(e => e.PriceContainer).HasColumnType("decimal(18,2)");
        builder.Property(e => e.ImproperWeight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.DateCreateSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.DateModifiedSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.WasteMove)
            .WithMany()
            .HasForeignKey(e => e.IdWasteMove)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceOrder)
            .WithMany()
            .HasForeignKey(e => e.ServiceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TreatmentOperation)
            .WithMany(t => t.TreatmentPlants)
            .HasForeignKey(e => e.IdTreatmentOperation)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Incident)
            .WithMany()
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdWasteMove).HasDatabaseName("IX_TreatmentPlants_IdWasteMove");
        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_TreatmentPlants_OwnerId");
        builder.HasIndex(e => e.PlantTreatmentDate).HasDatabaseName("IX_TreatmentPlants_PlantTreatmentDate");
        builder.HasIndex(e => new { e.OwnerId, e.PlantTreatmentDate }).HasDatabaseName("IX_TreatmentPlants_OwnerId_TreatmentDate");
    }
}

public class TreatmentPlantResidueConfiguration : IEntityTypeConfiguration<TreatmentPlantResidue>
{
    public void Configure(EntityTypeBuilder<TreatmentPlantResidue> builder)
    {
        builder.ToTable("TreatmentPlantResidues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Category).HasMaxLength(64);
        builder.Property(e => e.WeightTotal).HasColumnType("decimal(18,2)");
        builder.Property(e => e.WeightReused).HasColumnType("decimal(18,2)");
        builder.Property(e => e.WeightValued).HasColumnType("decimal(18,2)");
        builder.Property(e => e.WeightRemove).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PriceWeight).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PriceUnit).HasColumnType("decimal(18,2)");

        builder.HasOne(e => e.TreatmentPlant)
            .WithMany(tp => tp.TreatmentPlantResidues)
            .HasForeignKey(e => e.IdTreatmentPlant)
            .OnDelete(DeleteBehavior.Cascade);

        // ── 4 FK a Residue: obligatorio especificar inverse navigation ─────────
        builder.HasOne(e => e.Residue)
            .WithMany(r => r.TreatmentPlantResiduesAsInput)
            .HasForeignKey(e => e.IdResidue)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ResidueReused)
            .WithMany(r => r.TreatmentPlantResiduesAsReused)
            .HasForeignKey(e => e.IdResidueReused)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ResidueValued)
            .WithMany(r => r.TreatmentPlantResiduesAsValued)
            .HasForeignKey(e => e.IdResidueValued)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ResidueRemove)
            .WithMany(r => r.TreatmentPlantResiduesAsRemove)
            .HasForeignKey(e => e.IdResidueRemove)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdTreatmentPlant).HasDatabaseName("IX_TreatmentPlantResidues_IdTreatmentPlant");
    }
}

// ── Centros de acopio

public class EntryCACConfiguration : IEntityTypeConfiguration<EntryCAC>
{
    public void Configure(EntityTypeBuilder<EntryCAC> builder)
    {
        builder.ToTable("EntryCACs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WasteMoveReference).HasMaxLength(128);
        builder.Property(e => e.TypeContainer).HasMaxLength(256);
        builder.Property(e => e.PriceContainer).HasColumnType("decimal(18,2)");
        builder.Property(e => e.CollectionMethod).HasMaxLength(32);
        builder.Property(e => e.DateCreateSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.DateModifiedSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.WasteMove)
            .WithMany()
            .HasForeignKey(e => e.IdWasteMove)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_EntryCACs_OwnerId");
        builder.HasIndex(e => e.CACEntryDate).HasDatabaseName("IX_EntryCACs_CACEntryDate");
    }
}

public class EntryCACResidueConfiguration : IEntityTypeConfiguration<EntryCACResidue>
{
    public void Configure(EntityTypeBuilder<EntryCACResidue> builder)
    {
        builder.ToTable("EntryCACResidues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Weight).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PriceWeight).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PriceUnit).HasColumnType("decimal(18,2)");
        builder.Property(e => e.MeasureUnit).HasMaxLength(64);

        builder.HasOne(e => e.EntryCAC)
            .WithMany(c => c.EntryCACResidues)
            .HasForeignKey(e => e.IdEntryCAC)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Residue)
            .WithMany(r => r.EntryCACResidues)
            .HasForeignKey(e => e.IdResidue)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IdEntryCAC).HasDatabaseName("IX_EntryCACResidues_IdEntryCAC");
    }
}

// ── Producto y declaraciones

public class ProductDeclarationConfiguration : IEntityTypeConfiguration<ProductDeclaration>
{
    public void Configure(EntityTypeBuilder<ProductDeclaration> builder)
    {
        builder.ToTable("ProductDeclaration");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Currency).HasMaxLength(8);
        builder.Property(e => e.State).HasMaxLength(32);
        builder.Property(e => e.Reference).HasMaxLength(128);
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Type).HasMaxLength(64);
        builder.Property(e => e.DateCreate).HasColumnType("datetime2(0)");
        builder.Property(e => e.DateEmit).HasColumnType("datetime2(0)");
        builder.Property(e => e.DateCreateSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.DateModifiedSys).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.Producer)
            .WithMany(b => b.ProductDeclarations)
            .HasForeignKey(e => e.IdProducer)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reference).HasMaxLength(128);
        builder.Property(e => e.Source).HasMaxLength(128);
        builder.Property(e => e.ProductUse).HasMaxLength(128);
        builder.Property(e => e.ProductCategory).HasMaxLength(256);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Price).HasColumnType("decimal(18,2)");
        builder.Property(e => e.MeasureUnit).HasMaxLength(64);

        builder.HasOne(e => e.ProductDeclaration)
            .WithMany(pd => pd.Products)
            .HasForeignKey(e => e.IdProductDeclaration)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Residue)
            .WithMany(r => r.Products)
            .HasForeignKey(e => e.IdResidue)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductSpecConfiguration : IEntityTypeConfiguration<ProductSpec>
{
    public void Configure(EntityTypeBuilder<ProductSpec> builder)
    {
        builder.ToTable("ProductSpecs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProductRef).HasMaxLength(128).IsRequired();
        builder.Property(e => e.CategoryRef).HasMaxLength(64);
        builder.Property(e => e.ProducerRef).HasMaxLength(64);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.Version).HasDefaultValue(1);

        builder.HasOne(e => e.Residue)
            .WithMany(r => r.ProductSpecs)
            .HasForeignKey(e => e.IdResidue)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Producer)
            .WithMany(b => b.ProductSpecs)
            .HasForeignKey(e => e.IdProducer)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ProductRef).IsUnique().HasDatabaseName("UX_ProductSpecs_ProductRef");
    }
}

public class MarketShareConfiguration : IEntityTypeConfiguration<MarketShare>
{
    public void Configure(EntityTypeBuilder<MarketShare> builder)
    {
        builder.ToTable("MarketShares");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Category).HasMaxLength(64).IsRequired();
        builder.Property(e => e.AutonomousCommunity).HasMaxLength(64);
        builder.Property(e => e.Weight).HasColumnType("decimal(18,3)");
        builder.Property(e => e.FlowType).HasMaxLength(32);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Version).HasDefaultValue(1);

        builder.HasOne(e => e.Scrap)
            .WithMany(b => b.MarketShares)
            .HasForeignKey(e => e.IdScrap)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.OwnerId, e.Year, e.Period }).HasDatabaseName("IX_MarketShares_OwnerId_Period");
    }
}

// ── Sostenibilidad ────────────────────────────────────────────────────────────

public class EmissionFactorSetConfiguration : IEntityTypeConfiguration<EmissionFactorSet>
{
    public void Configure(EntityTypeBuilder<EmissionFactorSet> builder)
    {
        builder.ToTable("EmissionFactorSets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FactorSetName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Version).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Publisher).HasMaxLength(256);
        builder.Property(e => e.Reference).HasMaxLength(128);
        builder.Property(e => e.Methodology).HasMaxLength(256);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.ValidFrom).HasColumnType("datetime2(0)");
        builder.Property(e => e.ValidTo).HasColumnType("datetime2(0)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public class EmissionFactorConfiguration : IEntityTypeConfiguration<EmissionFactor>
{
    public void Configure(EntityTypeBuilder<EmissionFactor> builder)
    {
        builder.ToTable("EmissionFactors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.VehicleType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.FuelType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.EuroClass).HasMaxLength(16);
        builder.Property(e => e.Unit).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Value).HasColumnType("decimal(18,6)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.FactorSet)
            .WithMany(fs => fs.EmissionFactors)
            .HasForeignKey(e => e.FactorSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EcoModulationRuleSetConfiguration : IEntityTypeConfiguration<EcoModulationRuleSet>
{
    public void Configure(EntityTypeBuilder<EcoModulationRuleSet> builder)
    {
        builder.ToTable("EcoModulationRuleSets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RuleSetName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Version).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.PublisherName).HasMaxLength(256);
        builder.Property(e => e.PublisherNationalId).HasMaxLength(32);
        builder.Property(e => e.PublisherCenterCode).HasMaxLength(64);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.ValidFrom).HasColumnType("datetime2(0)");
        builder.Property(e => e.ValidTo).HasColumnType("datetime2(0)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public class EcoModulationRuleConfiguration : IEntityTypeConfiguration<EcoModulationRule>
{
    public void Configure(EntityTypeBuilder<EcoModulationRule> builder)
    {
        builder.ToTable("EcoModulationRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RuleCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.FeeImpactType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.FeeImpactValue).HasColumnType("decimal(18,6)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.RuleSet)
            .WithMany(rs => rs.EcoModulationRules)
            .HasForeignKey(e => e.RuleSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PlantEnergyConfiguration : IEntityTypeConfiguration<PlantEnergy>
{
    public void Configure(EntityTypeBuilder<PlantEnergy> builder)
    {
        builder.ToTable("PlantEnergies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlantName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.PlantCenterCode).HasMaxLength(64);
        builder.Property(e => e.KwhTotal).HasColumnType("decimal(18,3)");
        builder.Property(e => e.Source).HasMaxLength(64);
        builder.Property(e => e.GridMixRef).HasMaxLength(128);
        builder.Property(e => e.AllocationMethod).HasMaxLength(64);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.Version).HasDefaultValue(1);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Severity).HasMaxLength(32).IsRequired();
        builder.Property(e => e.WasteMoveReference).HasMaxLength(128);
        builder.Property(e => e.TicketScale).HasMaxLength(128);
        builder.Property(e => e.ReportedByName).HasMaxLength(256);
        builder.Property(e => e.ReportedByNationalId).HasMaxLength(32);
        builder.Property(e => e.ReportedByCenterCode).HasMaxLength(64);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.OpenedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.ClosedAt).HasColumnType("datetime2(0)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.Version).HasDefaultValue(1);

        builder.HasOne(e => e.ServiceOrder)
            .WithMany()
            .HasForeignKey(e => e.ServiceOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_Incidents_OwnerId");
    }
}

// ── Zonas DUM

public class DumZoneConfiguration : IEntityTypeConfiguration<DumZone>
{
    public void Configure(EntityTypeBuilder<DumZone> builder)
    {
        builder.ToTable("DUMZones");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ZoneCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(512);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.Version).HasDefaultValue(1);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.ZoneCode).IsUnique().HasDatabaseName("UX_DUMZones_ZoneCode");
    }
}

public class DumRestrictionRuleConfiguration : IEntityTypeConfiguration<DumRestrictionRule>
{
    public void Configure(EntityTypeBuilder<DumRestrictionRule> builder)
    {
        builder.ToTable("DUMRestrictionRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RuleCode).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ActionType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ActionReason).HasMaxLength(256);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.Version).HasDefaultValue(1);
        builder.Property(e => e.ValidFrom).HasColumnType("datetime2(0)");
        builder.Property(e => e.ValidTo).HasColumnType("datetime2(0)");
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.Zone)
            .WithMany(z => z.DumRestrictionRules)
            .HasForeignKey(e => e.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.RuleCode).IsUnique().HasDatabaseName("UX_DUMRestrictionRules_RuleCode");
    }
}
