using ShieldReport.Application.LlmProviderConfigurations.Dtos;

namespace ShieldReport.Application.LlmProviderConfigurations;

public interface ILlmProviderConfigurationService
{
    Task<IReadOnlyList<LlmProviderConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LlmProviderConfigurationDto> CreateAsync(CreateLlmProviderConfigurationRequestDto request, long? currentUserId, CancellationToken cancellationToken = default);
    Task<LlmProviderConfigurationDto> UpdateAsync(long id, UpdateLlmProviderConfigurationRequestDto request, long? currentUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<LlmProviderConfigurationDto> ActivateAsync(long id, long? currentUserId, CancellationToken cancellationToken = default);
}
