namespace ShieldReport.Domain.Entities;

// Plain join table (no BaseEntity, composite PK) — lets an engagement have helpers
// beyond the lead pentester. Assigning someone to a sub-task auto-adds them here too.
public sealed class EngagementAssignee
{
    public long EngagementId { get; set; }
    public long UserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Engagement Engagement { get; set; } = null!;
    public User User { get; set; } = null!;
}
