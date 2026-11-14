using ShieldReport.Application.Reports.Delivery;
using ShieldReport.Application.Audit;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Reports;
using ShieldReport.Application.Reports.Dtos;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public ReportsController(IReportService reportService, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _reportService = reportService;
        _currentUserService = currentUserService;
        _auditLogger = auditLogger;
    }

    [HttpPost("engagements/{engagementId:long}/reports")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Generate(long engagementId, [FromBody] GenerateReportRequestDto? request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var report = await _reportService.GenerateAsync(engagementId, currentUserId, request ?? new GenerateReportRequestDto(), cancellationToken);
        var message = report.NarrativeStatus == "Drafting"
            ? "Report generated. The AI is drafting the summary sections in the background."
            : "Report generated successfully";
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ReportDto>.SuccessResponse(report, message, StatusCodes.Status201Created));
    }

    // What Generate would produce with these options (placeholder narrative). Saves nothing.
    // The HTML is only shown in the frontend's sandboxed preview iframe.
    [HttpPost("engagements/{engagementId:long}/reports/preview")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewNew(long engagementId, [FromBody] ReportPreviewRequestDto? request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var html = await _reportService.PreviewNewAsync(engagementId, currentUserId, request ?? new ReportPreviewRequestDto(), cancellationToken);
        return Ok(ApiResponse<string>.SuccessResponse(html, "Report preview rendered"));
    }

    // Templates offered in the Generate Report dialog. Under the Generate permission (not
    // report-templates.read) so anyone who can generate can choose a template.
    [HttpGet("reports/template-options")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReportTemplateOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TemplateOptions(CancellationToken cancellationToken)
    {
        var options = await _reportService.GetTemplateOptionsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReportTemplateOptionDto>>.SuccessResponse(options, "Report templates retrieved successfully"));
    }

    [HttpPost("reports/{id:long}/draft-narrative")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DraftNarrative(long id, CancellationToken cancellationToken)
    {
        var report = await _reportService.RequestNarrativeDraftAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "AI drafting queued"));
    }

    [HttpGet("engagements/{engagementId:long}/reports")]
    [Authorize(Policy = Permissions.VulnerabilityReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListForEngagement(long engagementId, CancellationToken cancellationToken)
    {
        var reports = await _reportService.ListAsync(engagementId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReportDto>>.SuccessResponse(reports, "Reports retrieved successfully"));
    }

    [HttpGet("reports/{id:long}")]
    [Authorize(Policy = Permissions.VulnerabilityReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var report = await _reportService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "Report retrieved successfully"));
    }

    [HttpPatch("reports/{id:long}/narrative")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNarrative(long id, [FromBody] UpdateReportNarrativeRequestDto request, CancellationToken cancellationToken)
    {
        var report = await _reportService.UpdateNarrativeAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "Report narrative updated successfully"));
    }

    [HttpPatch("reports/{id:long}/sections")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSections(long id, [FromBody] UpdateReportSectionsRequestDto request, CancellationToken cancellationToken)
    {
        var report = await _reportService.UpdateSectionsAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "Report sections updated successfully"));
    }

    // The exact HTML the PDF/Word were produced from. Returned as JSON (not text/html) so it is only
    // ever displayed inside the frontend's sandboxed preview iframe, never as a page on the API origin.
    [HttpGet("reports/{id:long}/preview")]
    [Authorize(Policy = Permissions.VulnerabilityReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(long id, CancellationToken cancellationToken)
    {
        var html = await _reportService.GetPreviewHtmlAsync(id, cancellationToken)
            ?? throw new AppException("This report was generated with the legacy Word engine and has no HTML preview. Regenerate it to get one.", 404);
        return Ok(ApiResponse<string>.SuccessResponse(html, "Report preview retrieved successfully"));
    }

    // ---- Send to client ----

    [HttpGet("reports/{id:long}/recipient-options")]
    [Authorize(Policy = Permissions.VulnerabilityReportsSend)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReportRecipientOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecipientOptions(long id, [FromServices] IReportDeliveryService deliveryService, CancellationToken cancellationToken)
    {
        var options = await deliveryService.GetRecipientOptionsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReportRecipientOptionDto>>.SuccessResponse(options, "Recipient options retrieved successfully"));
    }

    // Approved reports only (409 otherwise). Emails go out from a background job; poll deliveries
    // for the outcome.
    [HttpPost("reports/{id:long}/send")]
    [Authorize(Policy = Permissions.VulnerabilityReportsSend)]
    [ProducesResponseType(typeof(ApiResponse<ReportDeliveryDto>), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Send(long id, [FromBody] SendReportRequestDto request, [FromServices] IReportDeliveryService deliveryService, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var delivery = await deliveryService.SendAsync(id, request, userId, cancellationToken);
        return StatusCode(StatusCodes.Status202Accepted, ApiResponse<ReportDeliveryDto>.SuccessResponse(delivery, "Report queued for sending", StatusCodes.Status202Accepted));
    }

    [HttpGet("reports/{id:long}/deliveries")]
    [Authorize(Policy = Permissions.VulnerabilityReportsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReportDeliveryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deliveries(long id, [FromServices] IReportDeliveryService deliveryService, CancellationToken cancellationToken)
    {
        var deliveries = await deliveryService.ListAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReportDeliveryDto>>.SuccessResponse(deliveries, "Deliveries retrieved successfully"));
    }

    [HttpPost("reports/{id:long}/regenerate")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Regenerate(long id, CancellationToken cancellationToken)
    {
        var report = await _reportService.RegenerateAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "Draft regenerated successfully"));
    }

    [HttpPost("reports/{id:long}/convert-to-pdf")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConvertToPdf(long id, CancellationToken cancellationToken)
    {
        var report = await _reportService.ConvertToPdfAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "Report converted to PDF successfully"));
    }

    [HttpPost("reports/{id:long}/approve")]
    [Authorize(Policy = Permissions.VulnerabilityReportsApprove)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var report = await _reportService.ApproveAsync(id, currentUserId, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "Report approved successfully"));
    }

    [HttpPost("reports/{id:long}/reject")]
    [Authorize(Policy = Permissions.VulnerabilityReportsApprove)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(long id, [FromBody] RejectReportRequestDto request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var report = await _reportService.RejectAsync(id, currentUserId, request.Reason, cancellationToken);
        return Ok(ApiResponse<ReportDto>.SuccessResponse(report, "Report rejected successfully"));
    }

    [HttpDelete("reports/{id:long}")]
    [Authorize(Policy = Permissions.VulnerabilityReportsDelete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _reportService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Report deleted successfully"));
    }

    [HttpGet("reports/{id:long}/download-docx")]
    [Authorize(Policy = Permissions.VulnerabilityReportsDownload)]
    public async Task<IActionResult> DownloadDraft(long id, CancellationToken cancellationToken)
    {
        var (content, fileName) = await _reportService.GetDraftFileAsync(id, cancellationToken);
        await _auditLogger.LogAsync(AuditActions.Downloaded, "Report", id.ToString(), new { format = "docx", fileName }, cancellationToken: cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }

    [HttpGet("reports/{id:long}/download-pdf")]
    [Authorize(Policy = Permissions.VulnerabilityReportsDownload)]
    public async Task<IActionResult> DownloadPdf(long id, CancellationToken cancellationToken)
    {
        var (content, fileName) = await _reportService.GetPdfFileAsync(id, cancellationToken);
        await _auditLogger.LogAsync(AuditActions.Downloaded, "Report", id.ToString(), new { format = "pdf", fileName }, cancellationToken: cancellationToken);
        return File(content, "application/pdf", fileName);
    }
}
