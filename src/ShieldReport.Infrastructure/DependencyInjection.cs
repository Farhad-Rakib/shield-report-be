using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Infrastructure.Authentication;
using ShieldReport.Infrastructure.Email;
using ShieldReport.Infrastructure.Reporting;
using ShieldReport.Infrastructure.Security;

namespace ShieldReport.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddScoped<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddScoped<IRegistrationInviteTokenGenerator, RegistrationInviteTokenGenerator>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddDataProtection();
        services.AddSingleton<IApiKeyEncryptor, DataProtectionApiKeyEncryptor>();
        // Scoped, not Singleton — it now depends on IFileStorageService (Scoped, needs
        // IWebHostEnvironment) to load a custom-uploaded template's bytes.
        services.AddScoped<IReportDocxTemplateFiller, OpenXmlReportDocxTemplateFiller>();
        services.AddScoped<IReportPdfConverter, LibreOfficeReportPdfConverter>();
        // HTML report engine (PDF via headless Chromium + paged.js, Word via HtmlToOpenXml).
        services.AddSingleton<ShieldReport.Infrastructure.Reporting.Html.ChromiumBrowserProvider>();
        services.AddSingleton<ShieldReport.Application.Reports.Rendering.IHtmlReportRenderer, ShieldReport.Infrastructure.Reporting.Html.HtmlReportRenderer>();

        // Durable background jobs (replaces Hangfire) — see BackgroundJobs/BackgroundJobWorkerService.
        services.AddHostedService<ShieldReport.Infrastructure.BackgroundJobs.BackgroundJobWorkerService>();
        services.AddHostedService<ShieldReport.Infrastructure.BackgroundJobs.AuditLogRetentionService>();

        return services;
    }
}
