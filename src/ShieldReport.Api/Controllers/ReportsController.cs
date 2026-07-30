using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ShieldReport.Api.Common;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
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
    private readonly IReportEditorAccessTokenService _editorAccessTokenService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public ReportsController(
        IReportService reportService,
        ICurrentUserService currentUserService,
        IReportEditorAccessTokenService editorAccessTokenService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _reportService = reportService;
        _currentUserService = currentUserService;
        _editorAccessTokenService = editorAccessTokenService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("engagements/{engagementId:long}/reports")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Generate(long engagementId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new AppException("User context is missing.", 401);
        var report = await _reportService.GenerateAsync(engagementId, currentUserId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ReportDto>.SuccessResponse(report, "Report generated successfully", StatusCodes.Status201Created));
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
        return File(content, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }

    [HttpGet("reports/{id:long}/download-pdf")]
    [Authorize(Policy = Permissions.VulnerabilityReportsDownload)]
    public async Task<IActionResult> DownloadPdf(long id, CancellationToken cancellationToken)
    {
        var (content, fileName) = await _reportService.GetPdfFileAsync(id, cancellationToken);
        return File(content, "application/pdf", fileName);
    }

    // ---- In-browser editing (OnlyOffice Document Server) — see the plan's networking note for
    // why the source/callback endpoints below use a short-lived token instead of the normal
    // bearer auth: the Document Server fetches/saves from inside its own container, with no
    // user session to send. ----

    [HttpPost("reports/{id:long}/editor-session")]
    [Authorize(Policy = Permissions.VulnerabilityReportsGenerate)]
    [ProducesResponseType(typeof(ApiResponse<ReportEditorSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartEditorSession(long id, CancellationToken cancellationToken)
    {
        var report = await _reportService.GetByIdAsync(id, cancellationToken);
        if (!report.DraftAvailable)
        {
            throw new AppException("This report has no draft document to edit yet.", 400);
        }

        var accessToken = _editorAccessTokenService.Issue(id);
        var session = new ReportEditorSessionDto(
            _configuration["OnlyOffice:DocumentServerUrl"] ?? throw new InvalidOperationException("OnlyOffice:DocumentServerUrl is not configured."),
            _configuration["OnlyOffice:ApiHostForContainer"] ?? throw new InvalidOperationException("OnlyOffice:ApiHostForContainer is not configured."),
            accessToken,
            Guid.NewGuid().ToString("N"), // fresh every session so the Document Server never serves a stale cached copy
            $"report-v{report.Version}.docx");

        return Ok(ApiResponse<ReportEditorSessionDto>.SuccessResponse(session, "Editor session started successfully"));
    }

    [HttpGet("reports/{id:long}/editor-source")]
    [AllowAnonymous]
    public async Task<IActionResult> EditorSource(long id, [FromQuery] string accessToken, CancellationToken cancellationToken)
    {
        if (!_editorAccessTokenService.TryValidate(accessToken, out var tokenReportId) || tokenReportId != id)
        {
            return Unauthorized();
        }

        var (content, fileName) = await _reportService.GetDraftFileAsync(id, cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
    }

    [HttpPost("reports/{id:long}/editor-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> EditorCallback(long id, [FromQuery] string accessToken, [FromBody] OnlyOfficeCallbackPayload payload, CancellationToken cancellationToken)
    {
        if (!_editorAccessTokenService.TryValidate(accessToken, out var tokenReportId) || tokenReportId != id)
        {
            return Unauthorized();
        }

        // Status 2 = "ready for saving" (editor closed), 6 = force-save while still open —
        // anything else (1 = still editing, 4 = closed with no changes, ...) needs no action.
        // Always ack with {"error":0} regardless, per OnlyOffice's callback contract, so a
        // no-op status doesn't get retried forever.
        if ((payload.Status == 2 || payload.Status == 6) && !string.IsNullOrWhiteSpace(payload.Url))
        {
            var httpClient = _httpClientFactory.CreateClient();
            var docxBytes = await httpClient.GetByteArrayAsync(payload.Url, cancellationToken);
            await _reportService.OverwriteDraftAsync(id, docxBytes, cancellationToken);
        }

        return Ok(new { error = 0 });
    }

    // Mirrors OnlyOffice's callback JSON shape (see their documentation for the full set of
    // fields) — only the two fields this integration acts on are declared; System.Text.Json
    // ignores the rest by default.
    public sealed class OnlyOfficeCallbackPayload
    {
        public int Status { get; set; }
        public string? Url { get; set; }
    }
}
