using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.LlmProviderConfigurations;
using ShieldReport.Application.LlmProviderConfigurations.Dtos;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class LlmProviderConfigurationsController : ControllerBase
{
    private readonly ILlmProviderConfigurationService _service;
    private readonly ICurrentUserService _currentUserService;

    public LlmProviderConfigurationsController(ILlmProviderConfigurationService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LlmProviderConfigurationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var configurations = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<LlmProviderConfigurationDto>>.SuccessResponse(configurations, "LLM provider configurations retrieved successfully"));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsCreate)]
    [ProducesResponseType(typeof(ApiResponse<LlmProviderConfigurationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateLlmProviderConfigurationRequestDto request, CancellationToken cancellationToken)
    {
        var configuration = await _service.CreateAsync(request, _currentUserService.UserId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<LlmProviderConfigurationDto>.SuccessResponse(configuration, "LLM provider configuration created successfully", StatusCodes.Status201Created));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<LlmProviderConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateLlmProviderConfigurationRequestDto request, CancellationToken cancellationToken)
    {
        var configuration = await _service.UpdateAsync(id, request, _currentUserService.UserId, cancellationToken);
        return Ok(ApiResponse<LlmProviderConfigurationDto>.SuccessResponse(configuration, "LLM provider configuration updated successfully"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsDelete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("LLM provider configuration deleted successfully"));
    }

    [HttpPost("{id:long}/activate")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<LlmProviderConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        var configuration = await _service.ActivateAsync(id, _currentUserService.UserId, cancellationToken);
        return Ok(ApiResponse<LlmProviderConfigurationDto>.SuccessResponse(configuration, "LLM provider configuration activated successfully"));
    }
}
