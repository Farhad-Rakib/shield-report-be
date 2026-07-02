using Microsoft.EntityFrameworkCore;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Persistence.Seeding;

public static class DefaultCvssSeverityBandSeeder
{
    public static async Task SeedAsync(DbContext dbContext)
    {
        var bands = new[]
        {
            new CvssSeverityBand(Severity.Informational, 0.0m, 0.0m),
            new CvssSeverityBand(Severity.Low, 0.1m, 3.9m),
            new CvssSeverityBand(Severity.Medium, 4.0m, 6.9m),
            new CvssSeverityBand(Severity.High, 7.0m, 8.9m),
            new CvssSeverityBand(Severity.Critical, 9.0m, 10.0m)
        };

        foreach (var band in bands)
        {
            if (!await dbContext.Set<CvssSeverityBand>().AnyAsync(b => b.Severity == band.Severity))
            {
                dbContext.Add(band);
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
