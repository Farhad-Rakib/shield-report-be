using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ScanToolSettingRepository : BaseRepository<ScanToolSetting>, IScanToolSettingRepository
{
    public ScanToolSettingRepository(ApplicationDbContext dbContext)
        : base(dbContext)
    {
    }

    public async Task<ScanToolSetting?> GetByToolAsync(ScanTool tool, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(s => s.Tool == tool, cancellationToken);
    }
}
