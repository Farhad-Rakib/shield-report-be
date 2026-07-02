using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

// Reconftw's subdomain-enumeration output is one discovered host per line, plain text.
// Recon findings carry no inherent severity — Informational, same rationale as Naabu.
public sealed class ReconftwOutputParser : IScanOutputParser
{
    public ScanTool Tool => ScanTool.Reconftw;

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

            findings.Add(new ParsedFinding("Subdomain discovered", line, "Informational"));
        }

        return findings;
    }
}
