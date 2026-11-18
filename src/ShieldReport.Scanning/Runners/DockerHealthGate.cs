using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ShieldReport.Scanning.Runners;

// Decides whether the scan workers may start new scans. When the Docker daemon is slow or not
// answering, starting more containers makes it worse (the 2026-11-16 reconftw load test hung
// Docker Desktop with 10 DNS-heavy scans), so new claims wait until it recovers. Running scans
// are not touched. One `docker version` probe at most every CacheFor, shared by all worker loops.
public sealed class DockerHealthGate
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(15);

    private readonly ILogger<DockerHealthGate> _logger;
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private bool _healthy = true;
    private DateTime _checkedAt = DateTime.MinValue;

    public DockerHealthGate(ILogger<DockerHealthGate> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CanStartScansAsync(CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _checkedAt < CacheFor)
        {
            return _healthy;
        }

        await _probeLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTime.UtcNow - _checkedAt < CacheFor)
            {
                return _healthy;
            }

            var stopwatch = Stopwatch.StartNew();
            bool healthy;
            string detail;
            try
            {
                var (exitCode, _, stderr) = await DockerScanRunner.RunDockerCommandAsync(["version", "--format", "{{.Server.Version}}"], SlowThreshold);
                healthy = exitCode == 0;
                detail = healthy ? $"answered in {stopwatch.ElapsedMilliseconds} ms" : $"exit {exitCode}: {stderr.Trim()}";
            }
            catch (TimeoutException)
            {
                healthy = false;
                detail = $"no answer within {SlowThreshold.TotalSeconds:0} s";
            }
            catch (Exception ex)
            {
                healthy = false;
                detail = ex.Message;
            }

            if (healthy != _healthy)
            {
                if (healthy)
                {
                    _logger.LogInformation("Docker is responsive again ({Detail}); scan workers resume starting scans.", detail);
                }
                else
                {
                    _logger.LogWarning("Docker is slow or unavailable ({Detail}); scan workers stop starting new scans until it recovers.", detail);
                }
            }

            _healthy = healthy;
            _checkedAt = DateTime.UtcNow;
            return healthy;
        }
        finally
        {
            _probeLock.Release();
        }
    }
}
