using System.Text.RegularExpressions;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

// Reconftw's subdomain-enumeration output is one discovered host per line, plain text, mixed in
// with the tool's own progress/log lines on stdout. Recon findings carry no inherent severity —
// Informational, same rationale as Naabu. A bare hostname line has no whitespace and at least
// one dot; anything else (banner art, "[INF] ..." progress lines, etc.) is skipped rather than
// mis-parsed as a finding.
public sealed partial class ReconftwOutputParser : IScanOutputParser
{
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9\-]{0,62}\.)+[A-Za-z]{2,}$")]
    private static partial Regex BareHostnameRegex();

    public ScanTool Tool => ScanTool.Reconftw;

    public IReadOnlyList<ParsedFinding> Parse(string rawOutput)
    {
        var findings = new List<ParsedFinding>();

        foreach (var rawLine in (rawOutput ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || !BareHostnameRegex().IsMatch(line))
            {
                continue;
            }

            findings.Add(new ParsedFinding(
                $"Subdomain discovered: {line}",
                line,
                "Informational",
                Description: $"Discovered via passive/active subdomain enumeration (reconftw). Host: {line}"));
        }

        return findings;
    }
}
