using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class RegistrationInviteConfiguration : IEntityTypeConfiguration<RegistrationInvite>
{
    public void Configure(EntityTypeBuilder<RegistrationInvite> builder)
    {
        builder.ToTable("registration_invites");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.TokenHash).IsRequired();
        builder.Property(x => x.RoleId).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.Email);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClientOrganization)
            .WithMany()
            .HasForeignKey(x => x.ClientOrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Three separate FKs into Users (creator/registered/revoker) — all Restrict to avoid
        // SQL Server's "multiple cascade paths" error from stacking cascades on the same table.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RegisteredUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
