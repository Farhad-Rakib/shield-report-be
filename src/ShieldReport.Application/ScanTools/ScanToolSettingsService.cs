using System.Text.Json;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ScanTools;

public interface IScanToolSettingsService
{
    Task<IReadOnlyList<ScanToolSettingDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ScanToolSettingDto> UpdateAsync(ScanTool tool, UpdateScanToolSettingRequestDto request, long updatedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailableScanToolDto>> GetAvailableAsync(CancellationToken cancellationToken = default);

    // Settings to snapshot onto a new scan; throws 400 when the tool is disabled.
    Task<ScanToolRuntimeSettings> ResolveForScanAsync(ScanTool tool, CancellationToken cancellationToken = default);

    Task<int> GetMaxParallelAsync(ScanTool tool, CancellationToken cancellationToken = default);
    Task<ScanToolTestResultDto> TestAsync(ScanTool tool, CancellationToken cancellationToken = default);
}

public sealed class ScanToolSettingsService : IScanToolSettingsService
{
    private readonly IScanToolSettingRepository _repository;
    private readonly IScanToolDeploymentSettings _deployment;
    private readonly IScanToolDiagnostics _diagnostics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Domain.Entities.CustomScanTool> _customTools;

    // Custom tools share one worker pool of this size.
    public const int CustomToolMaxParallel = 2;

    public ScanToolSettingsService(IScanToolSettingRepository repository, IScanToolDeploymentSettings deployment, IScanToolDiagnostics diagnostics, IUnitOfWork unitOfWork, IRepository<Domain.Entities.CustomScanTool> customTools)
    {
        _customTools = customTools;
        _repository = repository;
        _deployment = deployment;
        _diagnostics = diagnostics;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ScanToolSettingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAllAsync(cancellationToken);
        return settings.Where(s => s.Tool != ScanTool.Custom).OrderBy(s => (int)s.Tool).Select(ToDto).ToList();
    }

    public async Task<ScanToolSettingDto> UpdateAsync(ScanTool tool, UpdateScanToolSettingRequestDto request, long updatedByUserId, CancellationToken cancellationToken = default)
    {
        var setting = await _repository.GetByToolAsync(tool, cancellationToken)
            ?? throw new AppException("Scan tool not found.", 404);

        var (optionsJson, errors) = tool switch
        {
            ScanTool.Naabu => Serialize(request.Naabu ?? throw MissingOptions(tool), ScanToolOptionsValidator.Validate),
            ScanTool.Nuclei => Serialize(Normalize(request.Nuclei ?? throw MissingOptions(tool)), ScanToolOptionsValidator.Validate),
            ScanTool.Reconftw => Serialize((request.Reconftw ?? throw MissingOptions(tool)).Normalized(), ScanToolOptionsValidator.Validate),
            _ => ("{}", Array.Empty<string>())
        };

        if (errors.Count > 0)
        {
            throw new AppException(string.Join(" ", errors), 400);
        }

        setting.Update(request.IsEnabled, request.TimeoutMinutes, request.MemoryMb, request.Cpus, request.MaxParallel, optionsJson, updatedByUserId);
        _repository.Update(setting);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(setting);
    }

    public async Task<IReadOnlyList<AvailableScanToolDto>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAllAsync(cancellationToken);
        var builtIn = settings.Where(s => s.IsEnabled && s.Tool != ScanTool.Custom).OrderBy(s => (int)s.Tool)
            .Select(s => new AvailableScanToolDto(s.Tool.ToString(), s.Tool.ToClientLabel()));
        var custom = (await _customTools.GetAllAsync(cancellationToken)).Where(t => t.IsEnabled).OrderBy(t => t.ClientLabel)
            .Select(t => new AvailableScanToolDto(ScanTool.Custom.ToString(), t.ClientLabel, t.Id));
        return builtIn.Concat(custom).ToList();
    }

    public async Task<ScanToolRuntimeSettings> ResolveForScanAsync(ScanTool tool, CancellationToken cancellationToken = default)
    {
        if (tool == ScanTool.Custom)
        {
            throw new AppException("Pick a specific custom tool.", 400);
        }

        var setting = await _repository.GetByToolAsync(tool, cancellationToken);
        if (setting is null)
        {
            // No row yet (seeding hasn't run): fall back to the defaults the seeder would insert.
            return new ScanToolRuntimeSettings(tool, _deployment.GetImage(tool), 60, 1024, 1, _deployment.GetPidsLimit(tool),
                tool == ScanTool.Naabu ? NaabuOptions.Default : null,
                tool == ScanTool.Nuclei ? NucleiOptions.Default : null,
                tool == ScanTool.Reconftw ? ReconftwOptions.Default : null);
        }

        if (!setting.IsEnabled)
        {
            throw new AppException($"{tool.ToClientLabel()} is currently disabled by an administrator.", 400);
        }

        return ToRuntime(setting);
    }

    public async Task<int> GetMaxParallelAsync(ScanTool tool, CancellationToken cancellationToken = default)
    {
        if (tool == ScanTool.Custom)
        {
            return CustomToolMaxParallel;
        }

        var setting = await _repository.GetByToolAsync(tool, cancellationToken);
        return setting?.MaxParallel ?? 1;
    }

    public Task<ScanToolTestResultDto> TestAsync(ScanTool tool, CancellationToken cancellationToken = default) =>
        _diagnostics.TestAsync(tool, _deployment.GetImage(tool), cancellationToken);

    private ScanToolRuntimeSettings ToRuntime(ScanToolSetting s) => new(
        s.Tool, _deployment.GetImage(s.Tool), s.TimeoutMinutes, s.MemoryMb, s.Cpus, _deployment.GetPidsLimit(s.Tool),
        s.Tool == ScanTool.Naabu ? Deserialize(s.OptionsJson, NaabuOptions.Default) : null,
        s.Tool == ScanTool.Nuclei ? Deserialize(s.OptionsJson, NucleiOptions.Default) : null,
        s.Tool == ScanTool.Reconftw ? Deserialize(s.OptionsJson, ReconftwOptions.Default).Normalized() : null);

    private ScanToolSettingDto ToDto(ScanToolSetting s)
    {
        var runtime = ToRuntime(s);
        return new ScanToolSettingDto(
            s.Tool.ToString(), s.Tool.ToClientLabel(), s.IsEnabled, runtime.Image, runtime.PidsLimit,
            s.TimeoutMinutes, s.MemoryMb, s.Cpus, s.MaxParallel,
            runtime.Naabu, runtime.Nuclei, runtime.Reconftw,
            s.UpdatedAt == default ? null : s.UpdatedAt);
    }

    private static NucleiOptions Normalize(NucleiOptions o) => o with
    {
        Severities = o.Severities.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList(),
        IncludeTags = o.IncludeTags.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList(),
        ExcludeTags = o.ExcludeTags.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList(),
    };

    private static (string Json, IReadOnlyList<string> Errors) Serialize<T>(T options, Func<T, IReadOnlyList<string>> validate) =>
        (JsonSerializer.Serialize(options), validate(options));

    private static T Deserialize<T>(string json, T fallback)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static AppException MissingOptions(ScanTool tool) => new($"{tool} options are required.", 400);
}
