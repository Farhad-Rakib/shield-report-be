using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ReportDeliveryConfiguration : IEntityTypeConfiguration<ReportDelivery>
{
    public void Configure(EntityTypeBuilder<ReportDelivery> builder)
    {
        builder.ToTable("report_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Recipients).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.DeliveredTo).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.Status).IsRequired().HasConversion<byte>();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => x.ReportId);

        // Deleting a report removes its delivery history with it.
        builder.HasOne(x => x.Report).WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.SentByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
