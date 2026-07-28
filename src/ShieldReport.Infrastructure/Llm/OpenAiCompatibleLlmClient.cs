using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Infrastructure.Llm;

// Shared by both OpenAi and OpenRouter — OpenRouter's API is wire-compatible with OpenAI's
// chat-completions endpoint, so only the base URL and API key differ; not worth two classes.
public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private const string OpenAiDefaultBaseUrl = "https://api.openai.com/v1";
    private const string OpenRouterDefaultBaseUrl = "https://openrouter.ai/api/v1";

    private readonly HttpClient _httpClient;

    public OpenAiCompatibleLlmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var defaultBaseUrl = request.Provider == LlmProvider.OpenRouter ? OpenRouterDefaultBaseUrl : OpenAiDefaultBaseUrl;
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? defaultBaseUrl : request.BaseUrl.TrimEnd('/');

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            model = request.Model,
            temperature = request.Temperature,
            max_tokens = request.MaxOutputTokens,
            messages = new[] { new { role = "user", content = request.Prompt } }
        });

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
