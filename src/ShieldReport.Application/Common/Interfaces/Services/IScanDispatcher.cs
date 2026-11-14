using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces.Services;

// Signals that a scan row was persisted as Queued. Implemented in ShieldReport.Scanning by
// DatabaseScanDispatcher, which just wakes the database-backed queue worker (the row itself is
// the queue entry), so callers must save the scan before dispatching.
public interface IScanDispatcher
{
    void Dispatch(long scanId, ScanTool tool);
}
