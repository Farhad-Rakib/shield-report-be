namespace ShieldReport.Domain.Enums;

public enum ScanTool
{
    Naabu,
    Nuclei,
    Reconftw,
    // An admin-registered tool (CustomScanTool). The concrete tool is Scan.CustomScanToolId.
    Custom
}

// Real tool identity must not be disclosed to clients — every client-facing surface (reports,
// portal) must render this generic label instead of the raw ScanTool name. Mirrors
// SCAN_TOOL_LABELS in the frontend's domain/models/scan.model.ts.
public static class ScanToolExtensions
{
    public static string ToClientLabel(this ScanTool tool) => tool switch
    {
        ScanTool.Naabu => "Port Checking",
        ScanTool.Nuclei => "Vulnerability Scanning",
        ScanTool.Reconftw => "Reconnaissance",
        ScanTool.Custom => "Custom Check",
        _ => tool.ToString()
    };

    // toolsUsed entries are stored as raw ScanTool enum names (e.g. "Naabu") plus the sentinel
    // "Manual" for hand-created findings, which isn't a ScanTool and passes through unchanged.
    public static string ToClientLabel(this string rawToolName) =>
        Enum.TryParse<ScanTool>(rawToolName, out var tool) ? tool.ToClientLabel() : rawToolName;
}
