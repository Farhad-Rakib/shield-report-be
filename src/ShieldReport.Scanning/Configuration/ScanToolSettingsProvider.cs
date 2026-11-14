using System.Globalization;
using Microsoft.Extensions.Configuration;
using ShieldReport.Application.ScanTools;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Scanning.Configuration;

// Deployment-owned scanning configuration: pinned images, process limits and the reconftw output
// volume. Deliberately NOT editable from the UI — choosing the image is choosing what code runs
// on the host Docker daemon. Admin-editable settings (enabled, timeout, memory, CPU, parallelism,
// tool options) live in the scan_tool_settings table (IScanToolSettingsService).
public sealed class ScanToolSettingsProvider : IScanToolDeploymentSettings
{
    private static readonly Dictionary<ScanTool, (string Image, int PidsLimit)> Defaults = new()
    {
        [ScanTool.Naabu] = ("projectdiscovery/naabu:latest", 256),
        [ScanTool.Nuclei] = ("projectdiscovery/nuclei:latest", 512),
        // Lean derived image (docker/reconftw/Dockerfile): the upstream image plus reconftw.lean.cfg.
        [ScanTool.Reconftw] = ("shieldreport/reconftw-lean:latest", 1024),
        // Custom tools carry their own image; only the process limit comes from here.
        [ScanTool.Custom] = (string.Empty, 512),
    };

    private readonly IConfiguration _configuration;

    public ScanToolSettingsProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // Named volume the reconftw container writes into (mounted at /scan-output in the scan
    // container) and where the same volume is mounted inside the API container.
    public string ReconftwOutputVolume => _configuration["Scanning:ReconftwOutputVolume"] ?? "shieldreport-scan-output";

    public string ReconftwOutputPath => _configuration["Scanning:ReconftwOutputPath"] ?? "/app/scan-output";

    public string GetImage(ScanTool tool) =>
        _configuration[$"Scanning:Tools:{tool}:Image"] is { Length: > 0 } image ? image : Defaults[tool].Image;

    public int GetPidsLimit(ScanTool tool) =>
        int.TryParse(_configuration[$"Scanning:Tools:{tool}:PidsLimit"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : Defaults[tool].PidsLimit;
}
