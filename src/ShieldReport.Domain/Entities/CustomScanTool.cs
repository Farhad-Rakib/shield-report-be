namespace ShieldReport.Domain.Entities;

// An admin-registered scan tool: any Docker image plus an argument template. Runs through the
// same hardened detached-container path as the built-in tools (resource limits, no-new-privileges,
// non-root by default, no host mounts). Arguments are passed as an argv array, never via a shell.
public sealed class CustomScanTool : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    // Shown to clients instead of Name (real tool names must never reach the client side).
    public string ClientLabel { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Image { get; private set; } = string.Empty;
    // JSON array of argument tokens; may contain {target}, {host}, {url}, {port}.
    public string ArgumentsJson { get; private set; } = "[]";
    // "lines": every non-empty stdout line is a finding; "nuclei-jsonl": Nuclei-style JSON lines;
    // "raw": no findings, output only.
    public string OutputFormat { get; private set; } = "lines";
    public string DefaultSeverity { get; private set; } = "Informational";
    public int TimeoutMinutes { get; private set; } = 30;
    public int MemoryMb { get; private set; } = 512;
    public double Cpus { get; private set; } = 1;
    public bool RunAsRoot { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public long? UpdatedByUserId { get; private set; }

    private CustomScanTool()
    {
    }

    public CustomScanTool(string name, string clientLabel, string? description, string image, string argumentsJson, string outputFormat,
        string defaultSeverity, int timeoutMinutes, int memoryMb, double cpus, bool runAsRoot, bool isEnabled, long? userId)
    {
        Update(name, clientLabel, description, image, argumentsJson, outputFormat, defaultSeverity, timeoutMinutes, memoryMb, cpus, runAsRoot, isEnabled, userId);
    }

    public void Update(string name, string clientLabel, string? description, string image, string argumentsJson, string outputFormat,
        string defaultSeverity, int timeoutMinutes, int memoryMb, double cpus, bool runAsRoot, bool isEnabled, long? userId)
    {
        Name = name;
        ClientLabel = clientLabel;
        Description = description;
        Image = image;
        ArgumentsJson = argumentsJson;
        OutputFormat = outputFormat;
        DefaultSeverity = defaultSeverity;
        TimeoutMinutes = timeoutMinutes;
        MemoryMb = memoryMb;
        Cpus = cpus;
        RunAsRoot = runAsRoot;
        IsEnabled = isEnabled;
        UpdatedByUserId = userId;
    }
}
