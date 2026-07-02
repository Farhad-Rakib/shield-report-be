using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class Engagement : BaseEntity
{
    public Guid PublicId { get; private set; } = Guid.NewGuid();
    public long ClientOrganizationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Scope { get; private set; }
    public EngagementStatus Status { get; private set; } = EngagementStatus.Scheduled;
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public long LeadPentesterId { get; private set; }
    public long CreatedByUserId { get; private set; }

    public ClientOrganization ClientOrganization { get; private set; } = null!;
    public User LeadPentester { get; private set; } = null!;
    public ICollection<EngagementAssignee> Assignees { get; private set; } = new List<EngagementAssignee>();
    public ICollection<EngagementTask> Tasks { get; private set; } = new List<EngagementTask>();

    private Engagement()
    {
    }

    public Engagement(long clientOrganizationId, string title, long leadPentesterId, long createdByUserId, string? scope = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        ClientOrganizationId = clientOrganizationId;
        Title = !string.IsNullOrWhiteSpace(title)
            ? title.Trim()
            : throw new ArgumentException("Title is required.", nameof(title));
        LeadPentesterId = leadPentesterId;
        CreatedByUserId = createdByUserId;
        Scope = scope;
        StartDate = startDate;
        EndDate = endDate;
    }

    public void UpdateDetails(string title, string? scope, DateTime? startDate, DateTime? endDate)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        Scope = scope;
        StartDate = startDate;
        EndDate = endDate;
    }

    public void ChangeStatus(EngagementStatus status)
    {
        Status = status;
    }

    public void ReassignLead(long leadPentesterId)
    {
        LeadPentesterId = leadPentesterId;
    }
}
