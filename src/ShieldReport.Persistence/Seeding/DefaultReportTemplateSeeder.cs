using Microsoft.EntityFrameworkCore;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Persistence.Seeding;

// Ensures the built-in template rows exist: one Engagement template per testing type (White /
// Gray / Black Box) and one Findings summary template (Reporting -> Findings Report export). Each
// row starts with HtmlContent = null, i.e. it renders the matching built-in Handlebars template
// until an admin edits it. There is no default template; users always pick one. Idempotent:
// existing rows are matched by kind + BuiltInTestingType, then by name; an admin's HTML, custom
// fields and name are never touched.
public static class DefaultReportTemplateSeeder
{
    private static readonly (TestingType Type, string Name)[] EngagementBuiltIns =
    [
        (TestingType.White, "White Box Testing — Selise Standard"),
        (TestingType.Gray, "Gray Box Testing — Selise Standard"),
        (TestingType.Black, "Black Box Testing — Selise Standard"),
    ];

    private const string FindingsSummaryName = "Findings Summary — Selise Standard";

    public static async Task SeedAsync(DbContext dbContext)
    {
        var reportTemplates = dbContext.Set<ReportTemplate>();
        var existing = await reportTemplates.ToListAsync();
        var changed = false;

        foreach (var (type, name) in EngagementBuiltIns)
        {
            if (existing.Any(t => t.Kind == ReportTemplateKind.Engagement && t.BuiltInTestingType == type))
            {
                continue;
            }

            var byName = existing.FirstOrDefault(t => t.Kind == ReportTemplateKind.Engagement && t.BuiltInTestingType is null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                byName.SetBuiltInTestingType(type);
                changed = true;
                continue;
            }

            var template = new ReportTemplate(name, "Selise Digital");
            template.SetBuiltInTestingType(type);
            reportTemplates.Add(template);
            existing.Add(template);
            changed = true;
        }

        if (!existing.Any(t => t.Kind == ReportTemplateKind.FindingsSummary && string.Equals(t.Name, FindingsSummaryName, StringComparison.OrdinalIgnoreCase))
            && !existing.Any(t => t.Kind == ReportTemplateKind.FindingsSummary && t.HtmlContent is null))
        {
            reportTemplates.Add(new ReportTemplate(FindingsSummaryName, "Selise Digital", ReportTemplateKind.FindingsSummary));
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
