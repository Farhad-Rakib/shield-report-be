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
        var requestUrl = $"{baseUrl}/models/{request.Model}:generateContent?key={Uri.EscapeDataString(request.ApiKey)}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = JsonContent.Create(new
            {
                contents = new[] { new { parts = new[] { new { text = request.Prompt } } } },
                generationConfig = new { temperature = request.Temperature, maxOutputTokens = request.MaxOutputTokens }
            })
        };

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}
