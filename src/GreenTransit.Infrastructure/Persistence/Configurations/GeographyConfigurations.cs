using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreenTransit.Infrastructure.Persistence.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Ref).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(2).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Country");

        builder.HasMany(e => e.States)
               .WithOne(s => s.Country)
               .HasForeignKey(s => s.IdCountry)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TerritoryStateConfiguration : IEntityTypeConfiguration<TerritoryState>
{
    public void Configure(EntityTypeBuilder<TerritoryState> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Ref).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(128);

        builder.HasMany(e => e.Provinces)
               .WithOne(p => p.State)
               .HasForeignKey(p => p.IdState)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Ref).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(64);

        builder.HasMany(e => e.Municipalities)
               .WithOne(m => m.Province)
               .HasForeignKey(m => m.IdProvince)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MunicipalityConfiguration : IEntityTypeConfiguration<Municipality>
{
    public void Configure(EntityTypeBuilder<Municipality> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(6).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.CodeControlNumber).HasMaxLength(1);

        builder.HasMany(e => e.Populations)
               .WithOne(p => p.Municipality)
               .HasForeignKey(p => p.IdMunicipality)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ZipCodes)
               .WithOne(z => z.Municipality)
               .HasForeignKey(z => z.IdMunicipality)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MunicipalityZipCodeConfiguration : IEntityTypeConfiguration<MunicipalityZipCode>
{
    public void Configure(EntityTypeBuilder<MunicipalityZipCode> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ZipCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(256);
    }
}

public class MunicipalityPopulationConfiguration : IEntityTypeConfiguration<MunicipalityPopulation>
{
    public void Configure(EntityTypeBuilder<MunicipalityPopulation> builder)
    {
        builder.HasKey(e => e.Id);
    }
}
