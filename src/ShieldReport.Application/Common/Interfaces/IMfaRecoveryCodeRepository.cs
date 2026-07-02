using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Common.Interfaces;

public interface IMfaRecoveryCodeRepository : IRepository<MfaRecoveryCode>
{
    Task<MfaRecoveryCode?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MfaRecoveryCode>> GetActiveByUserIdAsync(long userId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<MfaRecoveryCode> codes, CancellationToken cancellationToken = default);
    void RemoveRange(IEnumerable<MfaRecoveryCode> codes);
}
