using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShieldReport.Application.Common.Interfaces;

namespace ShieldReport.Persistence.Context;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var provider = "sqlserver".ToLowerInvariant();

        const string sqlServerConnection = "Server=localhost,1433;Database=ShieldReportDb;User Id=sa;Password=Mssql@12345Strong;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        if (provider == "sqlserver")
        {
            optionsBuilder.UseSqlServer(sqlServerConnection);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported database provider '{provider}'. Supported values: postgres, sqlserver.");
        }

        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeCurrentUserService());
    }

    // No HTTP request exists at design time (migrations/scaffolding) — the query filters in
    // OnModelCreating only need *some* ICurrentUserService to build the model, never a real one.
    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public long? UserId => null;
        public string? Email => null;
        public long? ClientOrganizationId => null;
        public bool IsClientPortalUser => false;
        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
    }
}
