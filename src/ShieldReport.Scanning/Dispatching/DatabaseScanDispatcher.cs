using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;
using ShieldReport.Scanning.Queueing;

namespace ShieldReport.Scanning.Dispatching;

// The scan row is already persisted as Queued, which is all the database-backed queue needs —
// dispatching just wakes the worker so it claims it without waiting for the next poll.
public sealed class DatabaseScanDispatcher : IScanDispatcher
{
    private readonly ScanQueueSignal _signal;

    public DatabaseScanDispatcher(ScanQueueSignal signal)
    {
        _signal = signal;
    }

    public void Dispatch(long scanId, ScanTool tool)
    {
        _signal.Notify();
    }
}
