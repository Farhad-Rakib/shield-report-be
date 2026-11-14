using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("background_jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasConversion<byte>();
        builder.Property(x => x.LeaseOwner).HasMaxLength(200);
        builder.Property(x => x.LastError).HasMaxLength(2000);

        // Supports the claim query (BackgroundJobRepository.TryClaimNextAsync).
        builder.HasIndex(x => new { x.Status, x.RunAfter });
    }
}
