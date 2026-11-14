using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.ScanTools;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Api.Controllers;

// Scan Tools settings (Configuration → Scan Tools). Images are deployment-owned and read-only here.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/scan-tools")]
[Authorize]
public sealed class ScanToolsController : ControllerBase
{
    private readonly IScanToolSettingsService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpdateScanToolSettingRequestDto> _updateValidator;

    public ScanToolsController(IScanToolSettingsService service, ICurrentUserService currentUserService, IValidator<UpdateScanToolSettingRequestDto> updateValidator)
    {
        _service = service;
        _currentUserService = currentUserService;
        _updateValidator = updateValidator;
    }

    // Enabled tools with their client-safe labels — drives every tool picker (staff and portal).
    [HttpGet("available")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AvailableScanToolDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Available(CancellationToken cancellationToken)
    {
        var tools = await _service.GetAvailableAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AvailableScanToolDto>>.SuccessResponse(tools, "Available scan tools retrieved successfully"));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ScanToolsManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanToolSettingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var settings = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ScanToolSettingDto>>.SuccessResponse(settings, "Scan tool settings retrieved successfully"));
    }

    // The reconnaissance module catalogue behind the Scan Tools checkboxes (config flags omitted).
    [HttpGet("reconftw/modules")]
    [Authorize(Policy = Permissions.ScanToolsManage)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReconftwModuleDto>>), StatusCodes.Status200OK)]
    public IActionResult ReconftwModuleCatalogue()
    {
        var modules = ReconftwModules.All
            .Select(m => new ReconftwModuleDto(m.Key, m.Group, m.Label, m.Description, m.Heavy, ReconftwModules.DefaultSelection.Contains(m.Key)))
            .ToList();
        return Ok(ApiResponse<IReadOnlyList<ReconftwModuleDto>>.SuccessResponse(modules, "Reconnaissance modules retrieved successfully"));
    }

    [HttpPut("{tool}")]
    [Authorize(Policy = Permissions.ScanToolsManage)]
    [ProducesResponseType(typeof(ApiResponse<ScanToolSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(ScanTool tool, [FromBody] UpdateScanToolSettingRequestDto request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var userId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var setting = await _service.UpdateAsync(tool, request, userId, cancellationToken);
        return Ok(ApiResponse<ScanToolSettingDto>.SuccessResponse(setting, "Scan tool settings saved"));
    }

    [HttpPost("{tool}/test")]
    [Authorize(Policy = Permissions.ScanToolsManage)]
    [ProducesResponseType(typeof(ApiResponse<ScanToolTestResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Test(ScanTool tool, CancellationToken cancellationToken)
    {
        var result = await _service.TestAsync(tool, cancellationToken);
        return Ok(ApiResponse<ScanToolTestResultDto>.SuccessResponse(result, result.Success ? "Tool responded" : "Tool test failed"));
    }
}
