using System.Text.Json;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ScanTools;

public static class ScanToolLabels
{
    // Client-safe label for a scan: the built-in label, or the custom tool's client label from the
    // scan's settings snapshot (the custom tool itself may have been edited or deleted since).
    public static string ClientLabel(Scan scan)
    {
        if (scan.Tool == ScanTool.Custom && Snapshot(scan)?.Custom is { } custom)
        {
            return custom.ClientLabel;
        }

        return scan.Tool.ToClientLabel();
    }

    public static ScanToolRuntimeSettings? Snapshot(Scan scan)
    {
        if (string.IsNullOrWhiteSpace(scan.ToolSettingsJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<ScanToolRuntimeSettings>(scan.ToolSettingsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
