using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans;

public sealed class ScanQueueService : IScanQueueService
{
    // Confirmed against the external spec cross-check (TASK-GROUPS-AutomatedScanning.md
    // "Cross-checked against external spec") — 3 concurrent scans per client, hard cap.
    public int MaxConcurrentScansPerClient => 3;

    private readonly IScanRepository _scanRepository;
    private readonly IScanDispatcher _scanDispatcher;

    public ScanQueueService(IScanRepository scanRepository, IScanDispatcher scanDispatcher)
    {
        _scanRepository = scanRepository;
        _scanDispatcher = scanDispatcher;
    }

    public async Task<int> GetActiveCountAsync(long clientOrganizationId, CancellationToken cancellationToken = default)
    {
        return await _scanRepository.CountActiveByClientAsync(clientOrganizationId, cancellationToken);
    }

    public void Enqueue(long scanId, ScanTool tool)
    {
        _scanDispatcher.Dispatch(scanId, tool);
    }
}
