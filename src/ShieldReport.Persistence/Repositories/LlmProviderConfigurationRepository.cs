using Microsoft.EntityFrameworkCore;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;
using ShieldReport.Persistence.Context;

namespace ShieldReport.Persistence.Repositories;

public sealed class LlmProviderConfigurationRepository : ILlmProviderConfigurationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public LlmProviderConfigurationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LlmProviderConfiguration> AddAsync(LlmProviderConfiguration entity, CancellationToken cancellationToken = default)
    {
        await _dbContext.LlmProviderConfigurations.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<LlmProviderConfiguration?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LlmProviderConfigurations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<LlmProviderConfiguration?> GetByProviderAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LlmProviderConfigurations.FirstOrDefaultAsync(x => x.Provider == provider, cancellationToken);
    }

    public async Task<LlmProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LlmProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<LlmProviderConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LlmProviderConfigurations
            .AsNoTracking()
            .OrderBy(x => x.Provider)
            .ToListAsync(cancellationToken);
    }

    public void Update(LlmProviderConfiguration entity)
    {
        _dbContext.LlmProviderConfigurations.Update(entity);
    }

    public void Delete(LlmProviderConfiguration entity)
    {
        _dbContext.LlmProviderConfigurations.Remove(entity);
    }
}
