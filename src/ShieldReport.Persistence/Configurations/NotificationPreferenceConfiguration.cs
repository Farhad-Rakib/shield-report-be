using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId);
        builder.Property(x => x.ClientOrganizationId);
        builder.Property(x => x.NotificationType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IsEnabled).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ClientOrganization>()
            .WithMany()
            .HasForeignKey(x => x.ClientOrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // One row per (UserId, NotificationType) and per (ClientOrganizationId, NotificationType) — filtered
        // because exactly one of the two FKs is set on any given row (enforced in the constructor).
        builder.HasIndex(x => new { x.UserId, x.NotificationType })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(x => new { x.ClientOrganizationId, x.NotificationType })
            .IsUnique()
            .HasFilter("[ClientOrganizationId] IS NOT NULL");
    }
}
