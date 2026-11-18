using Microsoft.Extensions.DependencyInjection;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Scanning.Configuration;
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
        services.AddSingleton<ScanToolSettingsProvider>();
        services.AddSingleton<ShieldReport.Application.ScanTools.IScanToolDeploymentSettings>(sp => sp.GetRequiredService<ScanToolSettingsProvider>());
        services.AddSingleton<ShieldReport.Application.ScanTools.IScanToolDiagnostics, DockerScanToolDiagnostics>();
        services.AddSingleton<ScanQueueSignal>();
        services.AddSingleton<IScanCancellationRegistry, ScanCancellationRegistry>();
        services.AddScoped<IScanRunner, DockerScanRunner>();
        services.AddSingleton<ShieldReport.Scanning.Runners.DockerHealthGate>();
        services.AddScoped<IScanDispatcher, DatabaseScanDispatcher>();
        services.AddScoped<IScanOutputParser, NaabuOutputParser>();
        services.AddScoped<IScanOutputParser, NucleiOutputParser>();
        services.AddScoped<IScanOutputParser, ReconftwOutputParser>();
        services.AddScoped<IScanOutputParserFactory, ScanOutputParserFactory>();
        services.AddHostedService<ScanContainerJanitorService>();
        services.AddHostedService<ScanWorkerBackgroundService>();

        return services;
    }
}
