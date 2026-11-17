namespace ShieldReport.Domain.Entities;

// Global scan limits (a single row, seeded at startup), edited on Scanning → Tool Configuration.
// Per-tool parallelism lives on ScanToolSetting.MaxParallel.
public sealed class ScanQueueSetting : BaseEntity
{
    public const int DefaultMaxConcurrentScansPerClient = 3;
    public const int DefaultCustomToolMaxParallel = 2;

    // Queued + running scans one client organization may have at once.
    public int MaxConcurrentScansPerClient { get; private set; } = DefaultMaxConcurrentScansPerClient;
    // Registered custom tools share one worker pool of this size.
    public int CustomToolMaxParallel { get; private set; } = DefaultCustomToolMaxParallel;

    public void Update(int maxConcurrentScansPerClient, int customToolMaxParallel, long updatedByUserId)
    {
        MaxConcurrentScansPerClient = maxConcurrentScansPerClient;
        CustomToolMaxParallel = customToolMaxParallel;
        UpdatedBy = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
