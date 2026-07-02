using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Seeding;

public sealed class RbacSeeder : IRbacSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RbacSeeder> _logger;

    public RbacSeeder(ApplicationDbContext dbContext, ILogger<RbacSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var roles = new[]
        {
            new Role(SystemRoles.SuperAdmin, "Full system access."),
            new Role(SystemRoles.Admin, "Administrative management access."),
            new Role(SystemRoles.User, "Basic application user access."),
            new Role(SystemRoles.Pentester, "Manages assets, scans, vulnerabilities, and retests across all clients."),
            new Role(SystemRoles.ClientAdmin, "Manages their own organization's assets and requests scans/retests/services."),
            new Role(SystemRoles.ClientUser, "Read-only access to their own organization's data; can post remarks.")
        };

        foreach (var role in roles)
        {
            var existingRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == role.Name, cancellationToken);
            if (existingRole is null)
            {
                await _dbContext.Roles.AddAsync(role, cancellationToken);
            }
        }

        foreach (var permissionName in Permissions.All)
        {
            var existingPermission = await _dbContext.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName, cancellationToken);
            if (existingPermission is null)
            {
                await _dbContext.Permissions.AddAsync(new Permission(permissionName, $"Permission for {permissionName}."), cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var persistedRoles = await _dbContext.Roles.Where(r => roles.Select(x => x.Name).Contains(r.Name)).ToListAsync(cancellationToken);
        var allPermissionNames = Permissions.All.ToList();
        var persistedPermissions = await _dbContext.Permissions.Where(p => allPermissionNames.Contains(p.Name)).ToListAsync(cancellationToken);


        var admin = persistedRoles.Single(r => r.Name == SystemRoles.Admin);
        var user = persistedRoles.Single(r => r.Name == SystemRoles.User);
        var pentester = persistedRoles.Single(r => r.Name == SystemRoles.Pentester);
        var clientAdmin = persistedRoles.Single(r => r.Name == SystemRoles.ClientAdmin);
        var clientUser = persistedRoles.Single(r => r.Name == SystemRoles.ClientUser);

        var rolePermissions = new List<RolePermission>();
        var existingPairs = await _dbContext.RolePermissions
            .Select(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId })
            .ToListAsync(cancellationToken);
        var existingSet = existingPairs.Select(pair => (pair.RoleId, pair.PermissionId)).ToHashSet();

        void AssignPermissions(Role role, IEnumerable<string> permissionNames)
        {
            foreach (var permission in persistedPermissions.Where(p => permissionNames.Contains(p.Name)))
            {
                if (existingSet.Add((role.Id, permission.Id)))
                {
                    rolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                }
            }
        }

        // NOTE: SuperAdmin bypasses permission checks at runtime; do NOT assign explicit permissions here.

        foreach (var permission in persistedPermissions.Where(p => p.Name is not Permissions.UsersDelete and not Permissions.RolesDelete and not Permissions.PermissionsDelete and not Permissions.UserRolesDelete and not Permissions.RolePermissionsDelete and not Permissions.ClientsDelete and not Permissions.EngagementsDelete and not Permissions.FindingsDelete and not Permissions.EvidenceDelete and not Permissions.ClientAssetsDelete and not Permissions.VulnerabilitiesDelete))
        {
            if (existingSet.Add((admin.Id, permission.Id)))
            {
                rolePermissions.Add(new RolePermission { RoleId = admin.Id, PermissionId = permission.Id });
            }
        }

        var userDefaultPermissions = persistedPermissions.Where(p => p.Name is Permissions.UsersRead or Permissions.DashboardRead);
        foreach (var permission in userDefaultPermissions)
        {
            if (existingSet.Add((user.Id, permission.Id)))
            {
                rolePermissions.Add(new RolePermission
                {
                    RoleId = user.Id,
                    PermissionId = permission.Id
                });
            }
        }

        // PENTESTER — manages assets/scans across all clients, plus staff-only invite generation
        // (per BUSINESS-FLOW-PentestOps.md §12; engagements/dashboard reused from existing grants).
        AssignPermissions(pentester, new[]
        {
            Permissions.ClientsRead, Permissions.ClientsCreate, Permissions.ClientsUpdate,
            Permissions.ClientAssetsRead, Permissions.ClientAssetsCreate, Permissions.ClientAssetsUpdate,
            Permissions.ClientAssetsAuthorizeForScanning,
            Permissions.ScansRead, Permissions.ScansCreate, Permissions.ScansCancel,
            Permissions.EngagementsRead, Permissions.DashboardRead,
            Permissions.RegistrationInvitesCreate, Permissions.RegistrationInvitesRead, Permissions.RegistrationInvitesRevoke,
            Permissions.VulnerabilitiesRead, Permissions.VulnerabilitiesCreate, Permissions.VulnerabilitiesUpdate,
            Permissions.VulnerabilityAttachmentsRead, Permissions.VulnerabilityAttachmentsUpload, Permissions.VulnerabilityAttachmentsDelete,
            Permissions.VulnerabilityRemarksRead, Permissions.VulnerabilityRemarksCreate,
            Permissions.RetestRequestsRead, Permissions.RetestRequestsResolve
        });

        // CLIENT_ADMIN — manages their own org's assets, triggers scans on already-authorized
        // assets, but cannot authorize an asset for scanning (that requires the firm's sign-off).
        AssignPermissions(clientAdmin, new[]
        {
            Permissions.ClientAssetsRead, Permissions.ClientAssetsCreate, Permissions.ClientAssetsUpdate,
            Permissions.ScansRead, Permissions.ScansCreate, Permissions.ScansCancel,
            Permissions.DashboardRead,
            Permissions.VulnerabilitiesRead, Permissions.VulnerabilityAttachmentsRead,
            Permissions.VulnerabilityRemarksRead, Permissions.VulnerabilityRemarksCreate,
            // Read-only Engagement Tracker in the client portal (TASK-GROUPS-MASTER.md #95) —
            // EngagementsController has no [ClientScope] route param, so it's the Engagement
            // HasQueryFilter alone that scopes the list to this org; the permission was never
            // granted to either client role until the portal shell needed it.
            Permissions.EngagementsRead,
            // CLIENT_ADMIN can raise a retest once they've marked their fix Patched/In Progress
            // (gated in RetestRequestService, not here) — never Resolve (PENTESTER/Admin only).
            Permissions.RetestRequestsRead, Permissions.RetestRequestsCreate
        });

        // CLIENT_USER — read-only across their own org's data, but can post remarks (per the
        // role matrix — posting a remark is open to all 4 roles).
        AssignPermissions(clientUser, new[]
        {
            Permissions.ClientAssetsRead, Permissions.ScansRead, Permissions.DashboardRead,
            Permissions.VulnerabilitiesRead, Permissions.VulnerabilityAttachmentsRead,
            Permissions.VulnerabilityRemarksRead, Permissions.VulnerabilityRemarksCreate,
            Permissions.EngagementsRead, Permissions.RetestRequestsRead
        });

        if (rolePermissions.Count > 0)
        {
            await _dbContext.RolePermissions.AddRangeAsync(rolePermissions, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("RBAC seed completed successfully.");
    }
}
