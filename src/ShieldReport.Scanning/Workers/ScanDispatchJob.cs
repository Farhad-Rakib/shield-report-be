using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Workers;

// Minimal Hangfire job — deliberately does nothing but forward into the per-tool channel.
// Hangfire gives us persistence/retry on the "did this scan get dispatched" question; the
// actual docker run work happens in ScanWorkerBackgroundService, which controls concurrency
// independently of Hangfire's own worker pool.
public sealed class ScanDispatchJob
{
    private readonly IScanWorkQueue _scanWorkQueue;

    public ScanDispatchJob(IScanWorkQueue scanWorkQueue)
    {
        _scanWorkQueue = scanWorkQueue;
    }

    public async Task DispatchAsync(long scanId, ScanTool tool, CancellationToken cancellationToken)
    {
        await _scanWorkQueue.EnqueueAsync(scanId, tool, cancellationToken);
    }
}
