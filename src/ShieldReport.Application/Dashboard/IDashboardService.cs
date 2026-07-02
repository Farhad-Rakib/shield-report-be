using ShieldReport.Application.Dashboard.Dtos;

namespace ShieldReport.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}
