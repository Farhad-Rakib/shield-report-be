using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Dashboard;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = Permissions.DashboardRead)]
public sealed class DashboardLayoutController : ControllerBase
{
    private readonly IDashboardLayoutService _service;

    public DashboardLayoutController(IDashboardLayoutService service) => _service = service;

    /// <summary>Returns the calling user's saved dashboard layout. Returns null when not yet saved.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<DashboardLayoutDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLayout(CancellationToken ct)
    {
        var layout = await _service.GetLayoutAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse<DashboardLayoutDto?>.SuccessResponse(layout));
    }

    /// <summary>Upserts the calling user's dashboard layout (widget order + hidden widgets).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveLayout([FromBody] SaveDashboardLayoutRequest request, CancellationToken ct)
    {
        await _service.SaveLayoutAsync(GetCurrentUserId(), request.WidgetOrder, request.HiddenWidgets, ct);
        return Ok(ApiResponse<object?>.SuccessResponse(null, "Layout saved."));
    }

    private long GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst("id") ?? User.FindFirst("userId");
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            throw new UnauthorizedAccessException("User ID claim not found.");
        return userId;
    }
}

public sealed record SaveDashboardLayoutRequest(string[] WidgetOrder, string[] HiddenWidgets);
