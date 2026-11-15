using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShieldReport.Application.Auth;
using ShieldReport.Application.BackgroundJobs;
using ShieldReport.Application.ClientAssets;
using ShieldReport.Application.ClientOrganizations;
using ShieldReport.Application.Dashboard;
using ShieldReport.Application.Engagements;
using ShieldReport.Application.LlmProviderConfigurations;
using ShieldReport.Application.Menu;
using ShieldReport.Application.Notifications;
using ShieldReport.Application.Permissions;
using ShieldReport.Application.RegistrationInvites;
using ShieldReport.Application.ReportTemplates;
using ShieldReport.Application.Reports;
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
        services.AddScoped<ShieldReport.Application.FindingsReport.IFindingsReportService, ShieldReport.Application.FindingsReport.FindingsReportService>();
        services.AddScoped<ShieldReport.Application.FindingsReport.IFindingsReportExportService, ShieldReport.Application.FindingsReport.FindingsReportExportService>();
        services.AddScoped<IRegistrationInviteService, RegistrationInviteService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEngagementService, EngagementService>();
        services.AddScoped<IEngagementTaskService, EngagementTaskService>();
        services.AddScoped<IClientAssetService, ClientAssetService>();
        services.AddScoped<IClientOrganizationService, ClientOrganizationService>();
        services.AddScoped<IScanQueueService, ScanQueueService>();
        services.AddScoped<IBackgroundJobQueue, BackgroundJobQueue>();
        services.AddScoped<ShieldReport.Application.Audit.IAuditLogger, ShieldReport.Application.Audit.AuditLogger>();
        services.AddScoped<ShieldReport.Application.ScanAuthorizations.IScanAuthorizationService, ShieldReport.Application.ScanAuthorizations.ScanAuthorizationService>();
        services.AddScoped<ShieldReport.Application.ScanTools.IScanToolSettingsService, ShieldReport.Application.ScanTools.ScanToolSettingsService>();
        services.AddScoped<ShieldReport.Application.Reports.Delivery.IReportDeliveryService, ShieldReport.Application.Reports.Delivery.ReportDeliveryService>();
        services.AddScoped<IBackgroundJobHandler, ShieldReport.Application.Reports.Delivery.ReportEmailJobHandler>();
        services.AddScoped<IBackgroundJobHandler, LlmExplanationJobHandler>();
        services.AddScoped<IBackgroundJobHandler, ShieldReport.Application.Reports.ReportNarrativeJobHandler>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IVulnerabilityDedupService, VulnerabilityDedupService>();
        services.AddScoped<ICvssSeverityMappingService, CvssSeverityMappingService>();
        services.AddScoped<IVulnerabilityService, VulnerabilityService>();
        services.AddScoped<IVulnerabilityAiExplanationService, VulnerabilityAiExplanationService>();
        services.AddScoped<IVulnerabilityAttachmentService, VulnerabilityAttachmentService>();
        services.AddScoped<IVulnerabilityRemarkService, VulnerabilityRemarkService>();
        services.AddScoped<IRetestRequestService, RetestRequestService>();
        services.AddScoped<ILlmProviderConfigurationService, LlmProviderConfigurationService>();
        services.AddScoped<IReportTemplateService, ReportTemplateService>();
        services.AddScoped<IReportNarrativeDraftingService, ReportNarrativeDraftingService>();
        services.AddScoped<ShieldReport.Application.CustomScanTools.ICustomScanToolService, ShieldReport.Application.CustomScanTools.CustomScanToolService>();
        services.AddScoped<ShieldReport.Application.LlmPrompts.ILlmPromptService, ShieldReport.Application.LlmPrompts.LlmPromptService>();
        services.AddScoped<ShieldReport.Application.LlmProviderCatalog.ILlmProviderCatalogService, ShieldReport.Application.LlmProviderCatalog.LlmProviderCatalogService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
