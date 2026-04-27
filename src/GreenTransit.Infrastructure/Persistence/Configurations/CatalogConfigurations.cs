using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreenTransit.Infrastructure.Persistence.Configurations;

public class BusinessEntityConfiguration : IEntityTypeConfiguration<BusinessEntity>
{
    public void Configure(EntityTypeBuilder<BusinessEntity> builder)
    {
        builder.ToTable("Entities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.NationalId).HasMaxLength(64);
        builder.Property(e => e.CenterCode).HasMaxLength(256);
        builder.Property(e => e.EntityRole).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TypeThirdParty).HasMaxLength(256);
        builder.Property(e => e.InscriptionType).HasMaxLength(64);
        builder.Property(e => e.InscriptionNumber).HasMaxLength(256);
        builder.Property(e => e.CountryCode).HasMaxLength(64);
        builder.Property(e => e.StateCode).HasMaxLength(64);
        builder.Property(e => e.ZipCode).HasMaxLength(64);
        builder.Property(e => e.ProvinceCode).HasMaxLength(256);
        builder.Property(e => e.MunicipalityCode).HasMaxLength(256);
        builder.Property(e => e.Address).HasMaxLength(512);
        builder.Property(e => e.Latitude).HasMaxLength(64);
        builder.Property(e => e.Longitude).HasMaxLength(64);
        builder.Property(e => e.PhoneNumber).HasMaxLength(64);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.ContactPerson).HasMaxLength(256);
        builder.Property(e => e.EconomicActivity).HasMaxLength(256);
        builder.Property(e => e.EntityType).HasMaxLength(256);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.NationalId).HasDatabaseName("IX_Entities_NationalId");
        builder.HasIndex(e => e.EntityRole).HasDatabaseName("IX_Entities_EntityRole");
        builder.HasIndex(e => e.CenterCode).HasDatabaseName("IX_Entities_CenterCode");
    }
}

public class LerCodeConfiguration : IEntityTypeConfiguration<LerCode>
{
    public void Configure(EntityTypeBuilder<LerCode> builder)
    {
        builder.ToTable("LERCodes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.Code).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CodeExtended).HasMaxLength(64);
        builder.Property(e => e.Description).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Chapter).HasMaxLength(8);
        builder.Property(e => e.ChapterDescription).HasMaxLength(256);
        builder.Property(e => e.SubChapter).HasMaxLength(8);
        builder.Property(e => e.SubChapterDescription).HasMaxLength(256);
        builder.Property(e => e.DefaultProductCategory).HasMaxLength(256);
        builder.Property(e => e.Notes).HasMaxLength(512);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_LERCodes_Code");
        builder.HasIndex(e => e.Chapter).HasDatabaseName("IX_LERCodes_Chapter");
        builder.HasIndex(e => e.IsDangerous).HasDatabaseName("IX_LERCodes_IsDangerous");
    }
}

public class ResidueConfiguration : IEntityTypeConfiguration<Residue>
{
    public void Configure(EntityTypeBuilder<Residue> builder)
    {
        builder.ToTable("Residues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.ResidueType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(512);
        builder.Property(e => e.Reference).HasMaxLength(128);
        builder.Property(e => e.DangerousCode).HasMaxLength(256);
        builder.Property(e => e.ProductUse).HasMaxLength(64);
        builder.Property(e => e.ProductCategory).HasMaxLength(256);
        builder.Property(e => e.WeightPerUnitKg).HasColumnType("decimal(18,3)");
        builder.Property(e => e.DefaultMeasureUnit).HasMaxLength(64);
        builder.Property(e => e.DisassemblyEase).HasMaxLength(32);
        builder.Property(e => e.RecycledContentPercent).HasColumnType("decimal(5,2)");
        builder.Property(e => e.ProducerRef).HasMaxLength(64);
        builder.Property(e => e.SourceSystem).HasMaxLength(64);
        builder.Property(e => e.Hash).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(e => e.LerCode).WithMany(l => l.Residues).HasForeignKey(e => e.IdLERCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Producer).WithMany(b => b.Residues).HasForeignKey(e => e.IdProducer).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.ResidueType).HasDatabaseName("IX_Residues_ResidueType");
        builder.HasIndex(e => e.IdLERCode).HasDatabaseName("IX_Residues_IdLERCode");
        builder.HasIndex(e => e.IdProducer).HasDatabaseName("IX_Residues_IdProducer");
        builder.HasIndex(e => e.ProductCategory).HasDatabaseName("IX_Residues_ProductCat");
        builder.HasIndex(e => e.IsDangerous).HasDatabaseName("IX_Residues_IsDangerous");
    }
}

public class TreatmentOperationConfiguration : IEntityTypeConfiguration<TreatmentOperation>
{
    public void Configure(EntityTypeBuilder<TreatmentOperation> builder)
    {
        builder.ToTable("TreatmentOperations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.Code).HasMaxLength(8).IsRequired();
        builder.Property(e => e.OperationType).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(512).IsRequired();
        builder.Property(e => e.ShortDescription).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_TreatmentOperations_Code");
        builder.HasIndex(e => e.OperationType).HasDatabaseName("IX_TreatmentOperations_OperationType");
    }
}
