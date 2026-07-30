using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Common.Interfaces.Services;

// Provider is included so a client shared across vendors (OpenAiCompatibleLlmClient serves both
// OpenAi and OpenRouter) can pick the right default BaseUrl when the admin leaves it blank —
// without it, a blank BaseUrl has no way to distinguish which vendor's default applies.
public sealed record LlmCompletionRequest(LlmProvider Provider, string ApiKey, string Model, string? BaseUrl, decimal Temperature, int MaxOutputTokens, string Prompt);

// Used by VulnerabilityAiExplanationService after it parses the raw completion text — kept here
// alongside the other LLM contracts even though ILlmClient itself no longer produces it directly.
public sealed record LlmExplanationResult(string Explanation, Severity? SuggestedSeverity, string? Recommendation, double? Confidence);

// One implementation per wire format, not per vendor — OpenAI and OpenRouter share the same
// chat-completions shape, so they're the same client with a different BaseUrl. Resolved via
// ILlmClientFactory keyed on LlmProvider, implemented in Infrastructure since these are plain
// HTTP client wrappers (same layering reason as INvdClient).
//
// Returns the raw completion text rather than a parsed domain shape — different callers (per-
// vulnerability explanation vs. report narrative drafting) send different JSON-response contracts
// in their prompts, so response-shape parsing belongs to the Application-layer caller, not to a
// vendor-wire-format client shared across both.
public interface ILlmClient
{
    Task<string> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default);
}

public interface ILlmClientFactory
{
    ILlmClient GetClient(LlmProvider provider);
}
