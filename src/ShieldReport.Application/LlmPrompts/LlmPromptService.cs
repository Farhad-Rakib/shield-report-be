using System.Diagnostics;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.LlmProviderConfigurations.Dtos;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.LlmPrompts;

public sealed record LlmPromptDto(
    string Key,
    string Name,
    string Description,
    string Template,
    string DefaultTemplate,
    bool IsCustomized,
    IReadOnlyList<LlmPromptPlaceholder> Placeholders,
    IReadOnlyList<string> RequiredPlaceholders,
    IReadOnlyList<string> ExpectedJsonFields,
    DateTime? UpdatedAt);

public sealed record UpdateLlmPromptRequestDto(string Template);

// Optional unsaved template to try; null = the saved one.
public sealed record TestLlmPromptRequestDto(string? Template);

public sealed record LlmPromptTestResultDto(bool Success, long LatencyMs, string RenderedPrompt, string? Reply, bool ReplyIsValidJson, string? Error, string? ProviderName);

public interface ILlmPromptService
{
    Task<IReadOnlyList<LlmPromptDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LlmPromptDto> UpdateAsync(string key, UpdateLlmPromptRequestDto request, long? userId, CancellationToken cancellationToken = default);
    Task<LlmPromptDto> ResetAsync(string key, CancellationToken cancellationToken = default);
    Task<LlmPromptTestResultDto> TestAsync(string key, TestLlmPromptRequestDto request, CancellationToken cancellationToken = default);
    // The template to use right now for a key (override or default).
    Task<string> GetTemplateAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> HasOverrideAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class LlmPromptService : ILlmPromptService
{
    private const int MaxTemplateLength = 20_000;

    private readonly IRepository<LlmPrompt> _prompts;
    private readonly ILlmProviderConfigurationRepository _providers;
    private readonly IApiKeyEncryptor _apiKeyEncryptor;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly IUnitOfWork _unitOfWork;

    public LlmPromptService(IRepository<LlmPrompt> prompts, ILlmProviderConfigurationRepository providers, IApiKeyEncryptor apiKeyEncryptor, ILlmClientFactory llmClientFactory, IUnitOfWork unitOfWork)
    {
        _prompts = prompts;
        _providers = providers;
        _apiKeyEncryptor = apiKeyEncryptor;
        _llmClientFactory = llmClientFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<LlmPromptDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var overrides = await _prompts.GetAllAsync(cancellationToken);
        return LlmPromptCatalog.All.Select(d => ToDto(d, overrides.FirstOrDefault(o => o.Key == d.Key))).ToList();
    }

    public async Task<LlmPromptDto> UpdateAsync(string key, UpdateLlmPromptRequestDto request, long? userId, CancellationToken cancellationToken = default)
    {
        var definition = LlmPromptCatalog.Find(key) ?? throw new AppException("Unknown prompt.", 404);
        var template = (request.Template ?? string.Empty).Trim();
        Validate(definition, template);

        var row = await FindAsync(key, cancellationToken);
        if (row is null)
        {
            row = new LlmPrompt(key, template, userId);
            await _prompts.AddAsync(row, cancellationToken);
        }
        else
        {
            row.SetTemplate(template, userId);
            _prompts.Update(row);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(definition, row);
    }

    public async Task<LlmPromptDto> ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = LlmPromptCatalog.Find(key) ?? throw new AppException("Unknown prompt.", 404);
        var row = await FindAsync(key, cancellationToken);
        if (row is not null)
        {
            _prompts.Delete(row);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToDto(definition, null);
    }

    public async Task<LlmPromptTestResultDto> TestAsync(string key, TestLlmPromptRequestDto request, CancellationToken cancellationToken = default)
    {
        var definition = LlmPromptCatalog.Find(key) ?? throw new AppException("Unknown prompt.", 404);
        var template = string.IsNullOrWhiteSpace(request.Template) ? await GetTemplateAsync(key, cancellationToken) : request.Template.Trim();
        Validate(definition, template);

        var rendered = definition.Placeholders.Aggregate(template, (t, p) => t.Replace(p.Name, p.SampleValue));
        var provider = await _providers.GetActiveAsync(cancellationToken);
        if (provider is null)
        {
            return new LlmPromptTestResultDto(false, 0, rendered, null, false, "No AI provider is active. Activate one under AI → Providers & Models.", null);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = _llmClientFactory.GetClient(provider.Provider);
            var reply = await client.CompleteAsync(new LlmCompletionRequest(
                provider.Provider, _apiKeyEncryptor.Decrypt(provider.ApiKeyEncrypted), provider.Model, provider.BaseUrl,
                provider.Temperature, Math.Max(provider.MaxOutputTokens, 1024), rendered), cancellationToken);
            stopwatch.Stop();
            return new LlmPromptTestResultDto(true, stopwatch.ElapsedMilliseconds, rendered, reply, IsJsonWithFields(reply, definition.ExpectedJsonFields), null, provider.Name);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var message = ex is HttpRequestException { StatusCode: { } code } ? $"HTTP {(int)code} ({code}) from the provider." : ex.Message;
            return new LlmPromptTestResultDto(false, stopwatch.ElapsedMilliseconds, rendered, null, false, message, provider.Name);
        }
    }

    public async Task<string> GetTemplateAsync(string key, CancellationToken cancellationToken = default)
    {
        var definition = LlmPromptCatalog.Find(key) ?? throw new InvalidOperationException($"Unknown prompt key {key}.");
        var row = await FindAsync(key, cancellationToken);
        return row?.Template ?? definition.DefaultTemplate;
    }

    public async Task<bool> HasOverrideAsync(string key, CancellationToken cancellationToken = default) =>
        await FindAsync(key, cancellationToken) is not null;

    private async Task<LlmPrompt?> FindAsync(string key, CancellationToken cancellationToken) =>
        (await _prompts.GetAllAsync(cancellationToken)).FirstOrDefault(p => p.Key == key);

    private static void Validate(LlmPromptDefinition definition, string template)
    {
        if (template.Length == 0 || template.Length > MaxTemplateLength)
        {
            throw new AppException($"The prompt must be between 1 and {MaxTemplateLength} characters.", 400);
        }

        var missing = definition.RequiredPlaceholders.Where(p => !template.Contains(p, StringComparison.Ordinal)).ToList();
        if (missing.Count > 0)
        {
            throw new AppException($"The prompt must contain {string.Join(" and ", missing)}.", 400);
        }

        var missingFields = definition.ExpectedJsonFields.Where(f => !template.Contains(f, StringComparison.Ordinal)).ToList();
        if (missingFields.Count > 0)
        {
            throw new AppException($"The prompt must still ask for the JSON field(s) {string.Join(", ", missingFields)}: the app reads them from the reply.", 400);
        }
    }

    private static bool IsJsonWithFields(string reply, IReadOnlyList<string> fields)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(Common.Llm.LlmJsonResponseHelper.StripCodeFence(reply));
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && fields.All(f => doc.RootElement.TryGetProperty(f, out _));
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static LlmPromptDto ToDto(LlmPromptDefinition d, LlmPrompt? row) => new(
        d.Key, d.Name, d.Description, row?.Template ?? d.DefaultTemplate, d.DefaultTemplate, row is not null,
        d.Placeholders, d.RequiredPlaceholders, d.ExpectedJsonFields, row?.UpdatedAt == default ? null : row?.UpdatedAt);
}
