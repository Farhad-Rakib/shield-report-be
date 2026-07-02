using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class MfaRecoveryCodeRepository : BaseRepository<MfaRecoveryCode>, IMfaRecoveryCodeRepository
{
    public MfaRecoveryCodeRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<MfaRecoveryCode?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        return await DbContext.MfaRecoveryCodes.FirstOrDefaultAsync(x => x.CodeHash == codeHash, cancellationToken);
    }

    public async Task<IReadOnlyList<MfaRecoveryCode>> GetActiveByUserIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.MfaRecoveryCodes
            .Where(x => x.UserId == userId && x.UsedAtUtc == null)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<MfaRecoveryCode> codes, CancellationToken cancellationToken = default)
    {
        await DbSet.AddRangeAsync(codes, cancellationToken);
    }

    public void RemoveRange(IEnumerable<MfaRecoveryCode> codes)
    {
        DbSet.RemoveRange(codes);
    }
}
