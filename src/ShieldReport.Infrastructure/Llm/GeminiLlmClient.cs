using System.Net.Http.Json;
using System.Text.Json;
using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Infrastructure.Llm;

public sealed class GeminiLlmClient : ILlmClient
{
    private const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    private readonly HttpClient _httpClient;

    public GeminiLlmClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? DefaultBaseUrl : request.BaseUrl.TrimEnd('/');
        // The API key goes in the x-goog-api-key header, not the ?key= query string: request URLs
        // end up in logs (HttpClient logging), headers do not.
        var requestUrl = $"{baseUrl}/models/{request.Model}:generateContent";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = JsonContent.Create(new
            {
                contents = new[] { new { parts = new[] { new { text = request.Prompt } } } },
                // thinkingBudget: 0 turns off this model family's extended "thinking" pass — left
                // on, it silently burns most of maxOutputTokens on invisible reasoning tokens
                // (confirmed via the raw API: a 1024-token budget produced ~980 thinking tokens
                // and finishReason "MAX_TOKENS", truncating the actual JSON answer mid-string).
                // That's what caused report narrative drafting and vulnerability AI-explanation
                // JSON parsing to fail/silently no-op.
                generationConfig = new { temperature = request.Temperature, maxOutputTokens = request.MaxOutputTokens, thinkingConfig = new { thinkingBudget = 0 } }
            })
        };
        httpRequest.Headers.Add("x-goog-api-key", request.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        // Concatenate every text part rather than assuming parts[0] holds the whole answer — a
        // defensive measure in case a future model variant still splits output across parts even
        // with thinking disabled.
        var parts = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");
        var textBuilder = new System.Text.StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textElement))
            {
                textBuilder.Append(textElement.GetString());
            }
        }

        return textBuilder.ToString();
    }
}
