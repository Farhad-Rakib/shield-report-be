using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Scans.Parsing;

public interface IScanOutputParserFactory
{
    IScanOutputParser GetParser(ScanTool tool);
}
