using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.SiteSettings;
using ShieldReport.Application.SiteSettings.Dtos;

namespace ShieldReport.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class SiteSettingsController : ControllerBase
{
    private readonly ISiteSettingService _siteSettingService;

    public SiteSettingsController(ISiteSettingService siteSettingService)
    {
        _siteSettingService = siteSettingService;
    }

    // Site settings (title/branding) are read here by AuthLayout to render the login/register
    // screens *before* a user has a session — that only works if these two GETs are actually
    // reachable pre-auth. Left [Authorize] before, every pre-login page's settings fetch 401'd,
    // which (via BaseRepository's refresh-then-hard-redirect interceptor) silently bounced
    // visitors on public pages like /register/:token straight back to /login before they could
    // see anything. Mutations below stay behind [Authorize].
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SiteSettingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var settings = await _siteSettingService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SiteSettingDto>>.SuccessResponse(settings, "Site settings retrieved successfully"));
    }

    [HttpGet("{key}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SiteSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(string key, CancellationToken cancellationToken)
    {
        var setting = await _siteSettingService.GetByKeyAsync(key, cancellationToken);
        if (setting is null)
            return NotFound(ApiResponse.FailureResponse("Setting not found", StatusCodes.Status404NotFound));

        return Ok(ApiResponse<SiteSettingDto>.SuccessResponse(setting, "Site setting retrieved successfully"));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<SiteSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrUpdate([FromBody] SiteSettingDto dto, CancellationToken cancellationToken)
    {
        var result = await _siteSettingService.CreateOrUpdateAsync(dto, cancellationToken);
        return Ok(ApiResponse<SiteSettingDto>.SuccessResponse(result, "Site setting saved successfully", StatusCodes.Status200OK));
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _siteSettingService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Site setting deleted successfully"));
    }
}

