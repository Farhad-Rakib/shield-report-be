using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class EngagementAssigneeConfiguration : IEntityTypeConfiguration<EngagementAssignee>
{
    public void Configure(EntityTypeBuilder<EngagementAssignee> builder)
    {
        builder.ToTable("engagement_assignees");

        builder.HasKey(x => new { x.EngagementId, x.UserId });
        builder.Property(x => x.AssignedAt).IsRequired();

        builder.HasOne(x => x.Engagement)
            .WithMany(x => x.Assignees)
            .HasForeignKey(x => x.EngagementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
