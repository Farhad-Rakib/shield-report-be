using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.Common.Interfaces;

namespace ShieldReport.Infrastructure.BackgroundJobs;

// Deletes audit rows older than Audit:RetentionDays (default 365) once a day.
public sealed class AuditLogRetentionService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuditLogRetentionService> _logger;

    public AuditLogRetentionService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<AuditLogRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) finish first.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var days = int.TryParse(_configuration["Audit:RetentionDays"], out var configured) && configured > 0 ? configured : 365;
                using var scope = _scopeFactory.CreateScope();
                var purged = await scope.ServiceProvider.GetRequiredService<IAuditLogRepository>()
                    .PurgeOlderThanAsync(DateTime.UtcNow.AddDays(-days), stoppingToken);
                if (purged > 0)
                {
                    _logger.LogInformation("Purged {Count} audit log row(s) older than {Days} days.", purged, days);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log retention run failed.");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
