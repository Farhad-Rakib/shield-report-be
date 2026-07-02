namespace ShieldReport.Domain.Entities;

public sealed class MfaRecoveryCode : BaseEntity
{
    public long UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime? UsedAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    private MfaRecoveryCode()
    {
    }

    public MfaRecoveryCode(long userId, string codeHash)
    {
        UserId = userId;
        CodeHash = !string.IsNullOrWhiteSpace(codeHash)
            ? codeHash
            : throw new ArgumentException("Code hash is required.", nameof(codeHash));
    }

    public bool IsActive => UsedAtUtc is null;

    public void MarkAsUsed()
    {
        UsedAtUtc ??= DateTime.UtcNow;
    }
}
