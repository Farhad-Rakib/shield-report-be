using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

// Naabu's default text output is one "host:port" pair per line — open ports aren't
// inherently severities, so every finding lands as Informational (overridden later if a
// follow-on tool like Nuclei flags the same endpoint as vulnerable).
public sealed class NaabuOutputParser : IScanOutputParser
{
    public ScanTool Tool => ScanTool.Naabu;

    public IReadOnlyList<ParsedFinding> Parse(string rawOutput)
    {
        var findings = new List<ParsedFinding>();

        foreach (var rawLine in (rawOutput ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || !line.Contains(':'))
            {
                continue;
            }

            var separatorIndex = line.LastIndexOf(':');
            var host = line[..separatorIndex];
            var port = line[(separatorIndex + 1)..];

            findings.Add(new ParsedFinding($"Open port {port} on {host}", line, "Informational"));
        }

        return findings;
    }
}
