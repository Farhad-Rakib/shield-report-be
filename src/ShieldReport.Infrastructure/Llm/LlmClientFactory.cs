using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Infrastructure.Llm;

public sealed class LlmClientFactory : ILlmClientFactory
{
    private readonly OpenAiCompatibleLlmClient _openAiCompatibleLlmClient;
    private readonly GeminiLlmClient _geminiLlmClient;

    public LlmClientFactory(OpenAiCompatibleLlmClient openAiCompatibleLlmClient, GeminiLlmClient geminiLlmClient)
    {
        _openAiCompatibleLlmClient = openAiCompatibleLlmClient;
        _geminiLlmClient = geminiLlmClient;
    }

    public ILlmClient GetClient(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.OpenAi or LlmProvider.OpenRouter => _openAiCompatibleLlmClient,
            LlmProvider.Gemini => _geminiLlmClient,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported LLM provider.")
        };
    }
}
