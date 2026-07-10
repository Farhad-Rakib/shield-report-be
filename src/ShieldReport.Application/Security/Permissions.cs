namespace ShieldReport.Application.Security;

public static class Permissions
{
    public const string UsersRead = "users.read";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersDelete = "users.delete";
    public const string RolesRead = "roles.read";
    public const string RolesCreate = "roles.create";
    public const string RolesUpdate = "roles.update";
    public const string RolesDelete = "roles.delete";
    public const string PermissionsRead = "permissions.read";
    public const string PermissionsCreate = "permissions.create";
    public const string PermissionsUpdate = "permissions.update";
    public const string PermissionsDelete = "permissions.delete";
    public const string UserRolesRead = "user-roles.read";
    public const string UserRolesCreate = "user-roles.create";
    public const string UserRolesUpdate = "user-roles.update";
    public const string UserRolesDelete = "user-roles.delete";
    public const string RolePermissionsRead = "role-permissions.read";
    public const string RolePermissionsCreate = "role-permissions.create";
    public const string RolePermissionsUpdate = "role-permissions.update";
    public const string RolePermissionsDelete = "role-permissions.delete";
    public const string SystemEndpointsRead = "system.endpoints.read";
    public const string SystemCacheRead = "system.cache.read";
    public const string SystemCacheFlush = "system.cache.flush";
    public const string MenusRead = "menus.read";
    public const string MenusCreate = "menus.create";
    public const string MenusUpdate = "menus.update";
    public const string MenusDelete = "menus.delete";
    public const string DashboardRead = "dashboard.read";
    public const string SiteSettingsRead = "site-settings.read";
    public const string SiteSettingsCreate = "site-settings.create";
    public const string SiteSettingsUpdate = "site-settings.update";
    public const string SiteSettingsDelete = "site-settings.delete";

    // Clients & Engagements
    public const string ClientsRead = "clients.read";
    public const string ClientsCreate = "clients.create";
    public const string ClientsUpdate = "clients.update";
    public const string ClientsDelete = "clients.delete";
    public const string EngagementsRead = "engagements.read";
    public const string EngagementsCreate = "engagements.create";
    public const string EngagementsUpdate = "engagements.update";
    public const string EngagementsDelete = "engagements.delete";
    public const string EngagementsAssign = "engagements.assign";

    // Findings & Evidence
    public const string FindingsRead = "findings.read";
    public const string FindingsCreate = "findings.create";
    public const string FindingsUpdate = "findings.update";
    public const string FindingsDelete = "findings.delete";
    public const string FindingTemplatesManage = "finding-templates.manage";
    public const string EvidenceRead = "evidence.read";
    public const string EvidenceUpload = "evidence.upload";
    public const string EvidenceDelete = "evidence.delete";

    // Reports — for the not-yet-built Finding/Report workflow engine (PRD-PentestOps.md), not
    // the removed Configuration > Reports mock dashboard page. No controller uses these yet.
    public const string ReportsCreate = "reports.create";
    public const string ReportsUpdate = "reports.update";
    public const string ReportsExport = "reports.export";
    public const string ReportCommentsManage = "report-comments.manage";

    // Workflow transitions — one permission per transition class, not per literal transition
    // row, so adding a custom intermediate state doesn't require a new platform permission.
    public const string WorkflowSubmit = "workflow.transition.submit";
    public const string WorkflowAdvance = "workflow.transition.advance";
    public const string WorkflowRequestChanges = "workflow.transition.requestchanges";
    public const string WorkflowApprove = "workflow.transition.approve";
    public const string WorkflowPublish = "workflow.transition.publish";
    public const string WorkflowDefinitionsManage = "workflow-definitions.manage";

    // Activity Visualization — deliberately its own permission, narrower than EngagementsRead.
    public const string EngagementsActivityRead = "engagements.activity.read";

    // Client Assets & Scanning (PentestOps) — registration and scan-authorization are
    // deliberately separate permissions so registering an asset never implies it's scannable.
    public const string ClientAssetsRead = "client-assets.read";
    public const string ClientAssetsCreate = "client-assets.create";
    public const string ClientAssetsUpdate = "client-assets.update";
    public const string ClientAssetsDelete = "client-assets.delete";
    public const string ClientAssetsAuthorizeForScanning = "client-assets.authorize-for-scanning";
    public const string ScansRead = "scans.read";
    public const string ScansCreate = "scans.create";
    public const string ScansCancel = "scans.cancel";

