using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class EmailSettingConfiguration : IEntityTypeConfiguration<EmailSetting>
{
    public void Configure(EntityTypeBuilder<EmailSetting> builder)
    {
        builder.ToTable("email_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Host).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(255);
        builder.Property(x => x.PasswordEncrypted).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.FromAddress).IsRequired().HasMaxLength(255);
        builder.Property(x => x.FromName).IsRequired().HasMaxLength(150);
    }
}
