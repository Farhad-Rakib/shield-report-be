namespace ShieldReport.Application.Dashboard;

public interface IDashboardLayoutRepository
{
    Task<DashboardLayoutDto?> GetByUserIdAsync(long userId, CancellationToken ct = default);
    Task UpsertAsync(long userId, string widgetOrder, string hiddenWidgets, CancellationToken ct = default);
}

public record DashboardLayoutDto(string[] WidgetOrder, string[] HiddenWidgets);
