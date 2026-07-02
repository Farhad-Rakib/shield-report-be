namespace ShieldReport.Application.Dashboard;

public interface IDashboardLayoutService
{
    Task<DashboardLayoutDto?> GetLayoutAsync(long userId, CancellationToken ct = default);
    Task SaveLayoutAsync(long userId, string[] widgetOrder, string[] hiddenWidgets, CancellationToken ct = default);
}
