using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class EngagementTask : BaseEntity
{
    public long EngagementId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public long AssignedToUserId { get; private set; }

    // Independent of the parent Engagement.Status — a lead pentester or admin can see
    // "web testing is done, network testing is still in progress" at the sub-task level.
    public EngagementTaskStatus Status { get; private set; } = EngagementTaskStatus.NotStarted;
    public long CreatedByUserId { get; private set; }

    public Engagement Engagement { get; private set; } = null!;
    public User AssignedToUser { get; private set; } = null!;
    public ICollection<EngagementTaskAsset> Assets { get; private set; } = new List<EngagementTaskAsset>();

    private EngagementTask()
    {
    }

    public EngagementTask(long engagementId, string title, long assignedToUserId, long createdByUserId, string? description = null)
    {
        EngagementId = engagementId;
        Title = !string.IsNullOrWhiteSpace(title)
            ? title.Trim()
            : throw new ArgumentException("Title is required.", nameof(title));
        AssignedToUserId = assignedToUserId;
        CreatedByUserId = createdByUserId;
        Description = description;
    }

    public void UpdateDetails(string title, string? description)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        Description = description;
    }

    public void Reassign(long assignedToUserId)
    {
        AssignedToUserId = assignedToUserId;
    }

    public void SetStatus(EngagementTaskStatus status)
    {
        Status = status;
    }
}
