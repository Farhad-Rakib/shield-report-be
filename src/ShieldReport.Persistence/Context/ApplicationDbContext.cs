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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Layer 2 client-portal isolation (unconditional, no role exception) — see
        // DATABASE-DESIGN-PentestOps.md §5. Defense in depth on top of [ClientScope] at the
        // controller level; this is the layer that can't be skipped by a forgotten attribute.
        modelBuilder.Entity<ClientAsset>().HasQueryFilter(x =>
            !_currentUserService.IsClientPortalUser || x.ClientOrganizationId == _currentUserService.ClientOrganizationId);

        modelBuilder.Entity<Vulnerability>().HasQueryFilter(x =>
            !_currentUserService.IsClientPortalUser || x.ClientOrganizationId == _currentUserService.ClientOrganizationId);

        // Engagement reuses the same isolation pattern (per TASK-GROUPS-EngagementManagement.md
        // Group B #3 — "tenant-scoped for clients via existing HasQueryFilter pattern").
        modelBuilder.Entity<Engagement>().HasQueryFilter(x =>
            !_currentUserService.IsClientPortalUser || x.ClientOrganizationId == _currentUserService.ClientOrganizationId);

        base.OnModelCreating(modelBuilder);
    }
}
