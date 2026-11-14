using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");
        builder.Property(x => x.NarrativeStatus).HasConversion<byte>();
        builder.Property(x => x.NarrativeError).HasMaxLength(1000);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasConversion<byte>();
        builder.Property(x => x.ExecutiveSummary).IsRequired();
        builder.Property(x => x.KeyFindings).IsRequired();
        builder.Property(x => x.KeyRecommendations).IsRequired();
        builder.Property(x => x.DraftDocxPath).HasMaxLength(500);
        builder.Property(x => x.FinalPdfPath).HasMaxLength(500);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.ProjectGoals).IsRequired();
        builder.Property(x => x.Limitations).IsRequired();
        builder.Property(x => x.ScopeStatusesJson).IsRequired().HasMaxLength(4000);

        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.HasIndex(x => new { x.EngagementId, x.Version }).IsUnique();

        builder.HasOne(x => x.Engagement)
            .WithMany()
            .HasForeignKey(x => x.EngagementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReportTemplate)
            .WithMany()
            .HasForeignKey(x => x.ReportTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.User>()
            .WithMany()
            .HasForeignKey(x => x.GeneratedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.User>()
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.User>()
            .WithMany()
            .HasForeignKey(x => x.RejectedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
