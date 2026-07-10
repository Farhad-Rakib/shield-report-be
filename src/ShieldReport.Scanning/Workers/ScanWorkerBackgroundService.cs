using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Scans;
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
        var scanQueueService = scope.ServiceProvider.GetRequiredService<IScanQueueService>();

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
            await PushStatusAsync(scan, cancellationToken);
            await EnqueueNextInChainAsync(scan, scanRepository, scanQueueService, cancellationToken);
            return;
        }

        scan.Start();
        scanRepository.Update(scan);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await PushStatusAsync(scan, cancellationToken);

        // Resolved up front (not just after the run, as before) so the live per-line finding
        // parsing below can reuse it — and so the "starting" phase message can go out immediately,
        // independent of whatever the tool itself does or doesn't print.
        var parser = parserFactory.GetParser(scan.Tool);
        await _realtimeNotifier.PushScanPhaseAsync(scan.PublicId, $"Starting {scan.Tool} scan against {asset.Identifier}...", cancellationToken);

        try
        {
            var result = await scanRunner.RunAsync(scan, asset, (line, stream) => PushOutputLineAsync(scan, line, stream, parser, cancellationToken), cancellationToken);

            if (!result.Success)
            {
                scan.Fail(result.ErrorMessage ?? "Scan failed.", result.RawOutput);
                scanRepository.Update(scan);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await _realtimeNotifier.PushScanPhaseAsync(scan.PublicId, $"{scan.Tool} failed: {scan.ErrorMessage}", cancellationToken);
                await PushStatusAsync(scan, cancellationToken);
                await EnqueueNextInChainAsync(scan, scanRepository, scanQueueService, cancellationToken);
                return;
            }

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
            await _realtimeNotifier.PushScanPhaseAsync(
                scan.PublicId,
                parsedFindings.Count > 0 ? $"{scan.Tool} finished — {parsedFindings.Count} finding(s)." : $"{scan.Tool} finished — no findings.",
                cancellationToken);
            await PushStatusAsync(scan, cancellationToken);
            await EnqueueNextInChainAsync(scan, scanRepository, scanQueueService, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanId} failed unexpectedly.", scanId);
            scan.Fail($"Unexpected error: {ex.Message}");
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await _realtimeNotifier.PushScanPhaseAsync(scan.PublicId, $"{scan.Tool} failed: {ex.Message}", cancellationToken);
            await PushStatusAsync(scan, cancellationToken);
            await EnqueueNextInChainAsync(scan, scanRepository, scanQueueService, cancellationToken);
        }
    }

    // A tool failing (e.g. Reconftw's known amd64 manifest gap) shouldn't strand the rest of
    // the chain — Nuclei/Reconftw still get their turn even if Naabu errored out.
    private static async Task EnqueueNextInChainAsync(Scan scan, IScanRepository scanRepository, IScanQueueService scanQueueService, CancellationToken cancellationToken)
    {
        if (!scan.NextScanId.HasValue)
        {
            return;
        }

        var nextScan = await scanRepository.GetByIdAsync(scan.NextScanId.Value, cancellationToken);
        if (nextScan is null || nextScan.Status != ScanStatus.Queued)
        {
            return;
        }

        scanQueueService.Enqueue(nextScan.Id, nextScan.Tool);
    }

    private async Task PushOutputLineAsync(Scan scan, string line, string stream, IScanOutputParser parser, CancellationToken cancellationToken)
    {
        await _realtimeNotifier.PushScanOutputAsync(scan.PublicId, line, stream, cancellationToken);

        if (stream == "stderr")
        {
            // Nuclei's -stats output (also stderr) already computes a real completion
            // percentage — forward it as-is instead of estimating one ourselves.
            await TryPushProgressAsync(scan, line, cancellationToken);
            return;
        }

        // Best-effort live finding parse — the parsers already split on '\n' and skip
        // malformed/non-matching lines internally, so re-using them per single streamed line is
        // just a normal call, not a special "line mode". Never let a parse hiccup break the
        // stream; the authoritative parse of the full RawOutput still runs after the scan
        // completes in ProcessScanAsync, so a live-parse miss here has no lasting effect.
        try
        {
            foreach (var finding in parser.Parse(line))
            {
                await _realtimeNotifier.PushScanFindingAsync(scan.PublicId, finding.Title, finding.SeverityRaw, finding.Endpoint, cancellationToken);
            }
        }
        catch
        {
            // Live-only convenience — swallow and let the post-scan parse be authoritative.
        }
    }

    // Nuclei's -stats-interval JSON looks like:
    // {"duration":"0:00:05","errors":"2","hosts":"1","matched":"0","percent":"2",
    //  "requests":"498","rps":"94","startedAt":"...","templates":"10447","total":"18274"}
    // — every value is a string, and "percent" is unique to this stats shape (findings and
    // Naabu's port records never have it), so its presence alone is a safe, cheap discriminator.
    private async Task TryPushProgressAsync(Scan scan, string line, CancellationToken cancellationToken)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed[0] != '{')
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            if (!root.TryGetProperty("percent", out var percentElement) || !int.TryParse(percentElement.GetString(), out var percent))
            {
                return;
            }

            await _realtimeNotifier.PushScanProgressAsync(
                scan.PublicId,
                percent,
                ReadLong(root, "requests"),
                ReadLong(root, "rps"),
                ReadLong(root, "matched"),
                ReadLong(root, "total"),
                cancellationToken);
        }
        catch (JsonException)
        {
            // Not a stats line — nothing to do.
        }
    }

    private static long? ReadLong(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && long.TryParse(element.GetString(), out var value)
            ? value
            : null;
    }

    private async Task PushStatusAsync(Scan scan, CancellationToken cancellationToken)
    {
        await _realtimeNotifier.PushScanStatusAsync(scan.PublicId, scan.Status.ToString(), cancellationToken);
    }
}
