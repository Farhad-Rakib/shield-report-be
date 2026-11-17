using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class ScanQueueSettingConfiguration : IEntityTypeConfiguration<ScanQueueSetting>
{
    public void Configure(EntityTypeBuilder<ScanQueueSetting> builder)
    {
        builder.ToTable("scan_queue_settings");
        builder.HasKey(x => x.Id);
    }
}
