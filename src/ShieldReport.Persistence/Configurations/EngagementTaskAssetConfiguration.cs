using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class EngagementTaskAssetConfiguration : IEntityTypeConfiguration<EngagementTaskAsset>
{
    public void Configure(EntityTypeBuilder<EngagementTaskAsset> builder)
    {
        builder.ToTable("engagement_task_assets");

        builder.HasKey(x => new { x.EngagementTaskId, x.ClientAssetId });

        builder.HasOne(x => x.EngagementTask)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.EngagementTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ClientAsset)
            .WithMany()
            .HasForeignKey(x => x.ClientAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
