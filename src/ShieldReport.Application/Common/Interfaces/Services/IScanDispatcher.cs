using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces.Services;

// Implemented in ShieldReport.Scanning using Hangfire's IBackgroundJobClient — Application can't
// reference Hangfire directly, same boundary reason as IRealtimeNotifier needing the SignalR hub
// context. The tool is carried alongside the scan id so the queue can route to a per-tool channel
// without an extra DB round trip to look it up.
public interface IScanDispatcher
{
    void Dispatch(long scanId, ScanTool tool);
}
