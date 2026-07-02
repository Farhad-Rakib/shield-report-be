using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans.Parsing;

public interface IScanOutputParser
{
    ScanTool Tool { get; }

    IReadOnlyList<ParsedFinding> Parse(string rawOutput);
}
