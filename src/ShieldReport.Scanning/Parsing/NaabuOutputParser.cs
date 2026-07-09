using System.Text.Json;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

// Naabu's `-json` output is one JSON object per line, e.g.
// {"host":"example.com","ip":"93.184.216.34","port":443,"protocol":"tcp","tls":true}. Open
// ports aren't inherently severities, so every finding still lands as Informational
// (overridden later if a follow-on tool like Nuclei flags the same endpoint as vulnerable).
// Falls back to the older plain "host:port" text line if a row isn't valid JSON, so this still
// tolerates a naabu invocation that dropped the -json flag.
public sealed class NaabuOutputParser : IScanOutputParser
{
    public ScanTool Tool => ScanTool.Naabu;

    public IReadOnlyList<ParsedFinding> Parse(string rawOutput)
    {
        var findings = new List<ParsedFinding>();

        foreach (var rawLine in (rawOutput ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var finding = line[0] == '{' ? TryParseJsonLine(line) : TryParseTextLine(line);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static ParsedFinding? TryParseJsonLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            var host = root.TryGetProperty("host", out var hostElement) && hostElement.ValueKind == JsonValueKind.String
                ? hostElement.GetString()
                : null;
            var ip = root.TryGetProperty("ip", out var ipElement) && ipElement.ValueKind == JsonValueKind.String ? ipElement.GetString() : null;
            var displayHost = !string.IsNullOrWhiteSpace(host) ? host : ip;

            if (string.IsNullOrWhiteSpace(displayHost) || !root.TryGetProperty("port", out var portElement))
            {
                return null;
            }

            var port = portElement.ValueKind == JsonValueKind.Number ? portElement.GetInt32().ToString() : portElement.GetString();
            if (string.IsNullOrWhiteSpace(port))
            {
                return null;
            }

            var protocol = root.TryGetProperty("protocol", out var protocolElement) && protocolElement.ValueKind == JsonValueKind.String
                ? protocolElement.GetString()
                : "tcp";
            var isTls = root.TryGetProperty("tls", out var tlsElement) && tlsElement.ValueKind == JsonValueKind.True;

            var title = $"Open port {port}/{protocol} on {displayHost}";
            var endpoint = $"{displayHost}:{port}";
            var descriptionParts = new List<string> { $"Protocol: {protocol}" };
            if (!string.IsNullOrWhiteSpace(ip) && ip != displayHost)
            {
                descriptionParts.Add($"IP: {ip}");
            }

            if (isTls)
            {
                descriptionParts.Add("TLS: enabled");
            }

            return new ParsedFinding(title, endpoint, "Informational", Description: string.Join(" | ", descriptionParts));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ParsedFinding? TryParseTextLine(string line)
    {
        if (!line.Contains(':'))
        {
            return null;
        }

        var separatorIndex = line.LastIndexOf(':');
        var host = line[..separatorIndex];
        var port = line[(separatorIndex + 1)..];

        return new ParsedFinding($"Open port {port} on {host}", line, "Informational");
    }
}
