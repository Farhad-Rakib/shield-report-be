using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

// Proof that scanning an asset was permitted: who approved it on our side, the client contact who
// gave permission, optional evidence (signed scope letter / approval email), the validity window
// and the tools allowed. Records are never deleted or edited after creation — revoking keeps the
// row (with who/when/why), so every past scan can still be traced to the authorization it ran
// under (Scan.ScanAuthorizationId).
public sealed class ScanAuthorization : BaseEntity
{
    public long ClientAssetId { get; private set; }
    public long? EngagementId { get; private set; }
    public long AuthorizedByUserId { get; private set; }
    public string? ClientContactName { get; private set; }
    public string? ClientContactEmail { get; private set; }
    public DateTime ValidFrom { get; private set; }
    // Null = no end date (only produced by migrating the legacy on/off flag).
    public DateTime? ValidUntil { get; private set; }
    // Comma-separated ScanTool names, e.g. "Naabu,Nuclei".
    public string AllowedTools { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string? EvidenceFilePath { get; private set; }
    public string? EvidenceFileName { get; private set; }
    public string? EvidenceContentType { get; private set; }
    public DateTime? EvidenceUploadedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public long? RevokedByUserId { get; private set; }
    public string? RevocationReason { get; private set; }

    public ClientAsset ClientAsset { get; private set; } = null!;

    private ScanAuthorization()
    {
    }

    public ScanAuthorization(
        long clientAssetId,
        long? engagementId,
        long authorizedByUserId,
        DateTime validFrom,
        DateTime? validUntil,
        IReadOnlyCollection<ScanTool> allowedTools,
        string? clientContactName,
        string? clientContactEmail,
        string? notes)
    {
        if (allowedTools.Count == 0)
        {
            throw new ArgumentException("At least one tool must be allowed.", nameof(allowedTools));
        }

        if (validUntil.HasValue && validUntil <= validFrom)
        {
            throw new ArgumentException("The authorization must end after it starts.", nameof(validUntil));
        }

        ClientAssetId = clientAssetId;
        EngagementId = engagementId;
        AuthorizedByUserId = authorizedByUserId;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        AllowedTools = string.Join(',', allowedTools.Distinct().OrderBy(t => (int)t));
        ClientContactName = clientContactName;
        ClientContactEmail = clientContactEmail;
        Notes = notes;
    }

    public IReadOnlyList<ScanTool> GetAllowedTools() =>
        AllowedTools.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => Enum.TryParse<ScanTool>(name, out var tool) ? (ScanTool?)tool : null)
            .Where(tool => tool.HasValue)
            .Select(tool => tool!.Value)
            .ToList();

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsActiveAt(DateTime utcNow) => !IsRevoked && ValidFrom <= utcNow && (ValidUntil is null || ValidUntil > utcNow);

    public bool Allows(ScanTool tool) => GetAllowedTools().Contains(tool);

    public void AttachEvidence(string filePath, string fileName, string? contentType)
    {
        EvidenceFilePath = filePath;
        EvidenceFileName = fileName;
        EvidenceContentType = contentType;
        EvidenceUploadedAt = DateTime.UtcNow;
    }

    public void Revoke(long revokedByUserId, string reason)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = DateTime.UtcNow;
        RevokedByUserId = revokedByUserId;
        RevocationReason = reason;
    }
}
