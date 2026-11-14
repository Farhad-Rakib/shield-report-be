using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Infrastructure.Llm;

public sealed class LlmClientFactory : ILlmClientFactory
{
    private readonly OpenAiCompatibleLlmClient _openAiCompatibleLlmClient;
    private readonly GeminiLlmClient _geminiLlmClient;
    private readonly AnthropicLlmClient _anthropicLlmClient;

    public LlmClientFactory(OpenAiCompatibleLlmClient openAiCompatibleLlmClient, GeminiLlmClient geminiLlmClient, AnthropicLlmClient anthropicLlmClient)
    {
        _openAiCompatibleLlmClient = openAiCompatibleLlmClient;
        _geminiLlmClient = geminiLlmClient;
        _anthropicLlmClient = anthropicLlmClient;
    }

    public ILlmClient GetClient(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.OpenAi or LlmProvider.OpenRouter or LlmProvider.OpenAiCompatible => _openAiCompatibleLlmClient,
            LlmProvider.Anthropic => _anthropicLlmClient,
            LlmProvider.Gemini => _geminiLlmClient,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported LLM provider.")
        };
    }
}
