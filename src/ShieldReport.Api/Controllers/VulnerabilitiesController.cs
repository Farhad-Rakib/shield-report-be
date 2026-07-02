using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Security;
using ShieldReport.Application.Vulnerabilities;
using ShieldReport.Application.Vulnerabilities.Dtos;
using ShieldReport.Domain.Enums;
using Severity = ShieldReport.Domain.Enums.Severity;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vulnerabilities")]
[Authorize]
public sealed class VulnerabilitiesController : ControllerBase
{
    private readonly IVulnerabilityService _vulnerabilityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateVulnerabilityRequestDto> _createValidator;
    private readonly IValidator<UpdateVulnerabilityRequestDto> _updateValidator;
    private readonly IValidator<UpdatePatchStatusRequestDto> _patchStatusValidator;

    public VulnerabilitiesController(
        IVulnerabilityService vulnerabilityService,
        ICurrentUserService currentUserService,
        IValidator<CreateVulnerabilityRequestDto> createValidator,
        IValidator<UpdateVulnerabilityRequestDto> updateValidator,
        IValidator<UpdatePatchStatusRequestDto> patchStatusValidator)
    {
        _vulnerabilityService = vulnerabilityService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _patchStatusValidator = patchStatusValidator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.VulnerabilitiesRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VulnerabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PagedRequest request,
        [FromQuery] long? clientOrganizationId,
        [FromQuery] long? scanId,
        [FromQuery] Severity? severity,
        [FromQuery] PatchStatus? patchStatus,
        CancellationToken cancellationToken)
    {
        var vulnerabilities = await _vulnerabilityService.ListAsync(request, clientOrganizationId, scanId, severity, patchStatus, cancellationToken);
        return Ok(ApiResponse<PagedResult<VulnerabilityDto>>.SuccessResponse(vulnerabilities, "Vulnerabilities retrieved successfully"));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Permissions.VulnerabilitiesRead)]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var vulnerability = await _vulnerabilityService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<VulnerabilityDto>.SuccessResponse(vulnerability, "Vulnerability retrieved successfully"));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.VulnerabilitiesCreate)]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateVulnerabilityRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var currentUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var callerIsPentester = _currentUserService.Roles.Contains(SystemRoles.Pentester);

        var vulnerability = await _vulnerabilityService.CreateAsync(request, currentUserId, callerIsPentester, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = vulnerability.Id }, ApiResponse<VulnerabilityDto>.SuccessResponse(vulnerability, "Vulnerability created successfully", StatusCodes.Status201Created));
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = Permissions.VulnerabilitiesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateVulnerabilityRequestDto request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var vulnerability = await _vulnerabilityService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<VulnerabilityDto>.SuccessResponse(vulnerability, "Vulnerability updated successfully"));
    }

    [HttpPatch("{id:long}/patch-status")]
    [Authorize(Policy = Permissions.VulnerabilitiesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePatchStatus(long id, [FromBody] UpdatePatchStatusRequestDto request, CancellationToken cancellationToken)
    {
        await _patchStatusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var vulnerability = await _vulnerabilityService.UpdatePatchStatusAsync(id, request.PatchStatus, cancellationToken);
        return Ok(ApiResponse<VulnerabilityDto>.SuccessResponse(vulnerability, "Patch status updated successfully"));
    }

    [HttpPatch("{id:long}/report-included")]
    [Authorize(Policy = Permissions.VulnerabilitiesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<VulnerabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateReportIncluded(long id, [FromBody] UpdateReportIncludedRequestDto request, CancellationToken cancellationToken)
    {
        var vulnerability = await _vulnerabilityService.UpdateReportIncludedAsync(id, request.ReportIncluded, cancellationToken);
        return Ok(ApiResponse<VulnerabilityDto>.SuccessResponse(vulnerability, "Report-included flag updated successfully"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Permissions.VulnerabilitiesDelete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _vulnerabilityService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Vulnerability deleted successfully"));
    }

    // Manual-entry assist only — gated to whoever can create a vulnerability (PENTESTER/staff).
    [HttpPost("fetch-intelligence")]
    [Authorize(Policy = Permissions.VulnerabilitiesCreate)]
    [ProducesResponseType(typeof(ApiResponse<FetchIntelligenceResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FetchIntelligence([FromBody] FetchIntelligenceRequestDto request, CancellationToken cancellationToken)
    {
        var intelligence = await _vulnerabilityService.FetchIntelligenceAsync(request.CveId, cancellationToken);
        return Ok(ApiResponse<FetchIntelligenceResponseDto>.SuccessResponse(intelligence, "Intelligence retrieved successfully"));
    }
}
