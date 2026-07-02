using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ScanWorkerNodeConfiguration : IEntityTypeConfiguration<ScanWorkerNode>
{
    public void Configure(EntityTypeBuilder<ScanWorkerNode> builder)
    {
        builder.ToTable("scan_worker_nodes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.HostAddress).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.MaxConcurrentJobs).IsRequired();

        builder.HasIndex(x => x.Name).IsUnique();
    }
}
