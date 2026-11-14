using System.Diagnostics;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.LlmProviderConfigurations.Dtos;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.LlmProviderConfigurations;

public sealed class LlmProviderConfigurationService : ILlmProviderConfigurationService
{
    private const string TestPrompt = "Reply with exactly this JSON and nothing else: {\"ok\": true}";

    private readonly ILlmProviderConfigurationRepository _repository;
    private readonly IApiKeyEncryptor _apiKeyEncryptor;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly IUnitOfWork _unitOfWork;

    public LlmProviderConfigurationService(ILlmProviderConfigurationRepository repository, IApiKeyEncryptor apiKeyEncryptor, ILlmClientFactory llmClientFactory, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _apiKeyEncryptor = apiKeyEncryptor;
        _llmClientFactory = llmClientFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<LlmProviderConfigurationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var configurations = await _repository.GetAllAsync(cancellationToken);
        return configurations.Select(MapToDto).ToList();
    }

    public async Task<LlmProviderConfigurationDto> CreateAsync(CreateLlmProviderConfigurationRequestDto request, long? currentUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new AppException("API key is required.", 400);
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? $"{request.Provider} · {request.Model}" : request.Name.Trim();
        Validate(name, request.Provider, request.Model, request.BaseUrl, request.Temperature, request.MaxOutputTokens);
        await EnsureUniqueNameAsync(name, null, cancellationToken);

        var configuration = new LlmProviderConfiguration(
            name,
            request.Provider,
            _apiKeyEncryptor.Encrypt(request.ApiKey.Trim()),
            request.Model.Trim(),
            NormalizeBaseUrl(request.BaseUrl),
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

        var name = string.IsNullOrWhiteSpace(request.Name) ? configuration.Name : request.Name.Trim();
        Validate(name, configuration.Provider, request.Model, request.BaseUrl, request.Temperature, request.MaxOutputTokens);
        await EnsureUniqueNameAsync(name, id, cancellationToken);

        var encryptedApiKey = string.IsNullOrWhiteSpace(request.ApiKey) ? null : _apiKeyEncryptor.Encrypt(request.ApiKey.Trim());
        configuration.UpdateDetails(name, encryptedApiKey, request.Model.Trim(), NormalizeBaseUrl(request.BaseUrl), request.Temperature, request.MaxOutputTokens, request.PromptTemplate ?? configuration.PromptTemplate, currentUserId);

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

    public async Task<LlmProviderConfigurationDto> DeactivateAsync(long id, long? currentUserId, CancellationToken cancellationToken = default)
    {
        var configuration = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("LLM provider configuration not found.", 404);

        configuration.Deactivate(currentUserId);
        _repository.Update(configuration);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(configuration);
    }

    public async Task<LlmProviderTestResultDto> TestAsync(long id, CancellationToken cancellationToken = default)
    {
        var configuration = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("LLM provider configuration not found.", 404);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = _llmClientFactory.GetClient(configuration.Provider);
            var request = new LlmCompletionRequest(
                configuration.Provider,
                _apiKeyEncryptor.Decrypt(configuration.ApiKeyEncrypted),
                configuration.Model,
                configuration.BaseUrl,
                configuration.Temperature,
                Math.Max(configuration.MaxOutputTokens, 64),
                TestPrompt);
            var reply = await client.CompleteAsync(request, cancellationToken);
            stopwatch.Stop();
            var trimmed = reply.Length > 300 ? reply[..300] + "…" : reply;
            return new LlmProviderTestResultDto(true, stopwatch.ElapsedMilliseconds, trimmed, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new LlmProviderTestResultDto(false, stopwatch.ElapsedMilliseconds, null, DescribeError(ex));
        }
    }

    private static string DescribeError(Exception ex)
    {
        var message = ex switch
        {
            TaskCanceledException => "The provider did not answer within the timeout.",
            HttpRequestException http when http.StatusCode is not null => $"HTTP {(int)http.StatusCode.Value} {http.StatusCode.Value}: {http.Message}",
            _ => ex.Message,
        };
        return message.Length > 500 ? message[..500] + "…" : message;
    }

    private static void Validate(string name, LlmProvider provider, string model, string? baseUrl, decimal temperature, int maxOutputTokens)
    {
        if (name.Length > 100)
        {
            throw new AppException("Name must be 100 characters or fewer.", 400);
        }

        if (string.IsNullOrWhiteSpace(model) || model.Length > 200)
        {
            throw new AppException("Model is required (200 characters max).", 400);
        }

        if (provider == LlmProvider.OpenAiCompatible && string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new AppException("Base URL is required for an OpenAI-compatible provider (for example https://api.groq.com/openai/v1).", 400);
        }

        if (!string.IsNullOrWhiteSpace(baseUrl)
            && (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
        {
            throw new AppException("Base URL must be an absolute http(s) URL.", 400);
        }

        if (temperature < 0 || temperature > 2)
        {
            throw new AppException("Temperature must be between 0 and 2.", 400);
        }

        if (maxOutputTokens < 64 || maxOutputTokens > 64000)
        {
            throw new AppException("Max output tokens must be between 64 and 64000.", 400);
        }
    }

    private async Task EnsureUniqueNameAsync(string name, long? excludeId, CancellationToken cancellationToken)
    {
        if (await _repository.NameExistsAsync(name, excludeId, cancellationToken))
        {
            throw new AppException($"A provider configuration named \"{name}\" already exists.", 409);
        }
    }

    private static string? NormalizeBaseUrl(string? baseUrl) => string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');

    private LlmProviderConfigurationDto MapToDto(LlmProviderConfiguration configuration)
    {
        var decrypted = _apiKeyEncryptor.Decrypt(configuration.ApiKeyEncrypted);
        return new LlmProviderConfigurationDto(
            configuration.Id,
            configuration.Name,
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
