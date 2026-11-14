using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Audit;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.ScanAuthorizations;
using ShieldReport.Application.ScanAuthorizations.Dtos;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

// Scan authorization records per asset: who approved scanning, the client's contact, evidence,
// validity window and allowed tools. Read: staff (Admin/Pentester); manage: Admin/SuperAdmin.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class ScanAuthorizationsController : ControllerBase
{
    private const long MaxEvidenceSizeBytes = 25L * 1024 * 1024;

    private readonly IScanAuthorizationService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;
    private readonly IValidator<CreateScanAuthorizationRequestDto> _createValidator;
    private readonly IValidator<RevokeScanAuthorizationRequestDto> _revokeValidator;

    public ScanAuthorizationsController(
        IScanAuthorizationService service,
        ICurrentUserService currentUserService,
        IAuditLogger auditLogger,
        IValidator<CreateScanAuthorizationRequestDto> createValidator,
        IValidator<RevokeScanAuthorizationRequestDto> revokeValidator)
    {
        _service = service;
        _currentUserService = currentUserService;
        _auditLogger = auditLogger;
        _createValidator = createValidator;
        _revokeValidator = revokeValidator;
    }

    [HttpGet("assets/{assetId:long}/scan-authorizations")]
    [Authorize(Policy = Permissions.ScanAuthorizationsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ScanAuthorizationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(long assetId, CancellationToken cancellationToken)
    {
        var authorizations = await _service.ListAsync(assetId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ScanAuthorizationDto>>.SuccessResponse(authorizations, "Scan authorizations retrieved successfully"));
    }

    [HttpPost("assets/{assetId:long}/scan-authorizations")]
    [Authorize(Policy = Permissions.ScanAuthorizationsManage)]
    [ProducesResponseType(typeof(ApiResponse<ScanAuthorizationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long assetId, [FromBody] CreateScanAuthorizationRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var userId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var authorization = await _service.CreateAsync(assetId, request, userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ScanAuthorizationDto>.SuccessResponse(authorization, "Scan authorization recorded successfully", StatusCodes.Status201Created));
    }

    [HttpPost("scan-authorizations/{id:long}/evidence")]
    [Authorize(Policy = Permissions.ScanAuthorizationsManage)]
    [RequestSizeLimit(MaxEvidenceSizeBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ScanAuthorizationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadEvidence(long id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse.FailureResponse("A non-empty file is required.", StatusCodes.Status400BadRequest));
        }

        if (file.Length > MaxEvidenceSizeBytes)
        {
            return BadRequest(ApiResponse.FailureResponse("File exceeds the 25MB upload limit.", StatusCodes.Status400BadRequest));
        }

        await using var stream = file.OpenReadStream();
        var authorization = await _service.UploadEvidenceAsync(id, file.FileName, file.ContentType, stream, cancellationToken);
        return Ok(ApiResponse<ScanAuthorizationDto>.SuccessResponse(authorization, "Evidence uploaded successfully"));
    }

    [HttpGet("scan-authorizations/{id:long}/evidence")]
    [Authorize(Policy = Permissions.ScanAuthorizationsRead)]
    public async Task<IActionResult> DownloadEvidence(long id, CancellationToken cancellationToken)
    {
        var (content, fileName, contentType) = await _service.GetEvidenceAsync(id, cancellationToken);
        await _auditLogger.LogAsync(AuditActions.Downloaded, "ScanAuthorizationEvidence", id.ToString(), new { fileName }, cancellationToken: cancellationToken);
        return File(content, contentType, fileName);
    }

    [HttpPost("scan-authorizations/{id:long}/revoke")]
    [Authorize(Policy = Permissions.ScanAuthorizationsManage)]
    [ProducesResponseType(typeof(ApiResponse<ScanAuthorizationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Revoke(long id, [FromBody] RevokeScanAuthorizationRequestDto request, CancellationToken cancellationToken)
    {
        await _revokeValidator.ValidateAndThrowAsync(request, cancellationToken);
        var userId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var authorization = await _service.RevokeAsync(id, request.Reason, userId, cancellationToken);
        return Ok(ApiResponse<ScanAuthorizationDto>.SuccessResponse(authorization, "Scan authorization revoked successfully"));
    }
}
