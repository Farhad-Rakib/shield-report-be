using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Infrastructure.Authentication;
using ShieldReport.Infrastructure.Email;
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

        return services;
    }
}
