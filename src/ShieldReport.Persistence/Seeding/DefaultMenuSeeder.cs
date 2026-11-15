using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Seeding
{
    // The sidebar: top-level entries without a URL are section headings; their children are the
    // links. Runs on every start and brings existing rows (matched by title, or by a former title)
    // to this layout, so a renamed or regrouped entry never duplicates.
    public static class DefaultMenuSeeder
    {
        private static readonly Dictionary<string, string> FormerTitles = new()
        {
            ["Configuration"] = "Administration",
            ["LLM Provider Configuration"] = "Providers & Models",
            ["Scan Tools"] = "Tool Configuration",
        };

        public static async Task SeedAsync(ApplicationDbContext dbContext)
        {
            foreach (var (oldTitle, newTitle) in FormerTitles)
            {
                var old = await dbContext.Menus.FirstOrDefaultAsync(m => m.Title == oldTitle);
                if (old is not null && !await dbContext.Menus.AnyAsync(m => m.Title == newTitle))
                {
                    old.Title = newTitle;
                }
            }

            await dbContext.SaveChangesAsync();

            var order = 0;
            await UpsertAsync(dbContext, "Dashboard", "/dashboard", "LayoutDashboard", Permissions.DashboardRead, null, ++order);

            var operations = await UpsertAsync(dbContext, "Operations", null, "Crosshair", null, null, ++order);
            // Order follows the onboarding flow: organization -> its assets -> engagement -> scans.
            await UpsertAsync(dbContext, "Client Organizations", "/clients", "Building2", Permissions.ClientsRead, operations, 1);
            await UpsertAsync(dbContext, "Client Assets", "/assets", "Server", Permissions.ClientAssetsRead, operations, 2);
            await UpsertAsync(dbContext, "Engagements", "/engagements", "Briefcase", Permissions.EngagementsRead, operations, 3);
            await UpsertAsync(dbContext, "Scans", "/scans", "Radar", Permissions.ScansRead, operations, 4);

            var findings = await UpsertAsync(dbContext, "Findings", null, "ShieldCheck", null, null, ++order);
            await UpsertAsync(dbContext, "Vulnerabilities", "/vulnerabilities", "Bug", Permissions.VulnerabilitiesRead, findings, 1);

            var scanning = await UpsertAsync(dbContext, "Scanning", null, "Activity", null, null, ++order);
            await UpsertAsync(dbContext, "Tool Configuration", "/scan-tools", "Settings2", Permissions.ScanToolsManage, scanning, 1);
            await UpsertAsync(dbContext, "Custom Tools", "/custom-tools", "Puzzle", Permissions.CustomScanToolsManage, scanning, 2);

            var ai = await UpsertAsync(dbContext, "AI", null, "Bot", null, null, ++order);
            await UpsertAsync(dbContext, "Providers & Models", "/llm-providers", "Cpu", Permissions.LlmProviderConfigurationsRead, ai, 1);
            await UpsertAsync(dbContext, "Prompt Library", "/llm-prompts", "ScrollText", Permissions.LlmProviderConfigurationsRead, ai, 2);
            await UpsertAsync(dbContext, "Provider List", "/llm-provider-catalog", "ListTree", Permissions.LlmProviderConfigurationsRead, ai, 3);

            var reporting = await UpsertAsync(dbContext, "Reporting", null, "FileText", null, null, ++order);
            await UpsertAsync(dbContext, "Report Templates", "/report-templates", "FileCode2", Permissions.ReportTemplatesRead, reporting, 1);
            await UpsertAsync(dbContext, "Findings Report", "/findings-report", "ClipboardList", Permissions.FindingsReportRead, reporting, 2);

            var admin = await UpsertAsync(dbContext, "Administration", null, "KeyRound", null, null, ++order);
            await UpsertAsync(dbContext, "Users", "/users", "users", Permissions.UsersRead, admin, 1);
            await UpsertAsync(dbContext, "Roles", "/roles", "roles", Permissions.RolesRead, admin, 2);
            await UpsertAsync(dbContext, "Permissions", "/permissions", "permissions", Permissions.PermissionsRead, admin, 3);
            await UpsertAsync(dbContext, "User Roles", "/users/roles", "user-roles", Permissions.UserRolesRead, admin, 4);
            await UpsertAsync(dbContext, "Role Permissions", "/roles/permissions", "role-permissions", Permissions.RolePermissionsRead, admin, 5);
            await UpsertAsync(dbContext, "Registration Invites", "/registration-invites", "user-plus", Permissions.RegistrationInvitesRead, admin, 6);
            await UpsertAsync(dbContext, "Audit Log", "/audit-logs", "History", Permissions.AuditLogsRead, admin, 7);

            await dbContext.SaveChangesAsync();
        }

        private static async Task<Menu> UpsertAsync(
            ApplicationDbContext dbContext,
            string title,
            string? url,
            string? icon,
            string? requiredPermission,
            Menu? parentMenu,
            int sortOrder)
        {
            var menu = await dbContext.Menus.FirstOrDefaultAsync(existing => existing.Title == title);
            if (menu is null)
            {
                menu = new Menu { Title = title };
                dbContext.Menus.Add(menu);
            }

            menu.Url = url;
            menu.Icon = icon;
            menu.RequiredPermission = requiredPermission;
            menu.ParentMenu = parentMenu;
            menu.SortOrder = sortOrder;
            if (parentMenu is null)
            {
                menu.ParentMenuId = null;
            }

            // Parents must have ids before children reference them.
            await dbContext.SaveChangesAsync();
            return menu;
        }
    }
}
