using Microsoft.EntityFrameworkCore;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Persistence.Seeding;

// Ensures one built-in template row per testing type (White / Gray / Black Box). Each row starts
// with HtmlContent = null, i.e. it renders the matching built-in Handlebars template until an admin
// edits it. Idempotent: existing rows are matched by BuiltInTestingType, then by name; an admin's
// HTML, custom fields, name and the current default are never touched.
public static class DefaultReportTemplateSeeder
{
    private static readonly (TestingType Type, string Name)[] BuiltIns =
    [
        (TestingType.White, "White Box Testing — Selise Standard"),
        (TestingType.Gray, "Gray Box Testing — Selise Standard"),
        (TestingType.Black, "Black Box Testing — Selise Standard"),
    ];

    public static async Task SeedAsync(DbContext dbContext)
    {
        var reportTemplates = dbContext.Set<ReportTemplate>();
        var existing = await reportTemplates.ToListAsync();
        var hasDefault = existing.Any(t => t.IsDefault);
        var changed = false;

        foreach (var (type, name) in BuiltIns)
        {
            if (existing.Any(t => t.BuiltInTestingType == type))
            {
                continue;
            }

            var byName = existing.FirstOrDefault(t => t.BuiltInTestingType is null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                byName.SetBuiltInTestingType(type);
                changed = true;
                continue;
            }

            // White becomes the default only when no template is the default yet (fresh database).
            var template = new ReportTemplate(name, "Selise Digital", isDefault: type == TestingType.White && !hasDefault);
            template.SetBuiltInTestingType(type);
            reportTemplates.Add(template);
            existing.Add(template);
            hasDefault |= template.IsDefault;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