    // Registration Invites — staff-only generation (PentestOps invite-based onboarding).
    public const string RegistrationInvitesCreate = "registration-invites.create";
    public const string RegistrationInvitesRead = "registration-invites.read";
    public const string RegistrationInvitesRevoke = "registration-invites.revoke";

    // Vulnerabilities (PentestOps) — deliberately distinct from the legacy Findings* constants
    // above, which predate this build and belong to ShieldReport's original, unbuilt
    // Finding/workflow-engine entity (see PRD-PentestOps.md §"Vulnerability... Deliberately
    // not named 'Finding'"). Delete is PLATFORM_ADMIN-only, so it's never granted to Pentester.
    public const string VulnerabilitiesRead = "vulnerabilities.read";
    public const string VulnerabilitiesCreate = "vulnerabilities.create";
    public const string VulnerabilitiesUpdate = "vulnerabilities.update";
    public const string VulnerabilitiesDelete = "vulnerabilities.delete";

    // Vulnerability Attachments — same "distinct from legacy Evidence*" rationale as above.
    public const string VulnerabilityAttachmentsRead = "vulnerability-attachments.read";
    public const string VulnerabilityAttachmentsUpload = "vulnerability-attachments.upload";
    public const string VulnerabilityAttachmentsDelete = "vulnerability-attachments.delete";

    // Vulnerability Remarks — Create/Read granted broadly to all 4 roles (post is open to
    // everyone per the role matrix); edit/delete-own and staff-only IsInternal are enforced as
    // row-level/role checks in the service, not separate permissions.
    public const string VulnerabilityRemarksRead = "vulnerability-remarks.read";
    public const string VulnerabilityRemarksCreate = "vulnerability-remarks.create";

    // Retest Workflow — Create is granted to PLATFORM_ADMIN (ungated) and CLIENT_ADMIN
    // (gated on patch status in the service layer); Resolve is PLATFORM_ADMIN/PENTESTER only.
    public const string RetestRequestsRead = "retest-requests.read";
    public const string RetestRequestsCreate = "retest-requests.create";
    public const string RetestRequestsResolve = "retest-requests.resolve";

    public static readonly string[] All =
    [
        UsersRead,
        UsersCreate,
        UsersUpdate,
        UsersDelete,
        RolesRead,
        RolesCreate,
        RolesUpdate,
        RolesDelete,
        PermissionsRead,
        PermissionsCreate,
        PermissionsUpdate,
        PermissionsDelete,
        UserRolesRead,
        UserRolesCreate,
        UserRolesUpdate,
        UserRolesDelete,
        RolePermissionsRead,
        RolePermissionsCreate,
        RolePermissionsUpdate,
        RolePermissionsDelete,
        MenusRead,
        MenusCreate,
        MenusUpdate,
        MenusDelete,
        DashboardRead,
        SiteSettingsRead,
        SiteSettingsCreate,
        SiteSettingsUpdate,
        SiteSettingsDelete,
        SystemEndpointsRead,
        SystemCacheRead,
        SystemCacheFlush,
        ClientsRead,
        ClientsCreate,
        ClientsUpdate,
        ClientsDelete,
        EngagementsRead,
        EngagementsCreate,
        EngagementsUpdate,
        EngagementsDelete,
        EngagementsAssign,
        FindingsRead,
        FindingsCreate,
        FindingsUpdate,
        FindingsDelete,
        FindingTemplatesManage,
        EvidenceRead,
        EvidenceUpload,
        EvidenceDelete,
        ReportsCreate,
        ReportsUpdate,
        ReportsExport,
        ReportCommentsManage,
        WorkflowSubmit,
        WorkflowAdvance,
        WorkflowRequestChanges,
        WorkflowApprove,
        WorkflowPublish,
        WorkflowDefinitionsManage,
        EngagementsActivityRead,
        ClientAssetsRead,
        ClientAssetsCreate,
        ClientAssetsUpdate,
        ClientAssetsDelete,
        ClientAssetsAuthorizeForScanning,
        ScansRead,
        ScansCreate,
        ScansCancel,
        RegistrationInvitesCreate,
        RegistrationInvitesRead,
        RegistrationInvitesRevoke,
        VulnerabilitiesRead,
        VulnerabilitiesCreate,
        VulnerabilitiesUpdate,
        VulnerabilitiesDelete,
        VulnerabilityAttachmentsRead,
        VulnerabilityAttachmentsUpload,
        VulnerabilityAttachmentsDelete,
        VulnerabilityRemarksRead,
        VulnerabilityRemarksCreate,
        RetestRequestsRead,
        RetestRequestsCreate,
        RetestRequestsResolve
    ];
}
