using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans;

public interface IScanQueueService
{
    int MaxConcurrentScansPerClient { get; }

    Task<int> GetActiveCountAsync(long clientOrganizationId, CancellationToken cancellationToken = default);

    void Enqueue(long scanId, ScanTool tool);
}
