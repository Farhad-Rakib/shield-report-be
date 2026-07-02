namespace ShieldReport.Domain.Entities;

// Plain join table (no BaseEntity, composite PK) — ties a sub-task to one or more
// specific assets. The service layer validates every referenced asset belongs to the
// same engagement's ClientOrganization before this row is created.
public sealed class EngagementTaskAsset
{
    public long EngagementTaskId { get; set; }
    public long ClientAssetId { get; set; }

    public EngagementTask EngagementTask { get; set; } = null!;
    public ClientAsset ClientAsset { get; set; } = null!;
}
