using ShieldReport.Application.FindingsReport.Dtos;

namespace ShieldReport.Application.FindingsReport;

public interface IFindingsReportService
{
    Task<FindingsReportDto> GetReportAsync(FindingsReportFilter filter, CancellationToken cancellationToken = default);
}
