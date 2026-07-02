using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface ICvssSeverityBandRepository : IRepository<CvssSeverityBand>
{
    Task<CvssSeverityBand?> FindBandForScoreAsync(decimal score, CancellationToken cancellationToken = default);
}
