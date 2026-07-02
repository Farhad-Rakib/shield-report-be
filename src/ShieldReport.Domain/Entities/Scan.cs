using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class Scan : BaseEntity
{
    public Guid PublicId { get; private set; } = Guid.NewGuid();
    public long ClientOrganizationId { get; private set; }
    public long ClientAssetId { get; private set; }

    // Nullable — ad-hoc/self-service scans don't need one. Real enforced FK now that
    // Engagement exists (see TASK-GROUPS-EngagementManagement.md Group C #5).
    public long? EngagementId { get; private set; }

    // Auto-linked by the service when the scanned asset matches exactly one EngagementTaskAsset
    // under that engagement; left null if no sub-task covers it or more than one does.
    public long? EngagementTaskId { get; private set; }

    public ScanTool Tool { get; private set; }
    public ScanStatus Status { get; private set; } = ScanStatus.Queued;
    public DateTime QueuedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long RequestedByUserId { get; private set; }
    public long? WorkerNodeId { get; private set; }
    public string? RawLogBlobKey { get; private set; }
    // Full captured stdout for the run, persisted so the frontend console can replay it —
    // live SignalR output is missed whenever the scan finishes before the browser's hub
    // connection joins the scan's group (e.g. Reconftw failing in under a second).
    public string? RawOutput { get; private set; }
    public string? ErrorMessage { get; private set; }
    public long? CancelledByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public ClientAsset ClientAsset { get; private set; } = null!;
    public ClientOrganization ClientOrganization { get; private set; } = null!;
    public ScanWorkerNode? WorkerNode { get; private set; }
    public Engagement? Engagement { get; private set; }
    public EngagementTask? EngagementTask { get; private set; }

    private Scan()
    {
    }

    public Scan(long clientOrganizationId, long clientAssetId, ScanTool tool, long requestedByUserId, long? engagementId = null, long? engagementTaskId = null)
    {
        ClientOrganizationId = clientOrganizationId;
        ClientAssetId = clientAssetId;
        Tool = tool;
        RequestedByUserId = requestedByUserId;
        EngagementId = engagementId;
        EngagementTaskId = engagementTaskId;
    }

    public void AssignWorker(long workerNodeId)
    {
        WorkerNodeId = workerNodeId;
    }

    public void Start()
    {
        Status = ScanStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete(string? rawLogBlobKey, string? rawOutput = null)
    {
        Status = ScanStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        RawLogBlobKey = rawLogBlobKey;
        RawOutput = rawOutput;
    }

    // A failed run never auto-creates or updates a Vulnerability (see BUSINESS-FLOW-AutomatedScanning.md §4).
    public void Fail(string errorMessage, string? rawOutput = null)
    {
        Status = ScanStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
        RawOutput = rawOutput;
    }

    public void Cancel(long cancelledByUserId)
    {
        Status = ScanStatus.Cancelled;
        CancelledByUserId = cancelledByUserId;
        CancelledAt = DateTime.UtcNow;
        CompletedAt = DateTime.UtcNow;
    }
}
