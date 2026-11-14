using FluentValidation;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ScanTools;

// Exactly one of Naabu/Nuclei/Reconftw is set, matching Tool. Image/PidsLimit are read-only
// (deployment-owned); Label is the fixed client-facing name.
public sealed record ScanToolSettingDto(
    string Tool,
    string Label,
    bool IsEnabled,
    string Image,
    int PidsLimit,
    int TimeoutMinutes,
    int MemoryMb,
    double Cpus,
    int MaxParallel,
    NaabuOptions? Naabu,
    NucleiOptions? Nuclei,
    ReconftwOptions? Reconftw,
    DateTime? UpdatedAt);

public sealed record UpdateScanToolSettingRequestDto(
    bool IsEnabled,
    int TimeoutMinutes,
    int MemoryMb,
    double Cpus,
    int MaxParallel,
    NaabuOptions? Naabu,
    NucleiOptions? Nuclei,
    ReconftwOptions? Reconftw);

// CustomToolId is set for registered custom tools (Tool == "Custom"); Label is always client-safe.
public sealed record AvailableScanToolDto(string Tool, string Label, long? CustomToolId = null);

public sealed record ReconftwModuleDto(string Key, string Group, string Label, string Description, bool Heavy, bool DefaultSelected);

public sealed record ScanToolTestResultDto(bool Success, string Image, string Output);

public sealed class UpdateScanToolSettingRequestDtoValidator : AbstractValidator<UpdateScanToolSettingRequestDto>
{
    public UpdateScanToolSettingRequestDtoValidator()
    {
        RuleFor(x => x.TimeoutMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.MemoryMb).InclusiveBetween(128, 8192);
        RuleFor(x => x.Cpus).InclusiveBetween(0.25, 4);
        // Per tool; the per-client cap of 3 active scans still applies on top.
        RuleFor(x => x.MaxParallel).InclusiveBetween(1, 3);
    }
}

// Runs a harmless version/help command in the tool's image to confirm it is present and starts.
public interface IScanToolDiagnostics
{
    Task<ScanToolTestResultDto> TestAsync(ScanTool tool, string image, CancellationToken cancellationToken = default);

    // Pulls (if needed) and inspects an arbitrary image: used for custom tools.
    Task<ScanToolTestResultDto> TestImageAsync(string image, CancellationToken cancellationToken = default);
}
