using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Scans.Parsing;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Parsing;

public sealed class ScanOutputParserFactory : IScanOutputParserFactory
{
    private readonly Dictionary<ScanTool, IScanOutputParser> _parsersByTool;

    public ScanOutputParserFactory(IEnumerable<IScanOutputParser> parsers)
    {
        _parsersByTool = parsers.ToDictionary(p => p.Tool);
    }

    public IScanOutputParser GetParser(ScanTool tool)
    {
        return _parsersByTool.TryGetValue(tool, out var parser)
            ? parser
            : throw new AppException($"No output parser registered for scan tool '{tool}'.", 500);
    }
}
