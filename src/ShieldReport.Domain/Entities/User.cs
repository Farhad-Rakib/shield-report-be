using ShieldReport.Domain.ValueObjects;

namespace ShieldReport.Domain.Entities;

public sealed class User : BaseEntity
{
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    public string? ProfileImageUrl { get; private set; }
    public bool MfaEnabled { get; private set; }
    public string? MfaSecretKey { get; private set; }

    public long? ClientOrganizationId { get; private set; }
    public bool IsClientPortalUser { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
    public ClientOrganization? ClientOrganization { get; private set; }

    private User()
    {
    }

    public User(string fullName, string email, string passwordHash)
    {
        FullName = !string.IsNullOrWhiteSpace(fullName)
            ? fullName.Trim()
            : throw new ArgumentException("Full name is required.", nameof(fullName));

        Email = EmailAddress.Create(email).Value;
        PasswordHash = !string.IsNullOrWhiteSpace(passwordHash)
            ? passwordHash
            : throw new ArgumentException("Password hash is required.", nameof(passwordHash));
    }

    public void SetRoles(IEnumerable<UserRole> roles)
    {
        UserRoles = roles.ToList();
    }

    // A client portal user must always belong to an org — there is no "client portal
    // user with no org" state, so the org id is a required parameter, not optional.
    public void SetClientPortalContext(long clientOrganizationId)
    {
        ClientOrganizationId = clientOrganizationId;
        IsClientPortalUser = true;
    }

    public void ClearClientPortalContext()
    {
        ClientOrganizationId = null;
        IsClientPortalUser = false;
    }

    public void Disable()
    {
        IsActive = false;
    }

    public void UpdateProfile(string fullName, string email, string? profileImageUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(fullName))
            FullName = fullName.Trim();
        if (!string.IsNullOrWhiteSpace(email))
            Email = EmailAddress.Create(email).Value;
        if (profileImageUrl != null)
            ProfileImageUrl = profileImageUrl;
    }

    public void UpdatePassword(string passwordHash)
    {
        PasswordHash = !string.IsNullOrWhiteSpace(passwordHash)
            ? passwordHash
            : throw new ArgumentException("Password hash is required.", nameof(passwordHash));
    }

    public void EnableMfa(string secretKey)
    {
        MfaSecretKey = !string.IsNullOrWhiteSpace(secretKey)
            ? secretKey
            : throw new ArgumentException("Secret key is required.", nameof(secretKey));
        MfaEnabled = true;
    }

    public void DisableMfa()
    {
        MfaEnabled = false;
        MfaSecretKey = null;
    }
}
