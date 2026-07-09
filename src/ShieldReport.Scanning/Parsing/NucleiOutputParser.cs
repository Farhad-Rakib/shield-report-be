using System.Text;
using System.Text.Json;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

// Nuclei's `-jsonl -irr` output is one JSON object per line, roughly:
// {"template-id":"...","info":{"name":"...","severity":"high","description":"...",
//  "classification":{"cve-id":[...],"cwe-id":[...],"cvss-metrics":"...","cvss-score":7.5},
//  "tags":[...],"reference":[...]},"host":"...","matched-at":"...","matcher-name":"...",
//  "extracted-results":[...],"curl-command":"...","request":"...","response":"..."}
// -irr (include request/response) is what actually populates curl-command/request/response —
// without it those three fields are simply absent from the line.
public sealed class NucleiOutputParser : IScanOutputParser
{
    public ScanTool Tool => ScanTool.Nuclei;

    public IReadOnlyList<ParsedFinding> Parse(string rawOutput)
    {
        var findings = new List<ParsedFinding>();

        foreach (var rawLine in (rawOutput ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                var info = root.TryGetProperty("info", out var infoElement) && infoElement.ValueKind == JsonValueKind.Object
                    ? infoElement
                    : default;
                var hasInfo = info.ValueKind == JsonValueKind.Object;

                var templateId = root.TryGetProperty("template-id", out var templateIdElement) ? templateIdElement.GetString() : null;
                var title = hasInfo && info.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : templateId;

                var severityRaw = hasInfo && info.TryGetProperty("severity", out var severityElement) ? severityElement.GetString() : null;

                var endpoint = root.TryGetProperty("matched-at", out var matchedAtElement) ? matchedAtElement.GetString()
                    : root.TryGetProperty("host", out var hostElement) ? hostElement.GetString() : null;

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(endpoint))
                {
                    continue;
                }

                var classification = hasInfo && info.TryGetProperty("classification", out var classificationElement) && classificationElement.ValueKind == JsonValueKind.Object
                    ? classificationElement
                    : default;
                var hasClassification = classification.ValueKind == JsonValueKind.Object;

                var cveIds = hasClassification ? ReadStringOrArray(classification, "cve-id") : [];
                var cweIds = hasClassification ? ReadStringOrArray(classification, "cwe-id") : [];
                var tags = hasInfo ? ReadStringOrArray(info, "tags") : [];
                var referenceLinks = hasInfo ? ReadStringOrArray(info, "reference") : [];

                var cvssVector = hasClassification && classification.TryGetProperty("cvss-metrics", out var cvssMetricsElement)
                    ? cvssMetricsElement.GetString()
                    : null;
                var cvssScore = hasClassification && classification.TryGetProperty("cvss-score", out var cvssScoreElement) && cvssScoreElement.ValueKind == JsonValueKind.Number
                    ? cvssScoreElement.GetDecimal()
                    : (decimal?)null;

                var rawDescription = hasInfo && info.TryGetProperty("description", out var descriptionElement) ? descriptionElement.GetString() : null;
                var description = BuildDescription(rawDescription, templateId, cveIds, cweIds, tags);

                var matcherName = root.TryGetProperty("matcher-name", out var matcherNameElement) ? matcherNameElement.GetString() : null;
                var extractedResults = ReadStringOrArray(root, "extracted-results");
                var curlCommand = root.TryGetProperty("curl-command", out var curlCommandElement) ? curlCommandElement.GetString() : null;
                var request = root.TryGetProperty("request", out var requestElement) ? requestElement.GetString() : null;
                var response = root.TryGetProperty("response", out var responseElement) ? responseElement.GetString() : null;
                var proofOfConcept = BuildProofOfConcept(matcherName, extractedResults, curlCommand, request, response);

                findings.Add(new ParsedFinding(
                    title!,
                    endpoint!,
                    severityRaw,
                    description,
                    proofOfConcept,
                    cvssVector,
                    cvssScore,
                    referenceLinks));
            }
            catch (JsonException)
            {
                // Skip malformed lines — never let one bad line abort the whole parse.
            }
        }

        return findings;
    }

    // Nuclei's info.tags/info.reference/classification.cve-id/cwe-id are each either a single
    // string or a JSON array of strings depending on how the template author wrote them —
    // handle both shapes rather than assuming one.
    private static IReadOnlyList<string> ReadStringOrArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
        {
            return [];
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()) ? [] : [element.GetString()!],
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray(),
            _ => []
        };
    }

    private static string? BuildDescription(string? rawDescription, string? templateId, IReadOnlyList<string> cveIds, IReadOnlyList<string> cweIds, IReadOnlyList<string> tags)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(rawDescription))
        {
            sb.AppendLine(rawDescription.Trim());
        }

        if (!string.IsNullOrWhiteSpace(templateId) || cveIds.Count > 0 || cweIds.Count > 0 || tags.Count > 0)
        {
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine("Classification:");
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                sb.AppendLine($"- Template: {templateId}");
            }

            if (cveIds.Count > 0)
            {
                sb.AppendLine($"- CVE: {string.Join(", ", cveIds)}");
            }

            if (cweIds.Count > 0)
            {
                sb.AppendLine($"- CWE: {string.Join(", ", cweIds)}");
            }

            if (tags.Count > 0)
            {
                sb.AppendLine($"- Tags: {string.Join(", ", tags)}");
            }
        }

        var result = sb.ToString().Trim();
        return result.Length > 0 ? result : null;
    }

    private static string? BuildProofOfConcept(string? matcherName, IReadOnlyList<string> extractedResults, string? curlCommand, string? request, string? response)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(matcherName))
        {
            sb.AppendLine($"Matcher: {matcherName}");
        }

        if (extractedResults.Count > 0)
        {
            sb.AppendLine($"Extracted: {string.Join(", ", extractedResults)}");
        }

        if (!string.IsNullOrWhiteSpace(curlCommand))
        {
            sb.AppendLine().AppendLine("Reproduction:").AppendLine(curlCommand.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(request))
        {
            sb.AppendLine().AppendLine("Request:").AppendLine(request.Trim());
        }

        if (!string.IsNullOrWhiteSpace(response))
        {
            sb.AppendLine().AppendLine("Response:").AppendLine(response.Trim());
        }

        var result = sb.ToString().Trim();
        return result.Length > 0 ? result : null;
    }
}
