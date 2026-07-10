using Microsoft.Extensions.DependencyInjection;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Scanning.Dispatching;
using ShieldReport.Scanning.Parsing;
using ShieldReport.Scanning.Queueing;
using ShieldReport.Scanning.Runners;
using ShieldReport.Scanning.Workers;

namespace ShieldReport.Scanning;

public static class DependencyInjection
{
    public static IServiceCollection AddScanning(this IServiceCollection services)
    {
        services.AddSingleton<IScanWorkQueue, PerToolScanWorkQueue>();
        services.AddSingleton<IScanCancellationRegistry, ScanCancellationRegistry>();
        services.AddScoped<IScanRunner, DockerScanRunner>();
        services.AddScoped<IScanDispatcher, HangfireScanDispatcher>();
        services.AddScoped<ScanDispatchJob>();
        services.AddScoped<IScanOutputParser, NaabuOutputParser>();
        services.AddScoped<IScanOutputParser, NucleiOutputParser>();
        services.AddScoped<IScanOutputParser, ReconftwOutputParser>();
        services.AddScoped<IScanOutputParserFactory, ScanOutputParserFactory>();
        services.AddHostedService<ScanWorkerBackgroundService>();

        return services;
    }
}
