using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Enums;
using ShieldReport.Scanning.Configuration;
using ShieldReport.Scanning.Runners;

namespace ShieldReport.Scanning.Workers;

// Scan containers run detached on the host's Docker daemon and are only removed once the worker
// has recorded their result, so anything that slips through (a user Stop while no process was
// following the scan, a crash between saving and cleanup, a cancelled scan whose container was
// re-created) would otherwise linger. At startup and then every SweepInterval this removes
// shieldreport-scan-{id} containers and reconftw output folders whose scan is not Running.
// Running scans are left alone: they are either being followed or about to be re-attached.
public sealed class ScanContainerJanitorService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private const string ContainerPrefix = "shieldreport-scan-";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScanToolSettingsProvider _settingsProvider;
    private readonly ILogger<ScanContainerJanitorService> _logger;

    public ScanContainerJanitorService(IServiceScopeFactory scopeFactory, ScanToolSettingsProvider settingsProvider, ILogger<ScanContainerJanitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepContainersAsync(stoppingToken);
            SweepReconftwOutputFolders();

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SweepContainersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var list = await DockerScanRunner.RunDockerCommandAsync(["ps", "-a", "--filter", $"name=^{ContainerPrefix}", "--format", "{{.Names}}"], TimeSpan.FromSeconds(30));
            if (list.ExitCode != 0)
            {
                _logger.LogDebug("Janitor: docker ps failed: {Error}", list.StdErr);
                return;
            }

            var removed = 0;
            foreach (var name in list.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!long.TryParse(name[ContainerPrefix.Length..], out var scanId) || await IsRunningAsync(scanId, cancellationToken))
                {
                    continue;
                }

                await DockerScanRunner.RunDockerCommandAsync(["rm", "-f", name], TimeSpan.FromSeconds(30));
                removed++;
            }

            if (removed > 0)
            {
                _logger.LogInformation("Janitor: removed {Count} leftover scan container(s).", removed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // Docker CLI unavailable (e.g. local dev without Docker) — nothing to sweep.
            _logger.LogDebug(ex, "Janitor: container sweep skipped.");
        }
    }

    private void SweepReconftwOutputFolders()
    {
        try
        {
            var root = _settingsProvider.ReconftwOutputPath;
            if (!Directory.Exists(root))
            {
                return;
            }

            foreach (var directory in Directory.EnumerateDirectories(root, "scan-*"))
            {
                if (!long.TryParse(Path.GetFileName(directory)["scan-".Length..], out var scanId)
                    || IsRunningAsync(scanId, CancellationToken.None).GetAwaiter().GetResult())
                {
                    continue;
                }

                Directory.Delete(directory, recursive: true);
            }

            // Per-scan reconftw config files (scan-{id}.cfg) next to the output folders.
            foreach (var file in Directory.EnumerateFiles(root, "scan-*.cfg"))
            {
                if (long.TryParse(Path.GetFileNameWithoutExtension(file)["scan-".Length..], out var scanId)
                    && !IsRunningAsync(scanId, CancellationToken.None).GetAwaiter().GetResult())
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Janitor: output folder sweep skipped.");
        }
    }

    private async Task<bool> IsRunningAsync(long scanId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var scan = await scope.ServiceProvider.GetRequiredService<IScanRepository>().GetByIdAsync(scanId, cancellationToken);
        return scan?.Status == ScanStatus.Running;
    }
}
