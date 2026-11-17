using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Context;

public sealed class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserDashboardPreference> UserDashboardPreferences => Set<UserDashboardPreference>();
    public DbSet<ClientOrganization> ClientOrganizations => Set<ClientOrganization>();
    public DbSet<MfaRecoveryCode> MfaRecoveryCodes => Set<MfaRecoveryCode>();
    public DbSet<CvssSeverityBand> CvssSeverityBands => Set<CvssSeverityBand>();
    public DbSet<ScanWorkerNode> ScanWorkerNodes => Set<ScanWorkerNode>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationDeliveryLog> NotificationDeliveryLogs => Set<NotificationDeliveryLog>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<RegistrationInvite> RegistrationInvites => Set<RegistrationInvite>();
    public DbSet<ClientAsset> ClientAssets => Set<ClientAsset>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<ScanFindingRaw> ScanFindingRaws => Set<ScanFindingRaw>();
    public DbSet<Vulnerability> Vulnerabilities => Set<Vulnerability>();
    public DbSet<VulnerabilityAttachment> VulnerabilityAttachments => Set<VulnerabilityAttachment>();
    public DbSet<VulnerabilityRemark> VulnerabilityRemarks => Set<VulnerabilityRemark>();
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<EngagementAssignee> EngagementAssignees => Set<EngagementAssignee>();
    public DbSet<EngagementTask> EngagementTasks => Set<EngagementTask>();
    public DbSet<EngagementTaskAsset> EngagementTaskAssets => Set<EngagementTaskAsset>();
    public DbSet<RetestRequest> RetestRequests => Set<RetestRequest>();
    public DbSet<RetestRequestCase> RetestRequestCases => Set<RetestRequestCase>();
    public DbSet<LlmProviderConfiguration> LlmProviderConfigurations => Set<LlmProviderConfiguration>();
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<ScanAuthorization> ScanAuthorizations => Set<ScanAuthorization>();
    public DbSet<ScanToolSetting> ScanToolSettings => Set<ScanToolSetting>();
    public DbSet<ScanQueueSetting> ScanQueueSettings => Set<ScanQueueSetting>();
    public DbSet<EmailSetting> EmailSettings => Set<EmailSetting>();
    public DbSet<CustomScanTool> CustomScanTools => Set<CustomScanTool>();
    public DbSet<LlmPrompt> LlmPrompts => Set<LlmPrompt>();
    public DbSet<ReportDelivery> ReportDeliveries => Set<ReportDelivery>();
    public DbSet<LlmProviderCatalogItem> LlmProviderCatalogItems => Set<LlmProviderCatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Two independent visibility rules, combined into ONE filter per entity (EF Core keeps only
        // the last HasQueryFilter call per type):
        //
        // 1. Client-portal isolation (unconditional, no role exception) — see
        //    DATABASE-DESIGN-PentestOps.md §5. Defense in depth on top of [ClientScope] at the
        //    controller level; this is the layer that can't be skipped by a forgotten attribute.
        //
        // 2. Staff project scoping (2026-11-15). A staff user without SuperAdmin or the
        //    "data.view-all" permission (ICurrentUserService.IsRestrictedToAssignedEngagements) sees
        //    only engagements they lead, are assigned to or created, plus what hangs off them:
        //    clients with such an engagement (or that they created), those clients' assets, and
        //    the scans, vulnerabilities, reports, tasks and scan authorizations of those
        //    engagements. Rows with no engagement (legacy scans/findings) follow their client.
        //    Background work has no user and is never restricted. Filters that reference another
        //    filtered set (Engagements, ClientOrganizations, ClientAssets) inherit its filter, so
        //    the chain stays consistent; there are no cycles (Engagement's rule only uses its own
        //    columns and EngagementAssignees).
        modelBuilder.Entity<Engagement>().HasQueryFilter(x =>
            (!_currentUserService.IsClientPortalUser || x.ClientOrganizationId == _currentUserService.ClientOrganizationId)
            && (!_currentUserService.IsRestrictedToAssignedEngagements
                || x.LeadPentesterId == _currentUserService.UserId
                || x.CreatedByUserId == _currentUserService.UserId
                || x.Assignees.Any(a => a.UserId == _currentUserService.UserId)));

        modelBuilder.Entity<ClientOrganization>().HasQueryFilter(x =>
            !_currentUserService.IsRestrictedToAssignedEngagements
            || x.CreatedBy == _currentUserService.UserId
            || Engagements.Any(e => e.ClientOrganizationId == x.Id));

        modelBuilder.Entity<ClientAsset>().HasQueryFilter(x =>
            (!_currentUserService.IsClientPortalUser || x.ClientOrganizationId == _currentUserService.ClientOrganizationId)
            && (!_currentUserService.IsRestrictedToAssignedEngagements
                || x.CreatedBy == _currentUserService.UserId
                || ClientOrganizations.Any(o => o.Id == x.ClientOrganizationId)));

        modelBuilder.Entity<Vulnerability>().HasQueryFilter(x =>
            (!_currentUserService.IsClientPortalUser || x.ClientOrganizationId == _currentUserService.ClientOrganizationId)
            && (!_currentUserService.IsRestrictedToAssignedEngagements
                || (x.EngagementId != null
                    ? Engagements.Any(e => e.Id == x.EngagementId)
                    : ClientOrganizations.Any(o => o.Id == x.ClientOrganizationId))));

        // Scan previously had no isolation at all (unlike its ClientAsset/Vulnerability/Engagement
        // siblings above) — ScanService.GetByPublicIdAsync did a bare PublicId lookup with zero
        // ownership check, so any client-portal user holding ScansRead could fetch any other
        // org's scan by GUID. Closed as part of exposing the scan console to the client portal.
        modelBuilder.Entity<Scan>().HasQueryFilter(x =>
            (!_currentUserService.IsClientPortalUser || x.ClientOrganizationId == _currentUserService.ClientOrganizationId)
            && (!_currentUserService.IsRestrictedToAssignedEngagements
                || (x.EngagementId != null
                    ? Engagements.Any(e => e.Id == x.EngagementId)
                    : ClientOrganizations.Any(o => o.Id == x.ClientOrganizationId))));

        modelBuilder.Entity<Report>().HasQueryFilter(x =>
            !_currentUserService.IsRestrictedToAssignedEngagements
            || Engagements.Any(e => e.Id == x.EngagementId));

        modelBuilder.Entity<EngagementTask>().HasQueryFilter(x =>
            !_currentUserService.IsRestrictedToAssignedEngagements
            || Engagements.Any(e => e.Id == x.EngagementId));

        modelBuilder.Entity<ScanAuthorization>().HasQueryFilter(x =>
            !_currentUserService.IsRestrictedToAssignedEngagements
            || ClientAssets.Any(a => a.Id == x.ClientAssetId));

        modelBuilder.Entity<RetestRequest>().HasQueryFilter(x =>
            !_currentUserService.IsRestrictedToAssignedEngagements
            || Vulnerabilities.Any(v => v.Id == x.VulnerabilityId));

        base.OnModelCreating(modelBuilder);
    }
}
