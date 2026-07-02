using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Application.Vulnerabilities;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Workers;

// Drains IScanWorkQueue at a bounded concurrency independent of Hangfire's own worker pool
// (TASK-GROUPS-AutomatedScanning.md AS-17). One drain loop + one SemaphoreSlim(1,1) per
// ScanTool — concurrency is hardcoded to 1 per tool for the MVP single ScanWorkerNode row
// seeded in Phase 0, but scans of different tools no longer block each other. Revisit if/when
// a second worker node or higher per-tool throughput is needed.
public sealed class ScanWorkerBackgroundService : BackgroundService
{
    private readonly IScanWorkQueue _scanWorkQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<ScanWorkerBackgroundService> _logger;
    private readonly Dictionary<ScanTool, SemaphoreSlim> _concurrencyGates =
        Enum.GetValues<ScanTool>().ToDictionary(tool => tool, _ => new SemaphoreSlim(1, 1));

    public ScanWorkerBackgroundService(
        IScanWorkQueue scanWorkQueue,
        IServiceScopeFactory scopeFactory,
        IScanRealtimeNotifier realtimeNotifier,
        ILogger<ScanWorkerBackgroundService> logger)
    {
        _scanWorkQueue = scanWorkQueue;
        _scopeFactory = scopeFactory;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var toolLoops = Enum.GetValues<ScanTool>().Select(tool => RunToolQueueAsync(tool, stoppingToken));
        await Task.WhenAll(toolLoops);
    }

    private async Task RunToolQueueAsync(ScanTool tool, CancellationToken stoppingToken)
    {
        var gate = _concurrencyGates[tool];

        await foreach (var scanId in _scanWorkQueue.ReadAllAsync(tool, stoppingToken))
        {
            await gate.WaitAsync(stoppingToken);
            _ = ProcessScanAsync(scanId, stoppingToken).ContinueWith(
                _ => gate.Release(),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    private async Task ProcessScanAsync(long scanId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var scanRepository = scope.ServiceProvider.GetRequiredService<IScanRepository>();
        var clientAssetRepository = scope.ServiceProvider.GetRequiredService<IClientAssetRepository>();
        var scanRunner = scope.ServiceProvider.GetRequiredService<IScanRunner>();
        var parserFactory = scope.ServiceProvider.GetRequiredService<IScanOutputParserFactory>();
        var dedupService = scope.ServiceProvider.GetRequiredService<IVulnerabilityDedupService>();
        var findingRepository = scope.ServiceProvider.GetRequiredService<IScanFindingRawRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var scan = await scanRepository.GetByIdWithDetailsAsync(scanId, cancellationToken);
        if (scan is null || scan.Status != ScanStatus.Queued)
        {
            return;
        }

        var asset = await clientAssetRepository.GetByIdWithDetailsAsync(scan.ClientAssetId, cancellationToken);
        if (asset is null)
        {
            scan.Fail("Client asset no longer exists.");
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        scan.Start();
        scanRepository.Update(scan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await PushStatusAsync(scan, cancellationToken);

        try
        {
            var result = await scanRunner.RunAsync(scan, asset, line => PushOutputLineAsync(scan, line, cancellationToken), cancellationToken);

            if (!result.Success)
            {
                scan.Fail(result.ErrorMessage ?? "Scan failed.", result.RawOutput);
                scanRepository.Update(scan);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await PushStatusAsync(scan, cancellationToken);
                return;
            }

            var parser = parserFactory.GetParser(scan.Tool);
            var parsedFindings = parser.Parse(result.RawOutput);

            foreach (var finding in parsedFindings)
            {
                var findingRaw = new ScanFindingRaw(scan.Id, JsonSerializer.Serialize(finding), finding.Title, finding.Endpoint, finding.SeverityRaw);
                await findingRepository.AddAsync(findingRaw, cancellationToken);

                var vulnerability = await dedupService.ProcessFindingAsync(
                    scan.ClientOrganizationId,
                    scan.ClientAssetId,
                    scan.Id,
                    scan.EngagementId,
                    scan.EngagementTaskId,
                    scan.Tool.ToString(),
                    finding,
                    cancellationToken);

                findingRaw.MarkProcessed(vulnerability.Id);
                findingRepository.Update(findingRaw);
            }

            scan.Complete(rawLogBlobKey: null, rawOutput: result.RawOutput);
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await PushStatusAsync(scan, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanId} failed unexpectedly.", scanId);
            scan.Fail($"Unexpected error: {ex.Message}");
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await PushStatusAsync(scan, cancellationToken);
        }
    }

    private async Task PushOutputLineAsync(Scan scan, string line, CancellationToken cancellationToken)
    {
        await _realtimeNotifier.PushScanOutputAsync(scan.PublicId, line, cancellationToken);
    }

    private async Task PushStatusAsync(Scan scan, CancellationToken cancellationToken)
    {
        await _realtimeNotifier.PushScanStatusAsync(scan.PublicId, scan.Status.ToString(), cancellationToken);
    }
}
