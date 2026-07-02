using System.Text.Json;
using Microsoft.Extensions.Options;
using ShieldReport.Application.Common.Interfaces.Services;

namespace ShieldReport.Infrastructure.Nvd;

public sealed class NvdClient : INvdClient
{
    private readonly HttpClient _httpClient;
    private readonly NvdOptions _options;

    public NvdClient(HttpClient httpClient, IOptions<NvdOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<NvdCveResult?> LookupAsync(string cveId, CancellationToken cancellationToken = default)
    {
        var requestUrl = $"{_options.BaseUrl}?cveId={Uri.EscapeDataString(cveId)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("apiKey", _options.ApiKey);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var vulnerabilities = document.RootElement.TryGetProperty("vulnerabilities", out var vulnerabilitiesElement)
            ? vulnerabilitiesElement
            : default;

        if (vulnerabilities.ValueKind != JsonValueKind.Array || vulnerabilities.GetArrayLength() == 0)
        {
            return null;
        }

        var cve = vulnerabilities[0].GetProperty("cve");

        var description = ExtractEnglishDescription(cve);

        var (cvssScore, cvssVector) = ExtractCvss(cve);

        return new NvdCveResult(cveId, description, cvssScore, cvssVector);
    }

    private static string? ExtractEnglishDescription(JsonElement cve)
    {
        if (!cve.TryGetProperty("descriptions", out var descriptionsElement) || descriptionsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var description in descriptionsElement.EnumerateArray())
        {
            if (description.TryGetProperty("lang", out var lang) && lang.GetString() == "en"
                && description.TryGetProperty("value", out var value))
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static (decimal? Score, string? Vector) ExtractCvss(JsonElement cve)
    {
        if (!cve.TryGetProperty("metrics", out var metrics))
        {
            return (null, null);
        }

        foreach (var metricKey in new[] { "cvssMetricV31", "cvssMetricV30", "cvssMetricV2" })
        {
            if (metrics.TryGetProperty(metricKey, out var metricArray) && metricArray.ValueKind == JsonValueKind.Array && metricArray.GetArrayLength() > 0)
            {
                var cvssData = metricArray[0].GetProperty("cvssData");
                var score = cvssData.TryGetProperty("baseScore", out var scoreElement) ? scoreElement.GetDecimal() : (decimal?)null;
                var vector = cvssData.TryGetProperty("vectorString", out var vectorElement) ? vectorElement.GetString() : null;
                return (score, vector);
            }
        }

        return (null, null);
    }
}
