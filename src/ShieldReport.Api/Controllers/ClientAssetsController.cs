using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Authorization;
using ShieldReport.Api.Common;
using ShieldReport.Application.ClientAssets;
using ShieldReport.Application.ClientAssets.Dtos;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class ClientAssetsController : ControllerBase
{
    private readonly IClientAssetService _clientAssetService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateClientAssetRequestDto> _createValidator;
    private readonly IValidator<UpdateClientAssetRequestDto> _updateValidator;

    public ClientAssetsController(
        IClientAssetService clientAssetService,
        ICurrentUserService currentUserService,
        IValidator<CreateClientAssetRequestDto> createValidator,
        IValidator<UpdateClientAssetRequestDto> updateValidator)
    {
        _clientAssetService = clientAssetService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet("clients/{clientId:long}/assets")]
    [ClientScope]
    [Authorize(Policy = Permissions.ClientAssetsRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ClientAssetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListForClient(long clientId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var assets = await _clientAssetService.ListAsync(request, clientId, cancellationToken);
        return Ok(ApiResponse<PagedResult<ClientAssetDto>>.SuccessResponse(assets, "Client assets retrieved successfully"));
    }

    [HttpPost("clients/{clientId:long}/assets")]
    [ClientScope]
    [Authorize(Policy = Permissions.ClientAssetsCreate)]
    [ProducesResponseType(typeof(ApiResponse<ClientAssetDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long clientId, [FromBody] CreateClientAssetRequestDto request, CancellationToken cancellationToken)
    {
        if (request.ClientOrganizationId != clientId)
        {
            return BadRequest(ApiResponse.FailureResponse("Body clientOrganizationId must match the route clientId.", StatusCodes.Status400BadRequest));
        }

        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var asset = await _clientAssetService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = asset.Id }, ApiResponse<ClientAssetDto>.SuccessResponse(asset, "Client asset created successfully", StatusCodes.Status201Created));
    }

    [HttpGet("assets/{id:long}")]
    [Authorize(Policy = Permissions.ClientAssetsRead)]
    [ProducesResponseType(typeof(ApiResponse<ClientAssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var asset = await _clientAssetService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ClientAssetDto>.SuccessResponse(asset, "Client asset retrieved successfully"));
    }

    [HttpPut("assets/{id:long}")]
    [Authorize(Policy = Permissions.ClientAssetsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ClientAssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateClientAssetRequestDto request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var asset = await _clientAssetService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ClientAssetDto>.SuccessResponse(asset, "Client asset updated successfully"));
    }

    [HttpDelete("assets/{id:long}")]
    [Authorize(Policy = Permissions.ClientAssetsDelete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _clientAssetService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Client asset deactivated successfully"));
    }

    [HttpPost("assets/{id:long}/authorize-for-scanning")]
    [Authorize(Policy = Permissions.ClientAssetsAuthorizeForScanning)]
    [ProducesResponseType(typeof(ApiResponse<ClientAssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AuthorizeForScanning(long id, CancellationToken cancellationToken)
    {
        var authorizedByUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var asset = await _clientAssetService.AuthorizeForScanningAsync(id, authorizedByUserId, cancellationToken);
        return Ok(ApiResponse<ClientAssetDto>.SuccessResponse(asset, "Client asset authorized for scanning successfully"));
    }

    [HttpPost("assets/{id:long}/revoke-scan-authorization")]
    [Authorize(Policy = Permissions.ClientAssetsAuthorizeForScanning)]
    [ProducesResponseType(typeof(ApiResponse<ClientAssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeScanAuthorization(long id, CancellationToken cancellationToken)
    {
        var asset = await _clientAssetService.RevokeScanAuthorizationAsync(id, cancellationToken);
        return Ok(ApiResponse<ClientAssetDto>.SuccessResponse(asset, "Client asset scan authorization revoked successfully"));
    }
}
