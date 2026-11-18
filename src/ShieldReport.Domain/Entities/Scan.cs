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
    // Set when Tool == Custom: which registered custom tool this scan runs.
    public long? CustomScanToolId { get; private set; }
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

    // Auto-chaining: when a multi-tool scan is requested, ScanService creates one Scan row per
    // tool up front (in Naabu -> Nuclei -> Reconftw order), each linked to the next via this
    // self-FK. The database-backed queue (ScanRepository.TryClaimNextAsync) only hands out a
    // Queued scan once no Queued/Running scan points at it, so the tools run one at a time.
    public long? NextScanId { get; private set; }

    // Queue lease (see ScanWorkerBackgroundService). Whoever holds an unexpired lease owns this
    // scan: it claimed it from the Queued backlog, or is attached to its running container. The
    // owner renews the lease while working; if the process dies the lease expires and another
    // claim re-attaches to the still-running container (or fails the scan if it is gone).
    // The ScanAuthorization this scan was permitted under (null only for scans created before
    // authorization records existed).
    public long? ScanAuthorizationId { get; private set; }

    // Tool settings resolved when the scan was queued (ScanToolRuntimeSettings as JSON), so later
    // changes on the Scan Tools page don't alter queued or running scans. Null for older scans.
    public string? ToolSettingsJson { get; private set; }

    public string? LeaseOwner { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }

    public ClientAsset ClientAsset { get; private set; } = null!;
    public ClientOrganization ClientOrganization { get; private set; } = null!;
    public ScanWorkerNode? WorkerNode { get; private set; }
    public Engagement? Engagement { get; private set; }
    public EngagementTask? EngagementTask { get; private set; }
    public Scan? NextScan { get; private set; }

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
        // The column holds 1000 characters; tool stderr (e.g. a Python traceback when a container
        // is killed) can be longer, and an over-long value made the whole save fail, losing the
        // real error and the raw output. Keep the start (the summary) and mark the cut.
        ErrorMessage = errorMessage.Length > ErrorMessageMaxLength
            ? errorMessage[..(ErrorMessageMaxLength - 15)] + " … (truncated)"
            : errorMessage;
        RawOutput = rawOutput;
    }

    public const int ErrorMessageMaxLength = 1000;

    public void Cancel(long cancelledByUserId)
    {
        Status = ScanStatus.Cancelled;
        CancelledByUserId = cancelledByUserId;
        CancelledAt = DateTime.UtcNow;
        CompletedAt = DateTime.UtcNow;
    }

    public void SetCustomScanTool(long customScanToolId)
    {
        CustomScanToolId = customScanToolId;
    }

    public void SetToolSettingsSnapshot(string toolSettingsJson)
    {
        ToolSettingsJson = toolSettingsJson;
    }

    public void SetScanAuthorization(long scanAuthorizationId)
    {
        ScanAuthorizationId = scanAuthorizationId;
    }

    // Called on every terminal transition so a finished scan never looks owned.
    public void ReleaseLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    public void SetNextScan(long nextScanId)
    {
        NextScanId = nextScanId;
    }
}
