using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class CvssSeverityBandConfiguration : IEntityTypeConfiguration<CvssSeverityBand>
{
    public void Configure(EntityTypeBuilder<CvssSeverityBand> builder)
    {
        builder.ToTable("cvss_severity_bands");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Severity).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.MinScore).IsRequired().HasColumnType("decimal(3,1)");
        builder.Property(x => x.MaxScore).IsRequired().HasColumnType("decimal(3,1)");

        builder.HasIndex(x => x.Severity).IsUnique();
    }
}
