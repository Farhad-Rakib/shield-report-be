using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Common.Llm;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.Reports;

public sealed class ReportNarrativeDraftingService : IReportNarrativeDraftingService
{
    private const string Placeholder = "<To be completed>";



    private readonly ILlmProviderConfigurationRepository _llmProviderConfigurationRepository;
    private readonly IApiKeyEncryptor _apiKeyEncryptor;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly ILogger<ReportNarrativeDraftingService> _logger;
    private readonly ShieldReport.Application.LlmPrompts.ILlmPromptService _prompts;

    public ReportNarrativeDraftingService(
        ILlmProviderConfigurationRepository llmProviderConfigurationRepository,
        IApiKeyEncryptor apiKeyEncryptor,
        ILlmClientFactory llmClientFactory,
        ILogger<ReportNarrativeDraftingService> logger,
        ShieldReport.Application.LlmPrompts.ILlmPromptService prompts)
    {
        _prompts = prompts;
        _llmProviderConfigurationRepository = llmProviderConfigurationRepository;
        _apiKeyEncryptor = apiKeyEncryptor;
        _llmClientFactory = llmClientFactory;
        _logger = logger;
    }

    public string PlaceholderText => Placeholder;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        await _llmProviderConfigurationRepository.GetActiveAsync(cancellationToken) is not null;

    public async Task<ReportNarrative?> DraftAsync(IReadOnlyList<Vulnerability> includedFindings, Engagement? engagement = null, CancellationToken cancellationToken = default)
    {
        var activeConfiguration = await _llmProviderConfigurationRepository.GetActiveAsync(cancellationToken);
        if (activeConfiguration is null)
        {
            return null;
        }

        var findingsList = new StringBuilder();
        foreach (var finding in includedFindings)
        {
            var explanation = !string.IsNullOrWhiteSpace(finding.AiExplanation)
                ? finding.AiExplanation
                : finding.Description ?? finding.Impact ?? "No description available.";
            findingsList.AppendLine($"- {finding.Title} [{finding.SeverityOverride ?? finding.Severity}]: {explanation}");
        }

        var template = await _prompts.GetTemplateAsync(ShieldReport.Application.LlmPrompts.LlmPromptCatalog.ReportNarrative, cancellationToken);
        engagement ??= includedFindings.FirstOrDefault()?.Engagement;
        var prompt = template
            .Replace("{FindingsList}", findingsList.Length > 0 ? findingsList.ToString() : "(none)")
            .Replace("{ClientName}", engagement?.ClientOrganization?.Name ?? "the client")
            .Replace("{EngagementTitle}", engagement?.Title ?? "the engagement")
            .Replace("{TestingType}", engagement?.TestingType.ToString() ?? "White");
        var apiKey = _apiKeyEncryptor.Decrypt(activeConfiguration.ApiKeyEncrypted);
        var client = _llmClientFactory.GetClient(activeConfiguration.Provider);

        // Narrative JSON for many findings needs room; a small per-finding budget would truncate it.
        var maxTokens = Math.Max(activeConfiguration.MaxOutputTokens, 2048);
        var request = new LlmCompletionRequest(activeConfiguration.Provider, apiKey, activeConfiguration.Model, activeConfiguration.BaseUrl, activeConfiguration.Temperature, maxTokens, prompt);

        _logger.LogInformation("Drafting report narrative with {Provider} ({Name}) for {Count} findings.", activeConfiguration.Provider, activeConfiguration.Name, includedFindings.Count);
        var rawContent = await client.CompleteAsync(request, cancellationToken);
        try
        {
            return ParseNarrativeResponse(rawContent);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The AI provider returned text that isn't the expected JSON.", ex);
        }
    }

    private static ReportNarrative ParseNarrativeResponse(string rawContent)
    {
        using var document = JsonDocument.Parse(LlmJsonResponseHelper.StripCodeFence(rawContent));
        var root = document.RootElement;

        string ReadField(string name) => root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? Placeholder
            : Placeholder;

        return new ReportNarrative(ReadField("executiveSummary"), ReadField("keyFindings"), ReadField("keyRecommendations"));
    }
}
