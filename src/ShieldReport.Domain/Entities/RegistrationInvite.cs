using ShieldReport.Domain.ValueObjects;

namespace ShieldReport.Domain.Entities;

public sealed class RegistrationInvite : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public long RoleId { get; private set; }
    public long? ClientOrganizationId { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public long CreatedByUserId { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public long? RegisteredUserId { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public long? RevokedByUserId { get; private set; }

    public Role Role { get; private set; } = null!;
    public ClientOrganization? ClientOrganization { get; private set; }

    private RegistrationInvite()
    {
    }

    public RegistrationInvite(string email, string tokenHash, long roleId, long? clientOrganizationId, DateTime expiresAtUtc, long createdByUserId)
    {
        Email = EmailAddress.Create(email).Value;
        TokenHash = !string.IsNullOrWhiteSpace(tokenHash)
            ? tokenHash
            : throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        RoleId = roleId;
        ClientOrganizationId = clientOrganizationId;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByUserId = createdByUserId;
    }

    // Lifetime is locked at creation — neither role nor expiry can be edited after the fact,
    // only revoked and reissued as a new invite (see BUSINESS-FLOW-InvitationRegistration.md).
    public bool IsValid => ConsumedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;

    public void MarkConsumed(long registeredUserId)
    {
        if (ConsumedAtUtc is not null)
        {
            return;
        }

        ConsumedAtUtc = DateTime.UtcNow;
        RegisteredUserId = registeredUserId;
    }

    public void Revoke(long revokedByUserId)
    {
        if (RevokedAtUtc is not null)
        {
            return;
        }

        RevokedAtUtc = DateTime.UtcNow;
        RevokedByUserId = revokedByUserId;
    }
}
