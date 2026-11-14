using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ScanConfiguration : IEntityTypeConfiguration<Scan>
{
    public void Configure(EntityTypeBuilder<Scan> builder)
    {
        builder.ToTable("scans");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tool).IsRequired().HasConversion<byte>();
        builder.Property(x => x.Status).IsRequired().HasConversion<byte>();
        builder.Property(x => x.QueuedAt).IsRequired();
        builder.Property(x => x.RawLogBlobKey).HasMaxLength(500);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.LeaseOwner).HasMaxLength(200);
        builder.HasOne<ScanAuthorization>()
            .WithMany()
            .HasForeignKey(x => x.ScanAuthorizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.HasIndex(x => new { x.ClientOrganizationId, x.Status });
        // Supports the queue claim (ScanRepository.TryClaimNextAsync): per tool, by status.
        builder.HasIndex(x => new { x.Tool, x.Status, x.QueuedAt });
        builder.HasIndex(x => x.NextScanId);

        builder.HasOne(x => x.ClientOrganization)
            .WithMany()
            .HasForeignKey(x => x.ClientOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClientAsset)
            .WithMany()
            .HasForeignKey(x => x.ClientAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WorkerNode)
            .WithMany()
            .HasForeignKey(x => x.WorkerNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Engagement)
            .WithMany()
            .HasForeignKey(x => x.EngagementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EngagementTask)
            .WithMany()
            .HasForeignKey(x => x.EngagementTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Self-referencing chain pointer (Naabu -> Nuclei -> Reconftw) — Restrict, not Cascade,
        // since SQL Server rejects cascade paths that could revisit the same table twice.
        builder.HasOne(x => x.NextScan)
            .WithMany()
            .HasForeignKey(x => x.NextScanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
