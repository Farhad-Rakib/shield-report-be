using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.LlmProviderCatalog;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

// AI -> Provider List: CRUD for the LLM provider lookup (name + optional model). Uses the same
// permissions as Providers & Models.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/llm-provider-catalog")]
[Authorize]
public sealed class LlmProviderCatalogController : ControllerBase
{
    private readonly ILlmProviderCatalogService _service;

    public LlmProviderCatalogController(ILlmProviderCatalogService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsRead)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<LlmProviderCatalogItemDto>>.SuccessResponse(await _service.GetAllAsync(cancellationToken), "Providers retrieved successfully"));

    [HttpPost]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsCreate)]
    public async Task<IActionResult> Create([FromBody] SaveLlmProviderCatalogItemRequestDto request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LlmProviderCatalogItemDto>.SuccessResponse(await _service.CreateAsync(request, cancellationToken), "Provider added"));

    [HttpPut("{id:long}")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsUpdate)]
    public async Task<IActionResult> Update(long id, [FromBody] SaveLlmProviderCatalogItemRequestDto request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LlmProviderCatalogItemDto>.SuccessResponse(await _service.UpdateAsync(id, request, cancellationToken), "Provider saved"));

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Permissions.LlmProviderConfigurationsDelete)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResponse(null, "Provider deleted"));
    }
}
