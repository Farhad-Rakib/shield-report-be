using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ScanFindingRawConfiguration : IEntityTypeConfiguration<ScanFindingRaw>
{
    public void Configure(EntityTypeBuilder<ScanFindingRaw> builder)
    {
        builder.ToTable("scan_finding_raws");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.RawOutputJson).IsRequired();
        builder.Property(x => x.ParsedTitle).HasMaxLength(300);
        builder.Property(x => x.ParsedEndpoint).HasMaxLength(500);
        builder.Property(x => x.ParsedSeverityRaw).HasMaxLength(50);

        builder.HasIndex(x => x.ScanId);

        builder.HasOne(x => x.Scan)
            .WithMany()
            .HasForeignKey(x => x.ScanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Vulnerability>()
            .WithMany()
            .HasForeignKey(x => x.ResultingVulnerabilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
