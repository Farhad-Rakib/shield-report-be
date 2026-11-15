using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.Audit;
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
    private readonly IFindingsReportExportService _exportService;
    private readonly IAuditLogger _auditLogger;

    public FindingsReportController(IFindingsReportService service, IFindingsReportExportService exportService, IAuditLogger auditLogger)
    {
        _service = service;
        _exportService = exportService;
        _auditLogger = auditLogger;
    }

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

    /// <summary>Findings summary templates (with their dynamic fields) available for the PDF export.</summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FindingsReportTemplateOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<FindingsReportTemplateOptionDto>>.SuccessResponse(
            await _exportService.GetTemplateOptionsAsync(cancellationToken), "Findings summary templates retrieved successfully"));

    /// <summary>Renders the report for the given filters with a Findings summary template to PDF.</summary>
    [HttpPost("export-pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPdf([FromBody] ExportFindingsReportPdfRequestDto request, CancellationToken cancellationToken)
    {
        var file = await _exportService.ExportPdfAsync(request, cancellationToken);
        await _auditLogger.LogAsync(AuditActions.Downloaded, "FindingsReport", null,
            new { format = "pdf", file.FileName, file.TemplateId, file.TemplateName, request.Filters }, cancellationToken: cancellationToken);
        return File(file.Content, "application/pdf", file.FileName);
    }
}
