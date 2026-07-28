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
    private const string PlaceholderText = "<To be completed>";

    private const string PromptTemplate =
        """
        You are a security analyst assistant drafting the narrative sections of a penetration
        test report. You are given a list of findings (title, severity, and explanation) that
        will appear in the report's detailed findings section.

        Findings:
        {FindingsList}

        Using ONLY the data above, respond with a single JSON object (no markdown, no prose
        outside the JSON) matching exactly this shape:
        {"executiveSummary": "<2-4 sentence summary of the overall security posture and engagement objective>", "keyFindings": "<3-6 bullet-style sentences on the recurring themes across findings>", "keyRecommendations": "<3-6 bullet-style sentences on how to remediate the recurring themes>"}

        If the findings list is empty or insufficient to draft meaningful narrative, use
        "Insufficient data to generate this section." for each field rather than speculating.
        """;

    private readonly ILlmProviderConfigurationRepository _llmProviderConfigurationRepository;
    private readonly IApiKeyEncryptor _apiKeyEncryptor;
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly ILogger<ReportNarrativeDraftingService> _logger;

    public ReportNarrativeDraftingService(
        ILlmProviderConfigurationRepository llmProviderConfigurationRepository,
        IApiKeyEncryptor apiKeyEncryptor,
        ILlmClientFactory llmClientFactory,
        ILogger<ReportNarrativeDraftingService> logger)
    {
        _llmProviderConfigurationRepository = llmProviderConfigurationRepository;
        _apiKeyEncryptor = apiKeyEncryptor;
        _llmClientFactory = llmClientFactory;
        _logger = logger;
    }

    public async Task<ReportNarrative> DraftAsync(IReadOnlyList<Vulnerability> includedFindings, CancellationToken cancellationToken = default)
    {
        var placeholder = new ReportNarrative(PlaceholderText, PlaceholderText, PlaceholderText);

        var activeConfiguration = await _llmProviderConfigurationRepository.GetActiveAsync(cancellationToken);
        if (activeConfiguration is null)
        {
            return placeholder;
        }

        var findingsList = new StringBuilder();
        foreach (var finding in includedFindings)
        {
            var explanation = !string.IsNullOrWhiteSpace(finding.AiExplanation)
                ? finding.AiExplanation
                : finding.Description ?? finding.Impact ?? "No description available.";
            findingsList.AppendLine($"- {finding.Title} [{finding.SeverityOverride ?? finding.Severity}]: {explanation}");
        }

        var prompt = PromptTemplate.Replace("{FindingsList}", findingsList.Length > 0 ? findingsList.ToString() : "(none)");
        var apiKey = _apiKeyEncryptor.Decrypt(activeConfiguration.ApiKeyEncrypted);
        var client = _llmClientFactory.GetClient(activeConfiguration.Provider);

        try
        {
            var request = new LlmCompletionRequest(activeConfiguration.Provider, apiKey, activeConfiguration.Model, activeConfiguration.BaseUrl, activeConfiguration.Temperature, activeConfiguration.MaxOutputTokens, prompt);
            var rawContent = await client.CompleteAsync(request, cancellationToken);
            return ParseNarrativeResponse(rawContent, placeholder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Report narrative drafting failed via provider {Provider}.", activeConfiguration.Provider);
            return placeholder;
        }
    }

    private static ReportNarrative ParseNarrativeResponse(string rawContent, ReportNarrative fallback)
    {
        using var document = JsonDocument.Parse(LlmJsonResponseHelper.StripCodeFence(rawContent));
        var root = document.RootElement;

        string ReadField(string name) => root.TryGetProperty(name, out var element) ? element.GetString() ?? fallback.ExecutiveSummary : fallback.ExecutiveSummary;

        return new ReportNarrative(ReadField("executiveSummary"), ReadField("keyFindings"), ReadField("keyRecommendations"));
    }
}
