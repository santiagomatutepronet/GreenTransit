using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreenTransit.Infrastructure.Persistence.Configurations;

public class ExplorerLayoutConfigConfiguration : IEntityTypeConfiguration<ExplorerLayoutConfig>
{
    public void Configure(EntityTypeBuilder<ExplorerLayoutConfig> builder)
    {
        builder.ToTable("ExplorerLayoutConfigs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.OwnerId)
               .IsRequired();

        builder.Property(e => e.UserId)
               .IsRequired();

        builder.Property(e => e.AssetId)
               .HasMaxLength(512)
               .IsRequired();

        builder.Property(e => e.ProviderParticipantId)
               .HasMaxLength(512)
               .IsRequired();

        builder.Property(e => e.DatasetName)
               .HasMaxLength(256);

        builder.Property(e => e.LayoutConfigJson)
               .HasColumnType("nvarchar(max)")
               .HasDefaultValue("[]")
               .IsRequired();

        builder.Property(e => e.SchemaHash)
               .HasMaxLength(64);

        builder.Property(e => e.CreatedAt)
               .HasColumnType("datetime2(0)")
               .IsRequired();

        builder.Property(e => e.UpdatedAt)
               .HasColumnType("datetime2(0)")
               .IsRequired();

        // Índice único por tenant + usuario + asset + proveedor
        builder.HasIndex(e => new { e.OwnerId, e.UserId, e.AssetId, e.ProviderParticipantId })
               .IsUnique()
               .HasDatabaseName("UQ_ExplorerLayoutConfigs_Tenant_User_Asset");

        // Índice para búsquedas por tenant
        builder.HasIndex(e => e.OwnerId)
               .HasDatabaseName("IX_ExplorerLayoutConfigs_OwnerId");
    }
}
