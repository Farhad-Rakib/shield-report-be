using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class ScanWorkerNode : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string HostAddress { get; private set; } = string.Empty;
    public WorkerNodeStatus Status { get; private set; } = WorkerNodeStatus.Offline;
    public int MaxConcurrentJobs { get; private set; } = 1;

    private ScanWorkerNode()
    {
    }

    public ScanWorkerNode(string name, string hostAddress, int maxConcurrentJobs = 1)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Name is required.", nameof(name));
        HostAddress = !string.IsNullOrWhiteSpace(hostAddress)
            ? hostAddress.Trim()
            : throw new ArgumentException("Host address is required.", nameof(hostAddress));
        MaxConcurrentJobs = maxConcurrentJobs > 0 ? maxConcurrentJobs : 1;
        Status = WorkerNodeStatus.Online;
    }

    public void UpdateStatus(WorkerNodeStatus status)
    {
        Status = status;
    }
}
