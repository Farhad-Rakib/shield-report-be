using System.Text.Json;

namespace ShieldReport.Application.Dashboard;

public sealed class DashboardLayoutService : IDashboardLayoutService
{
    private readonly IDashboardLayoutRepository _repo;

    public DashboardLayoutService(IDashboardLayoutRepository repo) => _repo = repo;

    public Task<DashboardLayoutDto?> GetLayoutAsync(long userId, CancellationToken ct = default)
        => _repo.GetByUserIdAsync(userId, ct);

    public Task SaveLayoutAsync(long userId, string[] widgetOrder, string[] hiddenWidgets, CancellationToken ct = default)
    {
        var orderJson  = JsonSerializer.Serialize(widgetOrder);
        var hiddenJson = JsonSerializer.Serialize(hiddenWidgets);
        return _repo.UpsertAsync(userId, orderJson, hiddenJson, ct);
    }
}
