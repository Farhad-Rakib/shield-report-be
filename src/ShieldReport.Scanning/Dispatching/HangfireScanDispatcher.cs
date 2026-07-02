using Hangfire;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;
using ShieldReport.Scanning.Workers;

namespace ShieldReport.Scanning.Dispatching;

public sealed class HangfireScanDispatcher : IScanDispatcher
{
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireScanDispatcher(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void Dispatch(long scanId, ScanTool tool)
    {
        _backgroundJobClient.Enqueue<ScanDispatchJob>(job => job.DispatchAsync(scanId, tool, CancellationToken.None));
    }
}
