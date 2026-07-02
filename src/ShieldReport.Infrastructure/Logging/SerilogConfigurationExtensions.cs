using Microsoft.Extensions.Configuration;
using Serilog;

namespace ShieldReport.Infrastructure.Logging;

public static class SerilogConfigurationExtensions
{
    public static LoggerConfiguration AddApplicationSinks(this LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        var configuredLogger = loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day);


        return configuredLogger;
    }
}
