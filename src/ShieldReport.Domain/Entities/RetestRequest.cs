using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class RetestRequest : BaseEntity
{
    public long VulnerabilityId { get; private set; }
    public long RequestedByUserId { get; private set; }
    public DateTime RequestedAt { get; private set; } = DateTime.UtcNow;
    public long? AssignedToUserId { get; private set; }
    public string? Instructions { get; private set; }
    public RetestRequestStatus Status { get; private set; } = RetestRequestStatus.Pending;
    public long? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolutionNotes { get; private set; }

    public Vulnerability Vulnerability { get; private set; } = null!;
    public User RequestedByUser { get; private set; } = null!;
    public User? AssignedToUser { get; private set; }
    public User? ResolvedByUser { get; private set; }
    public ICollection<RetestRequestCase> Cases { get; private set; } = new List<RetestRequestCase>();

    private RetestRequest()
    {
    }

    public RetestRequest(long vulnerabilityId, long requestedByUserId, long? assignedToUserId = null, string? instructions = null)
    {
        VulnerabilityId = vulnerabilityId;
        RequestedByUserId = requestedByUserId;
        AssignedToUserId = assignedToUserId;
        Instructions = instructions;
    }

    public void VerifyClosed(long resolvedByUserId, string? resolutionNotes)
    {
        Status = RetestRequestStatus.VerifiedClosed;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = DateTime.UtcNow;
        ResolutionNotes = resolutionNotes;
    }

    public void Reopen(long resolvedByUserId, string? resolutionNotes)
    {
        Status = RetestRequestStatus.Reopened;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAt = DateTime.UtcNow;
        ResolutionNotes = resolutionNotes;
    }
}
