using Microsoft.EntityFrameworkCore;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Seeding;

public static class DefaultReportTemplateSeeder
{
    public static async Task SeedAsync(DbContext dbContext)
    {
        var reportTemplates = dbContext.Set<ReportTemplate>();
        if (!await reportTemplates.AnyAsync())
        {
            reportTemplates.Add(new ReportTemplate("White Box Testing — Selise Standard", "Selise Digital", isDefault: true));
            await dbContext.SaveChangesAsync();
        }
    }
}
