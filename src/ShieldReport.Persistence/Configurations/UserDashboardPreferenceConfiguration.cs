using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class UserDashboardPreferenceConfiguration : IEntityTypeConfiguration<UserDashboardPreference>
{
    public void Configure(EntityTypeBuilder<UserDashboardPreference> builder)
    {
        builder.ToTable("user_dashboard_preferences");

        // UserId is both the PK and the FK — one row per user
        builder.HasKey(p => p.UserId);

        builder.HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<UserDashboardPreference>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.WidgetOrder)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]");

        builder.Property(p => p.HiddenWidgets)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]");

        builder.Property(p => p.UpdatedAt)
            .IsRequired();
    }
}
