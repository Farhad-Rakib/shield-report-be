using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.LlmProviderConfigurations.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.LlmProviderConfigurations;

public sealed class LlmProviderConfigurationService : ILlmProviderConfigurationService
{
    private readonly ILlmProviderConfigurationRepository _repository;
    private readonly IApiKeyEncryptor _apiKeyEncryptor;
    private readonly IUnitOfWork _unitOfWork;

    public LlmProviderConfigurationService(ILlmProviderConfigurationRepository repository, IApiKeyEncryptor apiKeyEncryptor, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _apiKeyEncryptor = apiKeyEncryptor;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<LlmProviderConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var configurations = await _repository.GetAllAsync(cancellationToken);
        return configurations.Select(MapToDto).ToList();
    }

    public async Task<LlmProviderConfigurationDto> CreateAsync(CreateLlmProviderConfigurationRequestDto request, long? currentUserId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByProviderAsync(request.Provider, cancellationToken);
        if (existing is not null)
        {
            throw new AppException($"A configuration for {request.Provider} already exists — edit it instead of creating another.", 409);
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new AppException("API key is required.", 400);
        }

        var configuration = new LlmProviderConfiguration(
            request.Provider,
            _apiKeyEncryptor.Encrypt(request.ApiKey),
            request.Model,
            request.BaseUrl,
            request.Temperature,
            request.MaxOutputTokens,
            request.PromptTemplate ?? LlmProviderConfiguration.DefaultPromptTemplate);

        await _repository.AddAsync(configuration, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(configuration);
    }

    public async Task<LlmProviderConfigurationDto> UpdateAsync(long id, UpdateLlmProviderConfigurationRequestDto request, long? currentUserId, CancellationToken cancellationToken = default)
    {
        var configuration = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("LLM provider configuration not found.", 404);

        var encryptedApiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : _apiKeyEncryptor.Encrypt(request.ApiKey);
        configuration.UpdateDetails(encryptedApiKey, request.Model, request.BaseUrl, request.Temperature, request.MaxOutputTokens, request.PromptTemplate ?? configuration.PromptTemplate, currentUserId);

        _repository.Update(configuration);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(configuration);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var configuration = await _repository.GetByIdAsync(id, cancellationToken);
        if (configuration is null)
        {
            return;
        }

        _repository.Delete(configuration);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<LlmProviderConfigurationDto> ActivateAsync(long id, long? currentUserId, CancellationToken cancellationToken = default)
    {
        var configuration = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("LLM provider configuration not found.", 404);

        var allConfigurations = await _repository.GetAllAsync(cancellationToken);
        foreach (var other in allConfigurations.Where(c => c.Id != id && c.IsActive))
        {
            other.Deactivate(currentUserId);
            _repository.Update(other);
        }

        configuration.Activate(currentUserId);
        _repository.Update(configuration);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(configuration);
    }

    private LlmProviderConfigurationDto MapToDto(LlmProviderConfiguration configuration)
    {
        var decrypted = _apiKeyEncryptor.Decrypt(configuration.ApiKeyEncrypted);
        return new LlmProviderConfigurationDto(
            configuration.Id,
            configuration.Provider,
            _apiKeyEncryptor.Mask(decrypted),
            configuration.Model,
            configuration.BaseUrl,
            configuration.Temperature,
            configuration.MaxOutputTokens,
            configuration.PromptTemplate,
            configuration.IsActive);
    }
}
