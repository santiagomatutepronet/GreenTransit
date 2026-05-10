using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreenTransit.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("Profiles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reference).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(512);

        builder.HasIndex(e => e.Reference).IsUnique().HasDatabaseName("UX_Profiles_Reference");
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Login).HasMaxLength(256).IsRequired();
        builder.Property(e => e.CompleteName).HasMaxLength(512);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.ZipCode).HasMaxLength(16);
        builder.Property(e => e.Address).HasMaxLength(512);
        builder.Property(e => e.PortalEDCProvider).HasMaxLength(512);
        builder.Property(e => e.PortalEDCConsumer).HasMaxLength(512);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);

        builder.HasOne(u => u.Profile)
               .WithMany(p => p.Users)
               .HasForeignKey(u => u.IdProfile)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Country)
               .WithMany()
               .HasForeignKey(u => u.NationalId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.TerritoryState)
               .WithMany()
               .HasForeignKey(u => u.GeographicalId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Municipality)
               .WithMany()
               .HasForeignKey(u => u.MunicipalityId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.UserSharePointCredentials)
               .WithOne(c => c.User)
               .HasForeignKey<UserSharePointCredential>(c => c.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.Login).IsUnique().HasDatabaseName("UX_Users_Login");
        builder.HasIndex(e => e.OwnerId).HasDatabaseName("IX_Users_OwnerId");
        builder.HasIndex(e => e.IdProfile).HasDatabaseName("IX_Users_IdProfile");
    }
}

public class PageDefinitionConfiguration : IEntityTypeConfiguration<PageDefinition>
{
    public void Configure(EntityTypeBuilder<PageDefinition> builder)
    {
        builder.ToTable("PageDefinitions");
        builder.HasKey(e => e.ID);
        builder.Property(e => e.Route).HasMaxLength(256).IsRequired();
        builder.Property(e => e.PageName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.ModuleName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ComponentName).HasMaxLength(256);
        builder.HasIndex(e => e.Route).IsUnique().HasDatabaseName("UQ_PageDefinitions_Route");
        builder.HasMany(e => e.Permissions).WithOne(p => p.PageDefinition).HasForeignKey(p => p.IdPageDefinition);
    }
}

public class PagePermissionConfiguration : IEntityTypeConfiguration<PagePermission>
{
    public void Configure(EntityTypeBuilder<PagePermission> builder)
    {
        builder.ToTable("PagePermissions");
        builder.HasKey(e => e.ID);
        builder.Property(e => e.AccessLevel).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => new { e.IdPageDefinition, e.IdProfile }).IsUnique()
               .HasDatabaseName("UQ_PagePermissions_Page_Profile");
        builder.HasOne(e => e.PageDefinition).WithMany(p => p.Permissions).HasForeignKey(e => e.IdPageDefinition);
        builder.HasOne(e => e.Profile).WithMany().HasForeignKey(e => e.IdProfile);
    }
}
