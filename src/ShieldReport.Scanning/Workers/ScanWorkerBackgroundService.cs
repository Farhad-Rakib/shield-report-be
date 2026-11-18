using ShieldReport.Scanning.Runners;
using System.Collections.Concurrent;
using ShieldReport.Scanning.Parsing;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.BackgroundJobs;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.ScanTools;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Application.Vulnerabilities;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Scanning.Queueing;

namespace ShieldReport.Scanning.Workers;

// Database-backed scan queue worker (replaces Hangfire + the in-memory channel).
//
// One loop per ScanTool, each handling one scan at a time (so scans of different tools run in
// parallel, scans of the same tool queue up). A loop claims work with an atomic lease
// (IScanRepository.TryClaimNextAsync): either the next Queued scan whose chain predecessor has
// finished, or a Running scan whose lease expired because the process that owned it died. While
// working, the owner renews the lease every LeaseRenewInterval.
//
// Scan containers run detached (DockerScanRunner), so:
//   - on API shutdown the worker only stops *following* the container and releases its lease; the
//     container keeps scanning and the next process re-attaches to it and collects the result;
//   - if the host itself rebooted the container is gone, and the scan is failed as interrupted;
//   - user Stop and the per-tool timeout stop the container.
// Final status writes use CancellationToken.None so a shutdown can never half-apply them.
public sealed class ScanWorkerBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LeaseRenewInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanRealtimeNotifier _realtimeNotifier;
    private readonly IScanCancellationRegistry _cancellationRegistry;
    private readonly ScanQueueSignal _signal;
    private readonly DockerHealthGate _dockerHealth;
    private readonly ILogger<ScanWorkerBackgroundService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}"[..^24];

    public ScanWorkerBackgroundService(
        IServiceScopeFactory scopeFactory,
        IScanRealtimeNotifier realtimeNotifier,
        IScanCancellationRegistry cancellationRegistry,
        ScanQueueSignal signal,
        DockerHealthGate dockerHealth,
        ILogger<ScanWorkerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _realtimeNotifier = realtimeNotifier;
        _cancellationRegistry = cancellationRegistry;
        _signal = signal;
        _dockerHealth = dockerHealth;
        _logger = logger;
    }

    // Upper bound for the Scan Tools page's "max parallel" (UpdateScanToolSettingRequestDtoValidator).
    private const int MaxLoopsPerTool = ScanLimits.MaxParallelPerTool;
    private static readonly TimeSpan MaxParallelRefresh = TimeSpan.FromSeconds(15);

    // One MaxParallel read per tool per refresh interval, shared by all of that tool's slot loops.
    private readonly ConcurrentDictionary<ScanTool, (int Value, DateTime ReadAt)> _maxParallelCache = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var toolLoops = Enum.GetValues<ScanTool>()
            .SelectMany(tool => Enumerable.Range(0, MaxLoopsPerTool).Select(slot => RunToolLoopAsync(tool, slot, stoppingToken)));
        await Task.WhenAll(toolLoops);
    }

    private async Task<int> GetMaxParallelAsync(ScanTool tool, CancellationToken stoppingToken)
    {
        if (_maxParallelCache.TryGetValue(tool, out var cached) && DateTime.UtcNow - cached.ReadAt < MaxParallelRefresh)
        {
            return cached.Value;
        }

        using var scope = _scopeFactory.CreateScope();
        var value = await scope.ServiceProvider.GetRequiredService<IScanToolSettingsService>().GetMaxParallelAsync(tool, stoppingToken);
        _maxParallelCache[tool] = (value, DateTime.UtcNow);
        return value;
    }

    // `slot` 0..MaxLoopsPerTool-1: a loop only claims work while slot < the tool's current
    // MaxParallel setting (re-read every MaxParallelRefresh), so parallelism follows the Scan
    // Tools page without a restart. An idle slot just waits.
    private async Task RunToolLoopAsync(ScanTool tool, int slot, CancellationToken stoppingToken)
    {
        var leaseOwner = $"{_workerId}:{tool}:{slot}";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (slot > 0)
                {
                    var maxParallel = await GetMaxParallelAsync(tool, stoppingToken);
                    if (slot >= maxParallel)
                    {
                        await Task.Delay(MaxParallelRefresh, stoppingToken);
                        continue;
                    }
                }

                // Don't add containers while Docker is struggling; queued scans wait instead.
                if (!await _dockerHealth.CanStartScansAsync(stoppingToken))
                {
                    await Task.Delay(MaxParallelRefresh, stoppingToken);
                    continue;
                }

                long? scanId;
                using (var scope = _scopeFactory.CreateScope())
                {
                    scanId = await scope.ServiceProvider.GetRequiredService<IScanRepository>()
                        .TryClaimNextAsync(tool, leaseOwner, LeaseDuration, stoppingToken);
                }

                if (scanId is null)
                {
                    await _signal.WaitAsync(PollInterval, stoppingToken);
                    continue;
                }

                await ProcessClaimedScanAsync(scanId.Value, leaseOwner, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Database unavailable etc. — back off and keep the loop alive. Any scan claimed in
                // this iteration keeps its lease until it expires and is then re-claimed.
                _logger.LogError(ex, "Scan worker loop for {Tool} failed; retrying.", tool);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private const string InterruptedMessage = "Interrupted: the scan container no longer exists (host restart or manual removal). Please run the scan again.";

    private async Task ProcessClaimedScanAsync(long scanId, string leaseOwner, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var scanRepository = scope.ServiceProvider.GetRequiredService<IScanRepository>();
        var clientAssetRepository = scope.ServiceProvider.GetRequiredService<IClientAssetRepository>();
        var scanRunner = scope.ServiceProvider.GetRequiredService<IScanRunner>();
        var parserFactory = scope.ServiceProvider.GetRequiredService<IScanOutputParserFactory>();
        var dedupService = scope.ServiceProvider.GetRequiredService<IVulnerabilityDedupService>();
        var findingRepository = scope.ServiceProvider.GetRequiredService<IScanFindingRawRepository>();
        var jobQueue = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var scan = await scanRepository.GetByIdWithDetailsAsync(scanId, CancellationToken.None);
        if (scan is null || (scan.Status != ScanStatus.Queued && scan.Status != ScanStatus.Running))
        {
            return;
        }

        async Task FinishFailedAsync(string message, string? rawOutput)
        {
            scan.Fail(message, rawOutput);
            scan.ReleaseLease();
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await SafePushPhaseAsync(scan, $"{scan.Tool} failed: {message}");
            await SafePushStatusAsync(scan);
            _signal.Notify(); // let the next chain step (another tool's loop) start right away
        }

        var reattach = scan.Status == ScanStatus.Running;

        var asset = await clientAssetRepository.GetByIdWithDetailsAsync(scan.ClientAssetId, CancellationToken.None);
        if (asset is null)
        {
            await FinishFailedAsync("Client asset no longer exists.", null);
            return;
        }

        if (!reattach)
        {
            // Re-check at start: the authorization may have been revoked or have expired while
            // the scan sat in the queue (e.g. behind a long chain step).
            if (scan.ScanAuthorizationId.HasValue)
            {
                var authorization = await scope.ServiceProvider.GetRequiredService<IScanAuthorizationRepository>()
                    .GetByIdAsync(scan.ScanAuthorizationId.Value, CancellationToken.None);
                if (authorization is null || !authorization.IsActiveAt(DateTime.UtcNow) || !authorization.Allows(scan.Tool))
                {
                    await FinishFailedAsync("The scan authorization was revoked or expired before this scan started.", null);
                    return;
                }
            }

            scan.Start();
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await SafePushStatusAsync(scan);
        }

        // Settings snapshot taken when the scan was queued; older scans resolve current settings.
        var settings = DeserializeSettings(scan.ToolSettingsJson)
            ?? await scope.ServiceProvider.GetRequiredService<IScanToolSettingsService>().ResolveForScanAsync(scan.Tool, CancellationToken.None);

        // Custom tools: parser and tool name come from the snapshot (the client label, never the
        // real tool name, is what gets recorded on findings).
        IScanOutputParser parser = scan.Tool == ScanTool.Custom && settings.Custom is { } customOptions
            ? new CustomToolOutputParser(customOptions, asset.Identifier)
            : parserFactory.GetParser(scan.Tool);
        var toolName = scan.Tool == ScanTool.Custom && settings.Custom is { } c ? c.ClientLabel : scan.Tool.ToString();

        await SafePushPhaseAsync(scan, reattach
            ? $"Re-attached to the running {toolName} scan after a server restart..."
            : $"Starting {toolName} scan against {asset.Identifier}...");
        // Deadline is anchored to the original start, so a re-attach doesn't reset the clock.
        var deadline = (scan.StartedAt ?? DateTime.UtcNow).AddMinutes(settings.TimeoutMinutes);
        var remaining = deadline - DateTime.UtcNow;

        // stop = user Stop (ScanService.CancelAsync via the registry) or timeout → container stopped.
        // detach = shutdown or lost lease → stop following only, container keeps running.
        var userStopCts = _cancellationRegistry.Register(scan.Id, CancellationToken.None);
        using var timeoutCts = new CancellationTokenSource(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        using var stopCts = CancellationTokenSource.CreateLinkedTokenSource(userStopCts.Token, timeoutCts.Token);
        using var leaseLostCts = new CancellationTokenSource();
        using var detachCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, leaseLostCts.Token);
        using var heartbeatCts = new CancellationTokenSource();
        var heartbeat = RenewLeaseUntilCancelledAsync(scan.Id, leaseOwner, leaseLostCts, heartbeatCts.Token);

        try
        {
            var result = await scanRunner.RunAsync(scan, asset, settings, reattach, (line, stream) => PushOutputLineAsync(scan, line, stream, parser), stopCts.Token, detachCts.Token);

            switch (result.Outcome)
            {
                case ScanRunOutcome.Detached:
                    if (leaseLostCts.IsCancellationRequested)
                    {
                        _logger.LogWarning("Lost the lease on scan {ScanId}; another worker will re-attach to it.", scanId);
                    }
                    else
                    {
                        _logger.LogInformation("Detached from scan {ScanId} for shutdown; its container keeps running and will be re-attached.", scanId);
                        await ReleaseLeaseAsync(scan.Id, leaseOwner);
                    }

                    return;

                case ScanRunOutcome.ContainerMissing:
                    _logger.LogWarning("Scan {ScanId} could not be re-attached: its container is gone.", scanId);
                    await FinishFailedAsync(InterruptedMessage, null);
                    return;

                case ScanRunOutcome.Stopped when timeoutCts.IsCancellationRequested && !userStopCts.IsCancellationRequested:
                    _logger.LogWarning("Scan {ScanId} hit its {Minutes}-minute timeout.", scanId, settings.TimeoutMinutes);
                    await FinishFailedAsync($"Timed out after {settings.TimeoutMinutes} minutes.", result.RawOutput);
                    await scanRunner.CleanupAsync(scan);
                    return;

                case ScanRunOutcome.Stopped:
                    // ScanService.CancelAsync already set this scan (and the rest of its chain) to
                    // Cancelled and pushed the status update — only the lease is left to clear.
                    _logger.LogInformation("Scan {ScanId} was stopped by user request.", scanId);
                    await ReleaseLeaseAsync(scan.Id, leaseOwner);
                    return;

                case ScanRunOutcome.Failed:
                    await FinishFailedAsync(result.ErrorMessage ?? "Scan failed.", result.RawOutput);
                    await scanRunner.CleanupAsync(scan);
                    return;
            }

            // Completed. The dedup service saves each vulnerability as it goes; if the process dies
            // part-way, the scan stays Running, gets re-claimed, re-attaches to the (exited, not yet
            // removed) container and parses again — dedup keys keep vulnerabilities unique.
            var parsedFindings = parser.Parse(result.StructuredOutput ?? result.RawOutput);
            foreach (var finding in parsedFindings)
            {
                var findingRaw = new ScanFindingRaw(scan.Id, JsonSerializer.Serialize(finding), finding.Title, finding.Endpoint, finding.SeverityRaw);
                await findingRepository.AddAsync(findingRaw, CancellationToken.None);

                var vulnerability = await dedupService.ProcessFindingAsync(
                    scan.ClientOrganizationId,
                    scan.ClientAssetId,
                    scan.Id,
                    scan.EngagementId,
                    scan.EngagementTaskId,
                    toolName,
                    finding,
                    CancellationToken.None);

                findingRaw.MarkProcessed(vulnerability.Id);
                findingRepository.Update(findingRaw);

                // Durable, best-effort enrichment (no-op when no LLM provider is active). Saved by
                // the next SaveChanges together with the finding it belongs to.
                await jobQueue.EnqueueAsync(LlmExplanationJobHandler.Type, new LlmExplanationJobPayload(vulnerability.Id, toolName), maxAttempts: 3, cancellationToken: CancellationToken.None);
            }

            scan.Complete(rawLogBlobKey: null, rawOutput: result.RawOutput);
            scan.ReleaseLease();
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await scanRunner.CleanupAsync(scan);

            await SafePushPhaseAsync(
                scan,
                parsedFindings.Count > 0 ? $"{scan.Tool} finished — {parsedFindings.Count} finding(s)." : $"{scan.Tool} finished — no findings.");
            await SafePushStatusAsync(scan);
            _signal.Notify();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanId} failed unexpectedly.", scanId);
            await TryFinishFailedInFreshScopeAsync(scanId, $"Unexpected error: {ex.Message}");
        }
        finally
        {
            heartbeatCts.Cancel();
            await heartbeat;
            _cancellationRegistry.Unregister(scan.Id);
        }
    }

    private static ScanToolRuntimeSettings? DeserializeSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScanToolRuntimeSettings>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Keeps the lease alive while the scan runs. If renewal fails for longer than the lease
    // (database unreachable) ownership may pass to another worker, so signal a detach instead of
    // risking two processes following — and later double-recording — the same scan.
    private async Task RenewLeaseUntilCancelledAsync(long scanId, string leaseOwner, CancellationTokenSource leaseLostCts, CancellationToken cancellationToken)
    {
        var lastRenewed = DateTime.UtcNow;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LeaseRenewInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var renewed = await scope.ServiceProvider.GetRequiredService<IScanRepository>()
                    .RenewLeaseAsync(scanId, leaseOwner, LeaseDuration, CancellationToken.None);
                if (!renewed)
                {
                    leaseLostCts.Cancel();
                    return;
                }

                lastRenewed = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not renew the lease on scan {ScanId}.", scanId);
                if (DateTime.UtcNow - lastRenewed >= LeaseDuration)
                {
                    leaseLostCts.Cancel();
                    return;
                }
            }
        }
    }

    private async Task ReleaseLeaseAsync(long scanId, string leaseOwner)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IScanRepository>().ReleaseLeaseAsync(scanId, leaseOwner, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not release the lease on scan {ScanId}; it will expire on its own.", scanId);
        }
    }

    // Fresh scope: used when the original scope/DbContext may itself be the thing that failed.
    private async Task TryFinishFailedInFreshScopeAsync(long scanId, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scanRepository = scope.ServiceProvider.GetRequiredService<IScanRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var scan = await scanRepository.GetByIdAsync(scanId, CancellationToken.None);
            if (scan is null || (scan.Status != ScanStatus.Queued && scan.Status != ScanStatus.Running))
            {
                return;
            }

            scan.Fail(message);
            scan.ReleaseLease();
            scanRepository.Update(scan);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            await SafePushStatusAsync(scan);
            _signal.Notify();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not record failure for scan {ScanId}; its lease will expire and it will be re-claimed.", scanId);
        }
    }

    // Live console pushes are best-effort and must never be cancelled by (or throw because of)
    // shutdown; DockerScanRunner additionally guards the call site.
    private async Task PushOutputLineAsync(Scan scan, string line, string stream, IScanOutputParser parser)
    {
        await _realtimeNotifier.PushScanOutputAsync(scan.PublicId, line, stream, CancellationToken.None);

        if (stream == "stderr")
        {
            // Nuclei's -stats output (also stderr) already computes a real completion
            // percentage — forward it as-is instead of estimating one ourselves.
            await TryPushProgressAsync(scan, line);
            return;
        }

        // Best-effort live finding parse — the authoritative parse of the full output still runs
        // after the scan completes, so a live-parse miss here has no lasting effect.
        try
        {
            foreach (var finding in parser.Parse(line))
            {
                await _realtimeNotifier.PushScanFindingAsync(scan.PublicId, finding.Title, finding.SeverityRaw, finding.Endpoint, CancellationToken.None);
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
    private async Task TryPushProgressAsync(Scan scan, string line)
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
                CancellationToken.None);
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

    private async Task SafePushStatusAsync(Scan scan)
    {
        try
        {
            await _realtimeNotifier.PushScanStatusAsync(scan.PublicId, scan.Status.ToString(), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not push status for scan {ScanId}.", scan.Id);
        }
    }

    private async Task SafePushPhaseAsync(Scan scan, string message)
    {
        try
        {
            await _realtimeNotifier.PushScanPhaseAsync(scan.PublicId, message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not push phase message for scan {ScanId}.", scan.Id);
        }
    }
}
