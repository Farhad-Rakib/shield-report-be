using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class CustomScanToolConfiguration : IEntityTypeConfiguration<CustomScanTool>
{
    public void Configure(EntityTypeBuilder<CustomScanTool> builder)
    {
        builder.ToTable("custom_scan_tools");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ClientLabel).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Image).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ArgumentsJson).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.OutputFormat).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DefaultSeverity).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
