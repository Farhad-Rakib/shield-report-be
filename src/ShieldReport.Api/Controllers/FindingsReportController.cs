using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.FindingsReport;
using ShieldReport.Application.FindingsReport.Dtos;
using ShieldReport.Application.Security;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Api.Controllers;

/// <summary>Staff report: tested assets and their vulnerabilities, grouped Client → Asset → Vulnerability.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/findings-report")]
[Authorize(Policy = Permissions.FindingsReportRead)]
public sealed class FindingsReportController : ControllerBase
{
    private readonly IFindingsReportService _service;

    public FindingsReportController(IFindingsReportService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<FindingsReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] long? clientOrganizationId,
        [FromQuery] long? clientAssetId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Severity? severity,
        [FromQuery] PatchStatus? patchStatus,
        CancellationToken cancellationToken)
    {
        var report = await _service.GetReportAsync(
            new FindingsReportFilter(clientOrganizationId, clientAssetId, from, to, severity, patchStatus),
            cancellationToken);
        return Ok(ApiResponse<FindingsReportDto>.SuccessResponse(report, "Findings report retrieved successfully"));
    }
}
