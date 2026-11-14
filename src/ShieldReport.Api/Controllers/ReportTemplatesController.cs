using ShieldReport.Application.Common.Exceptions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShieldReport.Api.Common;
using ShieldReport.Application.ReportTemplates;
using ShieldReport.Application.ReportTemplates.Dtos;
using ShieldReport.Application.Security;

namespace ShieldReport.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class ReportTemplatesController : ControllerBase
{
    private readonly IReportTemplateService _reportTemplateService;

    public ReportTemplatesController(IReportTemplateService reportTemplateService)
    {
        _reportTemplateService = reportTemplateService;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.ReportTemplatesRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ReportTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var templates = await _reportTemplateService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReportTemplateDto>>.SuccessResponse(templates, "Report templates retrieved successfully"));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ReportTemplatesCreate)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateReportTemplateRequestDto request, CancellationToken cancellationToken)
    {
        var template = await _reportTemplateService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ReportTemplateDto>.SuccessResponse(template, "Report template created successfully", StatusCodes.Status201Created));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateReportTemplateRequestDto request, CancellationToken cancellationToken)
    {
        var template = await _reportTemplateService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ReportTemplateDto>.SuccessResponse(template, "Report template updated successfully"));
    }

    [HttpPost("{id:long}/docx-template")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadDocxTemplate(long id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse.FailureResponse("No file was uploaded.", StatusCodes.Status400BadRequest));
        }

        await using var stream = file.OpenReadStream();
        var template = await _reportTemplateService.UploadDocxTemplateAsync(id, stream, file.FileName, cancellationToken);
        return Ok(ApiResponse<ReportTemplateDto>.SuccessResponse(template, "Template document uploaded successfully"));
    }

    [HttpPost("{id:long}/set-default")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetDefault(long id, CancellationToken cancellationToken)
    {
        var template = await _reportTemplateService.SetDefaultAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportTemplateDto>.SuccessResponse(template, "Default report template updated"));
    }

    [HttpDelete("{id:long}/docx-template")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearDocxTemplate(long id, CancellationToken cancellationToken)
    {
        var template = await _reportTemplateService.ClearDocxTemplateAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportTemplateDto>.SuccessResponse(template, "Template document removed — this template now falls back to the default document"));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = Permissions.ReportTemplatesDelete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _reportTemplateService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Report template deleted successfully"));
    }

    // ---- HTML template (Handlebars) — the engine used for PDF + Word output ----

    [HttpGet("{id:long}/html")]
    [Authorize(Policy = Permissions.ReportTemplatesRead)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateHtmlDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHtml(long id, CancellationToken cancellationToken)
    {
        var html = await _reportTemplateService.GetHtmlAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportTemplateHtmlDto>.SuccessResponse(html, "Template retrieved successfully"));
    }

    [HttpPut("{id:long}/html")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateHtmlDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateHtml(long id, [FromBody] UpdateReportTemplateHtmlRequestDto request, CancellationToken cancellationToken)
    {
        var html = await _reportTemplateService.UpdateHtmlAsync(id, request.HtmlContent, cancellationToken);
        return Ok(ApiResponse<ReportTemplateHtmlDto>.SuccessResponse(html, "Template saved"));
    }

    [HttpPost("{id:long}/html-file")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    [RequestSizeLimit(2_500_000)]
    public async Task<IActionResult> UploadHtml(long id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppException("Choose a file to upload.", 400);
        }

        await using var stream = file.OpenReadStream();
        var html = await _reportTemplateService.UploadHtmlAsync(id, stream, file.FileName, cancellationToken);
        return Ok(ApiResponse<ReportTemplateHtmlDto>.SuccessResponse(html, "Template uploaded"));
    }

    [HttpPut("{id:long}/fields")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    public async Task<IActionResult> UpdateFields(long id, [FromBody] UpdateReportTemplateFieldsRequestDto request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ReportTemplateDto>.SuccessResponse(await _reportTemplateService.UpdateFieldsAsync(id, request.Fields, cancellationToken), "Dynamic fields saved"));

    // Sample-data render of the saved or the posted (unsaved) template, for the editor preview.
    [HttpPost("{id:long}/preview")]
    [Authorize(Policy = Permissions.ReportTemplatesRead)]
    public async Task<IActionResult> Preview(long id, [FromBody] PreviewReportTemplateRequestDto? request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<string>.SuccessResponse(await _reportTemplateService.PreviewAsync(id, request?.HtmlContent, request?.Fields, cancellationToken), "Preview rendered"));

    [HttpGet("variables")]
    [Authorize(Policy = Permissions.ReportTemplatesRead)]
    public IActionResult Variables() =>
        Ok(ApiResponse<IReadOnlyList<ReportTemplateVariable>>.SuccessResponse(ReportTemplateFields.Variables, "Template variables"));

    [HttpDelete("{id:long}/html")]
    [Authorize(Policy = Permissions.ReportTemplatesUpdate)]
    [ProducesResponseType(typeof(ApiResponse<ReportTemplateHtmlDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetHtml(long id, CancellationToken cancellationToken)
    {
        var html = await _reportTemplateService.ResetHtmlAsync(id, cancellationToken);
        return Ok(ApiResponse<ReportTemplateHtmlDto>.SuccessResponse(html, "Template reset to the built-in default"));
    }
}
