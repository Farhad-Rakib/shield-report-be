using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces.Services;

// Decouples the lightweight Hangfire dispatch job from the actual scan execution — the job
// just writes the scan id here; a dedicated background worker drains it at a bounded
// concurrency (TASK-GROUPS-AutomatedScanning.md AS-17). One logical stream per ScanTool so
// scans of different tools never queue behind each other, only same-tool scans do.
public interface IScanWorkQueue
{
    ValueTask EnqueueAsync(long scanId, ScanTool tool, CancellationToken cancellationToken = default);

    IAsyncEnumerable<long> ReadAllAsync(ScanTool tool, CancellationToken cancellationToken = default);
}
