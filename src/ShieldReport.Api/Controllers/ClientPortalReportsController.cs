using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Audit;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Reports.Delivery;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

// Client portal: the caller's own organization's APPROVED reports only. The organization always
// comes from the caller's token, never from the request; anything else (draft, rejected, other
// organization) is a 404.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/portal/reports")]
[Authorize(Policy = Permissions.ClientReportsRead)]
public sealed class ClientPortalReportsController : ControllerBase
{
    private readonly IReportDeliveryService _deliveryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public ClientPortalReportsController(IReportDeliveryService deliveryService, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _deliveryService = deliveryService;
        _currentUserService = currentUserService;
        _auditLogger = auditLogger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ClientReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var reports = await _deliveryService.ListForClientAsync(CallerOrganizationId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ClientReportDto>>.SuccessResponse(reports, "Reports retrieved successfully"));
    }

    [HttpGet("{id:long}/download-pdf")]
    public Task<IActionResult> DownloadPdf(long id, CancellationToken cancellationToken) => DownloadAsync(id, word: false, cancellationToken);

    [HttpGet("{id:long}/download-docx")]
    public Task<IActionResult> DownloadDocx(long id, CancellationToken cancellationToken) => DownloadAsync(id, word: true, cancellationToken);

    private async Task<IActionResult> DownloadAsync(long id, bool word, CancellationToken cancellationToken)
    {
        var (content, fileName, contentType) = await _deliveryService.GetClientFileAsync(id, CallerOrganizationId(), word, cancellationToken);
        await _auditLogger.LogAsync(AuditActions.Downloaded, "Report", id.ToString(), new { format = word ? "docx" : "pdf", portal = true }, cancellationToken: cancellationToken);
        return File(content, contentType, fileName);
    }

    private long CallerOrganizationId()
    {
        if (!_currentUserService.IsClientPortalUser || _currentUserService.ClientOrganizationId is not { } organizationId)
        {
            throw new AppException("The reports portal is only available to client users.", 403);
        }

        return organizationId;
    }
}
