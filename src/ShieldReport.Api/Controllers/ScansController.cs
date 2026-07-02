using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Scans;
using ShieldReport.Application.Scans.Dtos;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class ScansController : ControllerBase
{
    private readonly IScanService _scanService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateScanRequestDto> _createValidator;

    public ScansController(IScanService scanService, ICurrentUserService currentUserService, IValidator<CreateScanRequestDto> createValidator)
    {
        _scanService = scanService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
    }

    [HttpGet("scans")]
    [Authorize(Policy = Permissions.ScansRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ScanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, [FromQuery] long? clientOrganizationId, [FromQuery] long? clientAssetId, [FromQuery] ScanStatus? status, CancellationToken cancellationToken)
    {
        var scans = await _scanService.ListAsync(request, clientOrganizationId, clientAssetId, status, cancellationToken);
        return Ok(ApiResponse<PagedResult<ScanDto>>.SuccessResponse(scans, "Scans retrieved successfully"));
    }

    [HttpGet("scans/{publicId:guid}")]
    [Authorize(Policy = Permissions.ScansRead)]
    [ProducesResponseType(typeof(ApiResponse<ScanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPublicId(Guid publicId, CancellationToken cancellationToken)
    {
        var scan = await _scanService.GetByPublicIdAsync(publicId, cancellationToken);
        return Ok(ApiResponse<ScanDto>.SuccessResponse(scan, "Scan retrieved successfully"));
    }

    [HttpGet("scans/{publicId:guid}/raw-findings")]
    [Authorize(Policy = Permissions.ScansRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanFindingRawDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRawFindings(Guid publicId, CancellationToken cancellationToken)
    {
        var findings = await _scanService.GetRawFindingsAsync(publicId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ScanFindingRawDto>>.SuccessResponse(findings, "Raw findings retrieved successfully"));
    }

    [HttpPost("assets/{assetId:long}/scans")]
    [Authorize(Policy = Permissions.ScansCreate)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanDto>>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long assetId, [FromBody] CreateScanRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var requestedByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var callerIsPentester = _currentUserService.Roles.Contains(SystemRoles.Pentester);

        var scans = await _scanService.CreateAsync(assetId, request, requestedByUserId, callerIsPentester, _currentUserService.IsClientPortalUser, cancellationToken);
        return CreatedAtAction(nameof(List), null, ApiResponse<IReadOnlyList<ScanDto>>.SuccessResponse(scans, "Scan(s) queued successfully", StatusCodes.Status201Created));
    }

    [HttpPost("scans/{publicId:guid}/cancel")]
    [Authorize(Policy = Permissions.ScansCancel)]
    [ProducesResponseType(typeof(ApiResponse<ScanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid publicId, CancellationToken cancellationToken)
    {
        var cancelledByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var scan = await _scanService.CancelAsync(publicId, cancelledByUserId, cancellationToken);
        return Ok(ApiResponse<ScanDto>.SuccessResponse(scan, "Scan cancelled successfully"));
    }
}
