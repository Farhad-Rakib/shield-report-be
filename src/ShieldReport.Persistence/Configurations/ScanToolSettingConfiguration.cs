using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ScanToolSettingConfiguration : IEntityTypeConfiguration<ScanToolSetting>
{
    public void Configure(EntityTypeBuilder<ScanToolSetting> builder)
    {
        builder.ToTable("scan_tool_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Tool).IsRequired().HasConversion<byte>();
        builder.Property(x => x.OptionsJson).IsRequired().HasMaxLength(4000);
        builder.HasIndex(x => x.Tool).IsUnique();
    }
}
