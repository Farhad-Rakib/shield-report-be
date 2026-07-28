using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class ReportTemplateRepository : IReportTemplateRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ReportTemplateRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReportTemplate> AddAsync(ReportTemplate entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.ReportTemplates.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<ReportTemplate?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReportTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<ReportTemplate?> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReportTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.IsDefault, cancellationToken)
            ?? await _dbContext.ReportTemplates.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ReportTemplate?> GetDefaultTrackedAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReportTemplates.FirstOrDefaultAsync(x => x.IsDefault, cancellationToken);
    }

    public async Task<IReadOnlyList<ReportTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ReportTemplates.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public void Update(ReportTemplate entity)
    {
        _dbContext.ReportTemplates.Update(entity);
    }

    public void Delete(ReportTemplate entity)
    {
        _dbContext.ReportTemplates.Remove(entity);
    }
}
