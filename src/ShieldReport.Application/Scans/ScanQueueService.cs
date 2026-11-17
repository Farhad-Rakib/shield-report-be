using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans;

public sealed class ScanQueueService : IScanQueueService
{
    private readonly IScanRepository _scanRepository;
    private readonly IScanDispatcher _scanDispatcher;
    private readonly IRepository<ScanQueueSetting> _queueSettings;

    public ScanQueueService(IScanRepository scanRepository, IScanDispatcher scanDispatcher, IRepository<ScanQueueSetting> queueSettings)
    {
        _scanRepository = scanRepository;
        _scanDispatcher = scanDispatcher;
        _queueSettings = queueSettings;
    }

    // Was a hardcoded 3 (the original spec); now set on the Scan Tools page.
    public async Task<int> GetMaxConcurrentScansPerClientAsync(CancellationToken cancellationToken = default)
    {
        var setting = (await _queueSettings.GetAllAsync(cancellationToken)).OrderBy(s => s.Id).FirstOrDefault();
        return setting?.MaxConcurrentScansPerClient ?? ScanQueueSetting.DefaultMaxConcurrentScansPerClient;
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
