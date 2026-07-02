namespace ShieldReport.Persistence.Seeding;

public interface IRbacSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
