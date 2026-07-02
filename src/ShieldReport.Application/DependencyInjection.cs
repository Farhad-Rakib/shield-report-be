using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShieldReport.Application.Auth;
using ShieldReport.Application.ClientAssets;
using ShieldReport.Application.ClientOrganizations;
using ShieldReport.Application.Dashboard;
using ShieldReport.Application.Engagements;
using ShieldReport.Application.Menu;
using ShieldReport.Application.Notifications;
using ShieldReport.Application.Permissions;
using ShieldReport.Application.RegistrationInvites;
using ShieldReport.Application.RetestRequests;
using ShieldReport.Application.Roles;
using ShieldReport.Application.Scans;
using ShieldReport.Application.SiteSettings;
using ShieldReport.Application.Users;
using ShieldReport.Application.Vulnerabilities;

namespace ShieldReport.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ISiteSettingService, SiteSettingService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDashboardLayoutService, DashboardLayoutService>();
        services.AddScoped<IRegistrationInviteService, RegistrationInviteService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEngagementService, EngagementService>();
        services.AddScoped<IEngagementTaskService, EngagementTaskService>();
        services.AddScoped<IClientAssetService, ClientAssetService>();
        services.AddScoped<IClientOrganizationService, ClientOrganizationService>();
        services.AddScoped<IScanQueueService, ScanQueueService>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IVulnerabilityDedupService, VulnerabilityDedupService>();
        services.AddScoped<ICvssSeverityMappingService, CvssSeverityMappingService>();
        services.AddScoped<IVulnerabilityService, VulnerabilityService>();
        services.AddScoped<IVulnerabilityAttachmentService, VulnerabilityAttachmentService>();
        services.AddScoped<IVulnerabilityRemarkService, VulnerabilityRemarkService>();
        services.AddScoped<IRetestRequestService, RetestRequestService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
