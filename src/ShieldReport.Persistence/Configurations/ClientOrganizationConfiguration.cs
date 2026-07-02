using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ClientOrganizationConfiguration : IEntityTypeConfiguration<ClientOrganization>
{
    public void Configure(EntityTypeBuilder<ClientOrganization> builder)
    {
        builder.ToTable("client_organizations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicId).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PrimaryContactName).HasMaxLength(200);
        builder.Property(x => x.PrimaryContactEmail).HasMaxLength(256);
        builder.Property(x => x.AllowSelfServiceScanning).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
