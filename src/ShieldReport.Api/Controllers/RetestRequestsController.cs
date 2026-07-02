using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.RetestRequests;
using ShieldReport.Application.RetestRequests.Dtos;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vulnerabilities/{vulnerabilityId:long}/retest")]
[Authorize]
public sealed class RetestRequestsController : ControllerBase
{
    private readonly IRetestRequestService _retestRequestService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateRetestRequestDto> _createValidator;
    private readonly IValidator<ResolveRetestRequestDto> _resolveValidator;

    public RetestRequestsController(
        IRetestRequestService retestRequestService,
        ICurrentUserService currentUserService,
        IValidator<CreateRetestRequestDto> createValidator,
        IValidator<ResolveRetestRequestDto> resolveValidator)
    {
        _retestRequestService = retestRequestService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
        _resolveValidator = resolveValidator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.RetestRequestsRead)]
    [ProducesResponseType(typeof(ApiResponse<RetestRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(long vulnerabilityId, CancellationToken cancellationToken)
    {
        var retest = await _retestRequestService.GetCurrentAsync(vulnerabilityId, cancellationToken);
        return Ok(ApiResponse<RetestRequestDto?>.SuccessResponse(retest, "Retest request retrieved successfully"));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.RetestRequestsCreate)]
    [ProducesResponseType(typeof(ApiResponse<RetestRequestDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long vulnerabilityId, [FromBody] CreateRetestRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var requestedByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);

        // CLIENT_ADMIN is gated on patch status inside the service; PLATFORM_ADMIN (Admin/
        // SuperAdmin) is ungated, covering QA-driven retests raised ahead of a client update.
        var isPlatformAdmin = User.IsInRole(SystemRoles.SuperAdmin) || User.IsInRole(SystemRoles.Admin);

        var retest = await _retestRequestService.CreateAsync(vulnerabilityId, request, requestedByUserId, isPlatformAdmin, cancellationToken);
        return CreatedAtAction(nameof(GetCurrent), new { vulnerabilityId }, ApiResponse<RetestRequestDto>.SuccessResponse(retest, "Retest request created successfully", StatusCodes.Status201Created));
    }

    [HttpPatch]
    [Authorize(Policy = Permissions.RetestRequestsResolve)]
    [ProducesResponseType(typeof(ApiResponse<RetestRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resolve(long vulnerabilityId, [FromBody] ResolveRetestRequestDto request, CancellationToken cancellationToken)
    {
        await _resolveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var resolvedByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);

        // Verified-closed freezes DaysOpen permanently, so it requires a step-up MFA session
        // (amr contains "otp") — Reopened does not, per API-DESIGN-PentestOps.md. The single
        // PATCH route serves both outcomes, so this check is conditional rather than a blanket
        // [RequiresMfa] on the action.
        if (request.Outcome == RetestRequestStatus.VerifiedClosed)
        {
            var hasMfaAmr = User.Claims.Any(c => c.Type == "amr" && c.Value == "otp");
            if (!hasMfaAmr)
            {
                return Unauthorized(ApiResponse.FailureResponse("Verifying a retest as closed requires a multi-factor-authenticated session.", StatusCodes.Status401Unauthorized));
            }
        }

        var retest = await _retestRequestService.ResolveAsync(vulnerabilityId, request, resolvedByUserId, cancellationToken);
        return Ok(ApiResponse<RetestRequestDto>.SuccessResponse(retest, "Retest request resolved successfully"));
    }
}
