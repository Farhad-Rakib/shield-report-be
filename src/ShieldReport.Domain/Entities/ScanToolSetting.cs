using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

// Admin-editable runtime settings for one scan tool (one row per ScanTool, seeded at startup).
// Images and process limits are deployment-owned and intentionally not stored here.
public sealed class ScanToolSetting : BaseEntity
{
    public ScanTool Tool { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public int TimeoutMinutes { get; private set; }
    public int MemoryMb { get; private set; }
    public double Cpus { get; private set; }
    public int MaxParallel { get; private set; } = 1;
    // Tool-specific options (NaabuOptions / NucleiOptions / ReconftwOptions), validated by the
    // application layer before being stored.
    public string OptionsJson { get; private set; } = "{}";

    private ScanToolSetting()
    {
    }

    public ScanToolSetting(ScanTool tool, int timeoutMinutes, int memoryMb, double cpus, string optionsJson)
    {
        Tool = tool;
        TimeoutMinutes = timeoutMinutes;
        MemoryMb = memoryMb;
        Cpus = cpus;
        OptionsJson = optionsJson;
    }

    public void Update(bool isEnabled, int timeoutMinutes, int memoryMb, double cpus, int maxParallel, string optionsJson, long updatedByUserId)
    {
        IsEnabled = isEnabled;
        TimeoutMinutes = timeoutMinutes;
        MemoryMb = memoryMb;
        Cpus = cpus;
        MaxParallel = maxParallel;
        OptionsJson = optionsJson;
        UpdatedBy = updatedByUserId;
        UpdatedAt = DateTime.UtcNow;
    }
}
