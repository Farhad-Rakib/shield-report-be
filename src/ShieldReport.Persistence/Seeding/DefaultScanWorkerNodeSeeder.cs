using Microsoft.EntityFrameworkCore;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Seeding;

public static class DefaultScanWorkerNodeSeeder
{
    private const string DefaultNodeName = "local-worker";

    public static async Task SeedAsync(DbContext dbContext)
    {
        if (!await dbContext.Set<ScanWorkerNode>().AnyAsync(n => n.Name == DefaultNodeName))
        {
            // MVP runs a single in-process worker (see PRD §8) — this row keeps the schema
            // fleet-ready for a future move to external workers without changing the shape.
            dbContext.Add(new ScanWorkerNode(DefaultNodeName, "localhost", maxConcurrentJobs: 3));
            await dbContext.SaveChangesAsync();
        }
    }
}
