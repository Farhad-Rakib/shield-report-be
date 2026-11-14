using System.Text.Json;
using System.Text.RegularExpressions;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.ScanTools;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.CustomScanTools;

public sealed record CustomScanToolDto(
    long Id, string Name, string ClientLabel, string? Description, string Image, IReadOnlyList<string> Arguments,
    string OutputFormat, string DefaultSeverity, int TimeoutMinutes, int MemoryMb, double Cpus, bool RunAsRoot, bool IsEnabled,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record SaveCustomScanToolRequestDto(
    string Name, string ClientLabel, string? Description, string Image, IReadOnlyList<string> Arguments,
    string OutputFormat, string DefaultSeverity, int TimeoutMinutes, int MemoryMb, double Cpus, bool RunAsRoot, bool IsEnabled);

public sealed record CustomToolPreviewDto(string Command);

public interface ICustomScanToolService
{
    Task<IReadOnlyList<CustomScanToolDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CustomScanToolDto> CreateAsync(SaveCustomScanToolRequestDto request, long? userId, CancellationToken cancellationToken = default);
    Task<CustomScanToolDto> UpdateAsync(long id, SaveCustomScanToolRequestDto request, long? userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<ScanToolTestResultDto> TestAsync(long id, CancellationToken cancellationToken = default);
    // For scan creation: the enabled tool's run snapshot, or 400/404.
    Task<(ScanToolRuntimeSettings Settings, CustomScanTool Tool)> ResolveForScanAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomScanTool>> GetEnabledAsync(CancellationToken cancellationToken = default);
}

public static partial class CustomScanToolRules
{
    public static readonly string[] OutputFormats = ["lines", "nuclei-jsonl", "raw"];
    public static readonly string[] Severities = ["Informational", "Low", "Medium", "High", "Critical"];
    public static readonly string[] Placeholders = ["{target}", "{host}", "{url}", "{port}"];

    // registry/namespace/name[:tag][@sha256:digest], lowercase repository as Docker requires.
    [GeneratedRegex(@"^[a-z0-9]+(?:[._-][a-z0-9]+)*(?::[0-9]{1,5})?(?:/[a-z0-9]+(?:[._-][a-z0-9]+)*)*(?::[A-Za-z0-9_][A-Za-z0-9._-]{0,127})?(?:@sha256:[a-f0-9]{64})?$")]
    public static partial Regex ImageRegex();

    [GeneratedRegex(@"\{[a-z]+\}")]
    public static partial Regex PlaceholderRegex();
}

public sealed class CustomScanToolService : ICustomScanToolService
{
    private readonly IRepository<CustomScanTool> _repository;
    private readonly IScanToolDiagnostics _diagnostics;
    private readonly IScanToolDeploymentSettings _deployment;
    private readonly IUnitOfWork _unitOfWork;

    public CustomScanToolService(IRepository<CustomScanTool> repository, IScanToolDiagnostics diagnostics, IScanToolDeploymentSettings deployment, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _diagnostics = diagnostics;
        _deployment = deployment;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<CustomScanToolDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _repository.GetAllAsync(cancellationToken)).OrderBy(t => t.Name).Select(ToDto).ToList();

    public async Task<IReadOnlyList<CustomScanTool>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        (await _repository.GetAllAsync(cancellationToken)).Where(t => t.IsEnabled).OrderBy(t => t.Name).ToList();

    public async Task<CustomScanToolDto> CreateAsync(SaveCustomScanToolRequestDto request, long? userId, CancellationToken cancellationToken = default)
    {
        var r = await ValidateAsync(request, null, cancellationToken);
        var tool = new CustomScanTool(r.Name, r.ClientLabel, r.Description, r.Image, JsonSerializer.Serialize(r.Arguments), r.OutputFormat,
            r.DefaultSeverity, r.TimeoutMinutes, r.MemoryMb, r.Cpus, r.RunAsRoot, r.IsEnabled, userId);
        await _repository.AddAsync(tool, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tool);
    }

    public async Task<CustomScanToolDto> UpdateAsync(long id, SaveCustomScanToolRequestDto request, long? userId, CancellationToken cancellationToken = default)
    {
        var tool = await _repository.GetByIdAsync(id, cancellationToken) ?? throw new AppException("Custom tool not found.", 404);
        var r = await ValidateAsync(request, id, cancellationToken);
        tool.Update(r.Name, r.ClientLabel, r.Description, r.Image, JsonSerializer.Serialize(r.Arguments), r.OutputFormat,
            r.DefaultSeverity, r.TimeoutMinutes, r.MemoryMb, r.Cpus, r.RunAsRoot, r.IsEnabled, userId);
        _repository.Update(tool);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tool);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var tool = await _repository.GetByIdAsync(id, cancellationToken);
        if (tool is null) return;
        // Scans keep their own settings snapshot, so history stays readable after deletion.
        _repository.Delete(tool);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ScanToolTestResultDto> TestAsync(long id, CancellationToken cancellationToken = default)
    {
        var tool = await _repository.GetByIdAsync(id, cancellationToken) ?? throw new AppException("Custom tool not found.", 404);
        return await _diagnostics.TestImageAsync(tool.Image, cancellationToken);
    }

    public async Task<(ScanToolRuntimeSettings Settings, CustomScanTool Tool)> ResolveForScanAsync(long id, CancellationToken cancellationToken = default)
    {
        var tool = await _repository.GetByIdAsync(id, cancellationToken) ?? throw new AppException("Custom tool not found.", 404);
        if (!tool.IsEnabled)
        {
            throw new AppException($"{tool.ClientLabel} is currently disabled by an administrator.", 400);
        }

        var arguments = JsonSerializer.Deserialize<List<string>>(tool.ArgumentsJson) ?? [];
        var settings = new ScanToolRuntimeSettings(
            ScanTool.Custom, tool.Image, tool.TimeoutMinutes, tool.MemoryMb, tool.Cpus, _deployment.GetPidsLimit(ScanTool.Custom),
            null, null, null,
            new CustomToolOptions(tool.Id, tool.Name, tool.ClientLabel, arguments, tool.OutputFormat, tool.DefaultSeverity, tool.RunAsRoot));
        return (settings, tool);
    }

    private async Task<SaveCustomScanToolRequestDto> ValidateAsync(SaveCustomScanToolRequestDto r, long? excludeId, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var name = (r.Name ?? string.Empty).Trim();
        var label = (r.ClientLabel ?? string.Empty).Trim();
        var image = (r.Image ?? string.Empty).Trim();
        var args = (r.Arguments ?? []).Select(a => a ?? string.Empty).Where(a => a.Length > 0).ToList();

        if (name.Length is 0 or > 100) errors.Add("Name is required (100 characters max).");
        if (label.Length is 0 or > 100) errors.Add("Client label is required (100 characters max). Clients see this instead of the tool name.");
        if (image.Length is 0 or > 300 || !CustomScanToolRules.ImageRegex().IsMatch(image)) errors.Add("Image must be a Docker image reference such as projectdiscovery/httpx:v1.6.8 or ghcr.io/org/tool@sha256:….");
        if (args.Count > 40) errors.Add("At most 40 arguments.");
        if (args.Any(a => a.Length > 500 || a.Contains('\0') || a.Contains('\n'))) errors.Add("Each argument must be one line of at most 500 characters.");
        var unknown = args.SelectMany(a => CustomScanToolRules.PlaceholderRegex().Matches(a).Select(m => m.Value))
            .Where(p => !CustomScanToolRules.Placeholders.Contains(p)).Distinct().ToList();
        if (unknown.Count > 0) errors.Add($"Unknown placeholder(s) {string.Join(", ", unknown)}. Use {string.Join(", ", CustomScanToolRules.Placeholders)}.");
        if (!args.Any(a => CustomScanToolRules.Placeholders.Any(a.Contains))) errors.Add("At least one argument must contain the target ({target}, {host} or {url}).");
        if (!CustomScanToolRules.OutputFormats.Contains(r.OutputFormat)) errors.Add("Output format must be lines, nuclei-jsonl or raw.");
        if (!CustomScanToolRules.Severities.Contains(r.DefaultSeverity)) errors.Add("Default severity must be Informational, Low, Medium, High or Critical.");
        if (r.TimeoutMinutes is < 1 or > 1440) errors.Add("Timeout must be 1–1440 minutes.");
        if (r.MemoryMb is < 128 or > 8192) errors.Add("Memory must be 128–8192 MB.");
        if (r.Cpus is < 0.25 or > 4) errors.Add("CPUs must be 0.25–4.");

        var all = await _repository.GetAllAsync(cancellationToken);
        if (all.Any(t => t.Id != excludeId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))) errors.Add($"A custom tool named \"{name}\" already exists.");
        if (Enum.GetNames<ScanTool>().Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) errors.Add("That name is reserved for a built-in tool.");

        if (errors.Count > 0)
        {
            throw new AppException(string.Join(" ", errors), 400);
        }

        return r with { Name = name, ClientLabel = label, Image = image, Arguments = args, Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim() };
    }

    private static CustomScanToolDto ToDto(CustomScanTool t) => new(
        t.Id, t.Name, t.ClientLabel, t.Description, t.Image, JsonSerializer.Deserialize<List<string>>(t.ArgumentsJson) ?? [],
        t.OutputFormat, t.DefaultSeverity, t.TimeoutMinutes, t.MemoryMb, t.Cpus, t.RunAsRoot, t.IsEnabled,
        t.CreatedAt, t.UpdatedAt == default ? null : t.UpdatedAt);
}
