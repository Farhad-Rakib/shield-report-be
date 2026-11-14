using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ScanAuthorizationConfiguration : IEntityTypeConfiguration<ScanAuthorization>
{
    public void Configure(EntityTypeBuilder<ScanAuthorization> builder)
    {
        builder.ToTable("scan_authorizations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClientContactName).HasMaxLength(200);
        builder.Property(x => x.ClientContactEmail).HasMaxLength(256);
        builder.Property(x => x.AllowedTools).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.EvidenceFilePath).HasMaxLength(500);
        builder.Property(x => x.EvidenceFileName).HasMaxLength(260);
        builder.Property(x => x.EvidenceContentType).HasMaxLength(200);
        builder.Property(x => x.RevocationReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.ClientAssetId, x.RevokedAt });

        builder.HasOne(x => x.ClientAsset)
            .WithMany()
            .HasForeignKey(x => x.ClientAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Engagement>()
            .WithMany()
            .HasForeignKey(x => x.EngagementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorizedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
