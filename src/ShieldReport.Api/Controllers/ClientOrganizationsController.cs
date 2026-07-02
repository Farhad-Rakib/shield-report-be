using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.ClientOrganizations;
using ShieldReport.Application.ClientOrganizations.Dtos;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/client-organizations")]
[Authorize]
public sealed class ClientOrganizationsController : ControllerBase
{
    private readonly IClientOrganizationService _clientOrganizationService;
    private readonly IValidator<CreateClientOrganizationRequestDto> _createValidator;
    private readonly IValidator<UpdateClientOrganizationRequestDto> _updateValidator;

    public ClientOrganizationsController(
        IClientOrganizationService clientOrganizationService,
        IValidator<CreateClientOrganizationRequestDto> createValidator,
        IValidator<UpdateClientOrganizationRequestDto> updateValidator)
    {
        _clientOrganizationService = clientOrganizationService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ClientsRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ClientOrganizationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var organizations = await _clientOrganizationService.ListAsync(request, cancellationToken);
        return Ok(ApiResponse<PagedResult<ClientOrganizationDto>>.SuccessResponse(organizations, "Client organizations retrieved successfully"));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Permissions.ClientsRead)]
    [ProducesResponseType(typeof(ApiResponse<ClientOrganizationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var organization = await _clientOrganizationService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ClientOrganizationDto>.SuccessResponse(organization, "Client organization retrieved successfully"));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ClientsCreate)]
    [ProducesResponseType(typeof(ApiResponse<ClientOrganizationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateClientOrganizationRequestDto request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var organization = await _clientOrganizationService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = organization.Id }, ApiResponse<ClientOrganizationDto>.SuccessResponse(organization, "Client organization created successfully", StatusCodes.Status201Created));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Permissions.ClientsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ClientOrganizationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateClientOrganizationRequestDto request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var organization = await _clientOrganizationService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ClientOrganizationDto>.SuccessResponse(organization, "Client organization updated successfully"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Permissions.ClientsDelete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(long id, CancellationToken cancellationToken)
    {
        await _clientOrganizationService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Client organization deactivated successfully"));
    }
}
