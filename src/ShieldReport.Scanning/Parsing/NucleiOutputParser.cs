using System.Text.Json;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

// Nuclei's `-jsonl` output is one JSON object per line:
// {"template-id":"...","info":{"name":"...","severity":"high"},"host":"...","matched-at":"..."}
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

                var info = root.TryGetProperty("info", out var infoElement) ? infoElement : default;
                var title = info.ValueKind == JsonValueKind.Object && info.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : root.TryGetProperty("template-id", out var templateIdElement) ? templateIdElement.GetString() : null;

                var severityRaw = info.ValueKind == JsonValueKind.Object && info.TryGetProperty("severity", out var severityElement)
                    ? severityElement.GetString()
                    : null;

                var endpoint = root.TryGetProperty("matched-at", out var matchedAtElement) ? matchedAtElement.GetString()
                    : root.TryGetProperty("host", out var hostElement) ? hostElement.GetString() : null;

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(endpoint))
                {
                    findings.Add(new ParsedFinding(title!, endpoint!, severityRaw));
                }
            }
            catch (JsonException)
            {
                // Skip malformed lines — never let one bad line abort the whole parse.
            }
        }

        return findings;
    }
}
