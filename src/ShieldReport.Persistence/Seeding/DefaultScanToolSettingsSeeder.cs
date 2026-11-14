using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.ScanTools;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Persistence.Seeding;

// Ensures one ScanToolSetting row per ScanTool (inserts missing rows only — never overwrites what
// an admin changed). Defaults match the previous hardcoded/appsettings values.
public static class DefaultScanToolSettingsSeeder
{
    public static async Task SeedAsync(DbContext dbContext)
    {
        var existing = await dbContext.Set<ScanToolSetting>().Select(s => s.Tool).ToListAsync();

        foreach (var tool in Enum.GetValues<ScanTool>().Where(t => t != ScanTool.Custom && !existing.Contains(t)))
        {
            dbContext.Add(tool switch
            {
                ScanTool.Naabu => new ScanToolSetting(tool, 15, 512, 1, JsonSerializer.Serialize(NaabuOptions.Default)),
                ScanTool.Nuclei => new ScanToolSetting(tool, 120, 1024, 1, JsonSerializer.Serialize(NucleiOptions.Default)),
                ScanTool.Reconftw => new ScanToolSetting(tool, 90, 1536, 1, JsonSerializer.Serialize(ReconftwOptions.Default)),
                _ => new ScanToolSetting(tool, 60, 1024, 1, "{}")
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
