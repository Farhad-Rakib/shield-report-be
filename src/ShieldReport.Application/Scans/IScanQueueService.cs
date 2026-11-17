using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans;

public interface IScanQueueService
{
    // Admin-set on the Scan Tools page (ScanQueueSetting); counts queued + running scans.
    Task<int> GetMaxConcurrentScansPerClientAsync(CancellationToken cancellationToken = default);

    Task<int> GetActiveCountAsync(long clientOrganizationId, CancellationToken cancellationToken = default);

    void Enqueue(long scanId, ScanTool tool);
}
