using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces;

public interface ILlmProviderConfigurationRepository
{
    Task<LlmProviderConfiguration> AddAsync(LlmProviderConfiguration entity, CancellationToken cancellationToken = default);
    Task<LlmProviderConfiguration?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, long? excludeId, CancellationToken cancellationToken = default);
    Task<LlmProviderConfiguration?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LlmProviderConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    void Update(LlmProviderConfiguration entity);
    void Delete(LlmProviderConfiguration entity);
}
