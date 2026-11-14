using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IReportDeliveryRepository : IRepository<ReportDelivery>
{
    Task<IReadOnlyList<ReportDelivery>> GetByReportIdAsync(long reportId, CancellationToken cancellationToken = default);

    // Delivery with its report, engagement and client organization (for the email job).
    Task<ReportDelivery?> GetByIdWithReportAsync(long id, CancellationToken cancellationToken = default);
}
