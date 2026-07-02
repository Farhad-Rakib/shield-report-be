using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IScanFindingRawRepository : IRepository<ScanFindingRaw>
{
    Task<IReadOnlyList<ScanFindingRaw>> GetByScanIdAsync(long scanId, CancellationToken cancellationToken = default);
}
