using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ClientAssetConfiguration : IEntityTypeConfiguration<ClientAsset>
{
    public void Configure(EntityTypeBuilder<ClientAsset> builder)
    {
        builder.ToTable("client_assets");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AssetType).IsRequired().HasConversion<byte>();
        builder.Property(x => x.Identifier).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Environment).IsRequired().HasConversion<byte>();
        builder.Property(x => x.Criticality).IsRequired().HasConversion<byte>();
        builder.Property(x => x.IsAuthorizedForScanning).HasDefaultValue(false);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.HasIndex(x => x.ClientOrganizationId);

        builder.HasOne(x => x.ClientOrganization)
            .WithMany()
            .HasForeignKey(x => x.ClientOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorizedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
