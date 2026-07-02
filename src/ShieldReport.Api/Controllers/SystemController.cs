using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Security;
using ShieldReport.Api.Startup;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class SystemController : ControllerBase
{
    private readonly EndpointDataSource _endpointDataSource;
    private readonly DistributedCacheAdminService _distributedCacheAdminService;

    public SystemController(
        EndpointDataSource endpointDataSource,
        DistributedCacheAdminService distributedCacheAdminService)
    {
        _endpointDataSource = endpointDataSource;
        _distributedCacheAdminService = distributedCacheAdminService;
    }

    [HttpGet("endpoints")]
    [Authorize(Policy = Permissions.SystemEndpointsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EndpointInfoResponse>>), StatusCodes.Status200OK)]
    public IActionResult GetAllEndpoints()
    {
        var endpoints = _endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => new EndpointInfoResponse(
                endpoint.DisplayName ?? string.Empty,
                endpoint.RoutePattern.RawText ?? string.Empty,
                endpoint.Metadata
                    .OfType<HttpMethodMetadata>()
                    .SelectMany(metadata => metadata.HttpMethods)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(method => method)
                    .ToList()))
            .OrderBy(x => x.Route)
            .ThenBy(x => x.DisplayName)
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<EndpointInfoResponse>>.SuccessResponse(endpoints, "Endpoints retrieved successfully"));
    }

    [HttpGet("cache/distributed/keys")]
    [Authorize(Policy = Permissions.SystemCacheRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DistributedCacheEntryResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDistributedCacheEntries([FromQuery] string? pattern, CancellationToken cancellationToken)
    {
        var entries = await _distributedCacheAdminService.GetEntriesAsync(pattern, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DistributedCacheEntryResponse>>.SuccessResponse(
            entries.Select(entry => new DistributedCacheEntryResponse(entry.Key, entry.Value)).ToList(),
            "Distributed cache entries retrieved successfully"));
    }

    [HttpDelete("cache/distributed/flush")]
    [Authorize(Policy = Permissions.SystemCacheFlush)]
    [ProducesResponseType(typeof(ApiResponse<DistributedCacheFlushResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FlushDistributedCache(CancellationToken cancellationToken)
    {
        var deleted = await _distributedCacheAdminService.FlushAsync(cancellationToken);
        return Ok(ApiResponse<DistributedCacheFlushResponse>.SuccessResponse(
            new DistributedCacheFlushResponse(deleted),
            "Distributed cache flushed successfully"));
    }

    // Palette endpoints moved to SiteSettingsController

    public sealed record EndpointInfoResponse(string DisplayName, string Route, IReadOnlyList<string> Methods);
    public sealed record DistributedCacheEntryResponse(string Key, string? Value);
    public sealed record DistributedCacheFlushResponse(long DeletedCount);
}

