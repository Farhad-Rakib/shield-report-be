using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.CustomScanTools;
using ShieldReport.Application.ScanTools;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

// Registry of admin-defined scan tools (Docker image + argument template). SuperAdmin only:
// running an arbitrary image on the host Docker daemon is root-equivalent.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/custom-scan-tools")]
[Authorize(Policy = Permissions.CustomScanToolsManage)]
public sealed class CustomScanToolsController : ControllerBase
{
    private readonly ICustomScanToolService _service;
    private readonly ICurrentUserService _currentUserService;

    public CustomScanToolsController(ICustomScanToolService service, ICurrentUserService currentUserService)
    {
        _service = service;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<CustomScanToolDto>>.SuccessResponse(await _service.GetAllAsync(cancellationToken), "Custom tools retrieved successfully"));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveCustomScanToolRequestDto request, CancellationToken cancellationToken)
    {
        var tool = await _service.CreateAsync(request, _currentUserService.UserId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CustomScanToolDto>.SuccessResponse(tool, "Custom tool created", StatusCodes.Status201Created));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveCustomScanToolRequestDto request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CustomScanToolDto>.SuccessResponse(await _service.UpdateAsync(id, request, _currentUserService.UserId, cancellationToken), "Custom tool updated"));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Custom tool deleted"));
    }

    // Pulls the image if missing and reports its id/platform/size. Never runs it against a target.
    [HttpPost("{id:long}/test")]
    public async Task<IActionResult> Test(long id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ScanToolTestResultDto>.SuccessResponse(await _service.TestAsync(id, cancellationToken), "Test finished"));
}
