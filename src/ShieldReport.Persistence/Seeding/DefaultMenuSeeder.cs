using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Seeding
{
    public static class DefaultMenuSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext dbContext)
        {
            var dashboard = await UpsertMenuAsync(dbContext, "Dashboard", "/dashboard", "dashboard", Permissions.DashboardRead, null);
            var configuration = await UpsertMenuAsync(dbContext, "Configuration", null, "settings", null, null);

            await UpsertMenuAsync(dbContext, "Users", "/users", "users", Permissions.UsersRead, configuration);
            await UpsertMenuAsync(dbContext, "Roles", "/roles", "roles", Permissions.RolesRead, configuration);
            await UpsertMenuAsync(dbContext, "Permissions", "/permissions", "permissions", Permissions.PermissionsRead, configuration);
            await UpsertMenuAsync(dbContext, "User Roles", "/users/roles", "user-roles", Permissions.UserRolesRead, configuration);
            await UpsertMenuAsync(dbContext, "Role Permissions", "/roles/permissions", "role-permissions", Permissions.RolePermissionsRead, configuration);
            await UpsertMenuAsync(dbContext, "Registration Invites", "/registration-invites", "user-plus", Permissions.RegistrationInvitesRead, configuration);

            // PentestOps — Client Organizations is the org registry; Client Assets links to the
            // asset registration/scan-launch page; Scans now has its own list page.
            await UpsertMenuAsync(dbContext, "Client Organizations", "/clients", "Building2", Permissions.ClientsRead, null);
            await UpsertMenuAsync(dbContext, "Client Assets", "/assets", "Server", Permissions.ClientAssetsRead, null);
            await UpsertMenuAsync(dbContext, "Scans", "/scans", "Radar", Permissions.ScansRead, null);
            await UpsertMenuAsync(dbContext, "Vulnerabilities", "/vulnerabilities", "Bug", Permissions.VulnerabilitiesRead, null);
            await UpsertMenuAsync(dbContext, "Engagements", "/engagements", "Briefcase", Permissions.EngagementsRead, null);

            await dbContext.SaveChangesAsync();
        }

        private static async Task<Menu> UpsertMenuAsync(
            ApplicationDbContext dbContext,
            string title,
            string? url,
            string? icon,
            string? requiredPermission,
            Menu? parentMenu)
        {
            var menu = await dbContext.Menus.FirstOrDefaultAsync(existing => existing.Title == title);
            if (menu is null)
            {
                menu = new Menu
                {
                    Title = title,
                    Url = url,
                    Icon = icon,
                    RequiredPermission = requiredPermission,
                    ParentMenu = parentMenu,
                    ParentMenuId = parentMenu?.Id
                };
                dbContext.Menus.Add(menu);
            }

            return menu;
        }
    }
}
