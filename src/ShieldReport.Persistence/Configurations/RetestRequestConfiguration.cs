using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class RetestRequestConfiguration : IEntityTypeConfiguration<RetestRequest>
{
    public void Configure(EntityTypeBuilder<RetestRequest> builder)
    {
        builder.ToTable("retest_requests");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<byte>();
        builder.Property(x => x.Instructions).HasMaxLength(2000);
        builder.Property(x => x.ResolutionNotes).HasMaxLength(2000);

        // General lookup index — "get latest retest for this vulnerability" spans all statuses.
        builder.HasIndex(x => x.VulnerabilityId).HasDatabaseName("IX_retest_requests_VulnerabilityId");

        // Enforces "only one Pending retest per vulnerability at a time" at the DB level —
        // see BUSINESS-FLOW-PentestOps.md §8. Status = 1 is RetestRequestStatus.Pending.
        builder.HasIndex(x => x.VulnerabilityId)
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("IX_retest_requests_VulnerabilityId_Pending");

        builder.HasOne(x => x.Vulnerability)
            .WithMany()
            .HasForeignKey(x => x.VulnerabilityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedToUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResolvedByUser)
            .WithMany()
            .HasForeignKey(x => x.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Cases)
            .WithOne(x => x.RetestRequest)
            .HasForeignKey(x => x.RetestRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
