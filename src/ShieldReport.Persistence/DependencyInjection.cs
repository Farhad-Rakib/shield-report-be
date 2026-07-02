using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShieldReport.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using ShieldReport.Persistence.Context;
using ShieldReport.Persistence.Repositories;
using ShieldReport.Persistence.Seeding;
using ShieldReport.Application.Menu;
using ShieldReport.Application.Dashboard;

namespace ShieldReport.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            var provider = GetProvider(configuration);
            if (provider == "sqlserver")
            {
                options.UseSqlServer(GetConnectionString(configuration, "SqlServerConnection"));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider '{provider}'. Supported values: postgres, sqlserver.");
            }
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IRegistrationInviteRepository, RegistrationInviteRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IMfaRecoveryCodeRepository, MfaRecoveryCodeRepository>();
        services.AddScoped<ISiteSettingRepository, SiteSettingRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IRbacSeeder, RbacSeeder>();
        services.AddScoped<IDatabaseBootstrapper, EfCoreDatabaseBootstrapper>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IDashboardLayoutRepository, DashboardLayoutRepository>();
        services.AddScoped<IEngagementRepository, EngagementRepository>();
        services.AddScoped<IEngagementTaskRepository, EngagementTaskRepository>();
        services.AddScoped<IClientAssetRepository, ClientAssetRepository>();
        services.AddScoped<IClientOrganizationRepository, ClientOrganizationRepository>();
        services.AddScoped<IScanRepository, ScanRepository>();
        services.AddScoped<IScanFindingRawRepository, ScanFindingRawRepository>();
        services.AddScoped<IVulnerabilityRepository, VulnerabilityRepository>();
        services.AddScoped<ICvssSeverityBandRepository, CvssSeverityBandRepository>();
        services.AddScoped<IVulnerabilityAttachmentRepository, VulnerabilityAttachmentRepository>();
        services.AddScoped<IVulnerabilityRemarkRepository, VulnerabilityRemarkRepository>();
        services.AddScoped<IRetestRequestRepository, RetestRequestRepository>();

        return services;
    }

    private static string GetProvider(IConfiguration configuration)
    {
        var provider = (configuration["Database:Provider"] ?? "postgres").ToLowerInvariant();
        if (provider is not ("postgres" or "sqlserver"))
        {
            throw new InvalidOperationException($"Unsupported database provider '{provider}'. Supported values: postgres, sqlserver.");
        }

        return provider;
    }

    private static string GetConnectionString(IConfiguration configuration, string key)
    {
        return configuration.GetConnectionString(key)
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException($"Missing connection string: {key}.");
    }
}
