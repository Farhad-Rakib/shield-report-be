using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShieldReport.Api.Startup;

// Readiness checks behind /health/ready (tagged "ready"); /health stays a dependency-free
// liveness probe used by the container HEALTHCHECK, so a database blip doesn't get the API
// container restarted in a loop. Point an uptime monitor at /health/ready.

public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public SqlServerHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_configuration.GetConnectionString("SqlServerConnection"));
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable.", ex);
        }
    }
}

// Scans need the host Docker daemon (DockerScanRunner). Degraded rather than Unhealthy: the rest
// of the app (reports, vulnerabilities, portal) still works without it.
public sealed class DockerDaemonHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo("docker", ["version", "--format", "{{.Server.Version}}"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            return process.ExitCode == 0
                ? HealthCheckResult.Healthy($"Docker {(await output).Trim()}")
                : HealthCheckResult.Degraded("Docker daemon not reachable: scans cannot run.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Docker CLI/daemon unavailable: scans cannot run.", ex);
        }
    }
}

public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    private const long DegradedBelowBytes = 5L * 1024 * 1024 * 1024;   // 5 GB
    private const long UnhealthyBelowBytes = 1L * 1024 * 1024 * 1024;  // 1 GB

    private readonly IWebHostEnvironment _environment;

    public DiskSpaceHealthCheck(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(_environment.ContentRootPath))!);
        var freeGb = drive.AvailableFreeSpace / 1024d / 1024 / 1024;
        var description = $"{freeGb:F1} GB free";
        var data = new Dictionary<string, object> { ["freeBytes"] = drive.AvailableFreeSpace };

        return Task.FromResult(drive.AvailableFreeSpace switch
        {
            < UnhealthyBelowBytes => HealthCheckResult.Unhealthy(description, data: data),
            < DegradedBelowBytes => HealthCheckResult.Degraded(description, data: data),
            _ => HealthCheckResult.Healthy(description, data: data)
        });
    }
}

public static class ReadinessHealthChecks
{
    public const string Tag = "ready";

    public static IHealthChecksBuilder AddReadinessChecks(this IHealthChecksBuilder builder) => builder
        .AddCheck<SqlServerHealthCheck>("database", tags: [Tag])
        .AddCheck<DockerDaemonHealthCheck>("docker", tags: [Tag])
        .AddCheck<DiskSpaceHealthCheck>("disk", tags: [Tag]);

    // Compact JSON: overall status plus one entry per check. Exception details are deliberately
    // not included — the endpoint is anonymous.
    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), description = e.Value.Description, ms = (int)e.Value.Duration.TotalMilliseconds })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
