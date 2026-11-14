using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.BackgroundJobs;
using ShieldReport.Application.Common.Interfaces;

namespace ShieldReport.Infrastructure.BackgroundJobs;

// Executes rows from the background_jobs table (replaces Hangfire's server). Each loop claims one
// due job at a time with a lease (BackgroundJobRepository.TryClaimNextAsync), runs the handler
// registered for its Type in a fresh DI scope, then records success or schedules a retry with
// exponential back-off (BackgroundJob.MarkAttemptFailed). A job whose worker died mid-run is
// re-claimed once its lease expires, so handlers must be idempotent.
public sealed class BackgroundJobWorkerService : BackgroundService
{
    private const int Concurrency = 2;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan SucceededRetention = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobWorkerService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}"[..^24];

    public BackgroundJobWorkerService(IServiceScopeFactory scopeFactory, ILogger<BackgroundJobWorkerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var loops = Enumerable.Range(0, Concurrency).Select(i => RunLoopAsync($"{_workerId}#{i}", stoppingToken)).ToList();
        loops.Add(RunPurgeLoopAsync(stoppingToken));
        await Task.WhenAll(loops);
    }

    private async Task RunLoopAsync(string leaseOwner, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await ClaimAsync(leaseOwner, stoppingToken);
                if (jobId is null)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                await RunJobAsync(jobId.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Database hiccup etc. — back off a little and keep the loop alive.
                _logger.LogError(ex, "Background job loop error.");
                await DelaySafeAsync(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task<long?> ClaimAsync(string leaseOwner, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
        return await repository.TryClaimNextAsync(leaseOwner, LeaseDuration, cancellationToken);
    }

    private async Task RunJobAsync(long jobId, CancellationToken stoppingToken)
    {
        // Bookkeeping scope: separate from the handler's scope so a handler that fails halfway can
        // never have its partial, tracked changes saved together with the job's status update.
        using var bookkeepingScope = _scopeFactory.CreateScope();
        var repository = bookkeepingScope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
        var unitOfWork = bookkeepingScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var job = await repository.GetByIdAsync(jobId, CancellationToken.None);
        if (job is null)
        {
            return;
        }

        try
        {
            using var handlerScope = _scopeFactory.CreateScope();
            var handler = handlerScope.ServiceProvider.GetServices<IBackgroundJobHandler>().FirstOrDefault(h => h.JobType == job.Type)
                ?? throw new InvalidOperationException($"No handler is registered for background job type '{job.Type}'.");

            await handler.HandleAsync(job.PayloadJson, stoppingToken);
            job.MarkSucceeded();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            job.MarkAttemptFailed("Interrupted by server shutdown.");
            _logger.LogInformation("Background job {JobId} ({Type}) interrupted by shutdown; it will be retried.", job.Id, job.Type);
        }
        catch (Exception ex)
        {
            job.MarkAttemptFailed(ex.Message);
            _logger.LogWarning(ex, "Background job {JobId} ({Type}) failed on attempt {Attempt}/{MaxAttempts}.", job.Id, job.Type, job.Attempts, job.MaxAttempts);
        }

        repository.Update(job);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    private async Task RunPurgeLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IBackgroundJobRepository>();
                var purged = await repository.PurgeSucceededAsync(DateTime.UtcNow - SucceededRetention, stoppingToken);
                if (purged > 0)
                {
                    _logger.LogInformation("Purged {Count} succeeded background job(s).", purged);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background job purge failed.");
            }

            await DelaySafeAsync(PurgeInterval, stoppingToken);
        }
    }

    private static async Task DelaySafeAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
