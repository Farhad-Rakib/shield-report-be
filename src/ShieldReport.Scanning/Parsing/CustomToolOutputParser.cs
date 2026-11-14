using System.Text.RegularExpressions;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Application.ScanTools;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

// Output parser for a registered custom tool, built per scan from its settings snapshot:
//   lines        — every non-empty stdout line becomes one finding at the tool's default severity
//   nuclei-jsonl — the tool emits Nuclei-compatible JSON lines (many ProjectDiscovery tools can)
//   raw          — no findings; the output is only kept on the scan
public sealed partial class CustomToolOutputParser : IScanOutputParser
{
    private const int MaxFindings = 500;
    private const int MaxTitleLength = 200;

    private readonly CustomToolOptions _options;
    private readonly string _fallbackEndpoint;
    private readonly NucleiOutputParser _nuclei = new();

    public CustomToolOutputParser(CustomToolOptions options, string fallbackEndpoint)
    {
        _options = options;
        _fallbackEndpoint = fallbackEndpoint;
    }

    public ScanTool Tool => ScanTool.Custom;

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiRegex();

    [GeneratedRegex(@"^(?:https?://\S+|[a-z0-9.-]+\.[a-z]{2,}(?::\d+)?(?:/\S*)?)", RegexOptions.IgnoreCase)]
    private static partial Regex EndpointRegex();

    public IReadOnlyList<ParsedFinding> Parse(string rawOutput)
    {
        switch (_options.OutputFormat)
        {
            case "nuclei-jsonl":
                return _nuclei.Parse(rawOutput).Take(MaxFindings).ToList();
            case "raw":
                return [];
        }

        var severity = _options.DefaultSeverity.ToLowerInvariant() == "informational" ? "info" : _options.DefaultSeverity.ToLowerInvariant();
        return rawOutput
            .Split('\n')
            .Select(line => AnsiRegex().Replace(line, string.Empty).Trim())
            .Where(line => line.Length > 0)
            .Distinct()
            .Take(MaxFindings)
            .Select(line =>
            {
                var endpointMatch = EndpointRegex().Match(line);
                var title = line.Length > MaxTitleLength ? line[..MaxTitleLength] + "…" : line;
                return new ParsedFinding(
                    $"{_options.ClientLabel}: {title}",
                    endpointMatch.Success ? endpointMatch.Value : _fallbackEndpoint,
                    severity,
                    Description: line,
                    ProofOfConcept: line);
            })
            .ToList();
    }
}
