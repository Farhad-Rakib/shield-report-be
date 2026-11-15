using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.ReportTemplates.Dtos;
using ShieldReport.Application.Reports.Rendering;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ReportTemplates;

public sealed class ReportTemplateService : IReportTemplateService
{
    private readonly IReportTemplateRepository _repository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    private readonly ShieldReport.Application.Reports.Rendering.IHtmlReportRenderer _htmlRenderer;

    public ReportTemplateService(IReportTemplateRepository repository, IFileStorageService fileStorageService, IUnitOfWork unitOfWork, ShieldReport.Application.Reports.Rendering.IHtmlReportRenderer htmlRenderer)
    {
        _htmlRenderer = htmlRenderer;
        _repository = repository;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ReportTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _repository.GetAllAsync(cancellationToken);
        return templates.Select(MapToDto).ToList();
    }

    public async Task<ReportTemplateDto> CreateAsync(CreateReportTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var template = new ReportTemplate(request.Name, request.CompanyDisplayName, request.Kind ?? ReportTemplateKind.Engagement);
        await _repository.AddAsync(template, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<ReportTemplateDto> UpdateAsync(long id, UpdateReportTemplateRequestDto request, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        template.UpdateDetails(request.Name, request.CompanyDisplayName);
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<ReportTemplateDto> UploadDocxTemplateAsync(long id, Stream fileContent, string fileName, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        if (template.Kind == ReportTemplateKind.FindingsSummary)
        {
            throw new AppException("Findings summary templates are HTML only; Word template uploads are not supported for them.", 400);
        }

        if (!fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("Only .docx files are supported.", 400);
        }

        // Cheap structural sanity check — every .docx is a ZIP archive ("PK" magic bytes) — rather
        // than trusting the client-supplied content-type/extension alone.
        var header = new byte[2];
        var read = await fileContent.ReadAsync(header, 0, 2, cancellationToken);
        if (read < 2 || header[0] != 0x50 || header[1] != 0x4B)
        {
            throw new AppException("The uploaded file is not a valid .docx document.", 400);
        }
        fileContent.Position = 0;

        var relativePath = await _fileStorageService.SaveAsync($"report-templates/{template.Id}", "template.docx", fileContent, cancellationToken);
        template.SetDocxTemplate(relativePath, fileName);
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task<ReportTemplateDto> ClearDocxTemplateAsync(long id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        if (template.DocxTemplatePath is not null)
        {
            _fileStorageService.Delete(template.DocxTemplatePath);
        }

        template.ClearDocxTemplate();
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken);
        if (template is null)
        {
            return;
        }

        _repository.Delete(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static ReportTemplateDto MapToDto(ReportTemplate template)
        => new(template.Id, template.Name, template.CompanyDisplayName, template.Kind, template.DocxTemplatePath is not null, template.DocxTemplateFileName, template.HtmlContent is not null, ReportTemplateFields.Parse(template.CustomFieldsJson), template.BuiltInTestingType);

    public async Task<ReportTemplateHtmlDto> GetHtmlAsync(long id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);
        return new ReportTemplateHtmlDto(template.Id, template.HtmlContent is not null, _htmlRenderer.ResolveTemplateHtml(template, SampleTestingType(template)));
    }

    // Saves a custom Handlebars template after checking it compiles and renders against sample data.
    public async Task<ReportTemplateHtmlDto> UpdateHtmlAsync(long id, string htmlContent, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        if (string.IsNullOrWhiteSpace(htmlContent) || htmlContent.Length > 2_000_000)
        {
            throw new AppException("The template must be between 1 character and 2 MB.", 400);
        }

        var error = template.Kind == ReportTemplateKind.FindingsSummary
            ? await _htmlRenderer.ValidateFindingsSummaryTemplateAsync(htmlContent, cancellationToken)
            : await _htmlRenderer.ValidateTemplateAsync(htmlContent, cancellationToken);
        if (error is not null)
        {
            throw new AppException($"Template error: {error}", 400);
        }

        template.SetHtmlContent(htmlContent);
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ReportTemplateHtmlDto(template.Id, true, htmlContent);
    }

    public async Task<ReportTemplateDto> UpdateFieldsAsync(long id, IReadOnlyList<ReportTemplateField> fields, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);
        var validated = ReportTemplateFields.Validate(fields);
        template.SetCustomFields(ReportTemplateFields.Serialize(validated));
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(template);
    }

    // Uploaded .html/.hbs file → the template's HTML (same validation as the editor).
    public async Task<ReportTemplateHtmlDto> UploadHtmlAsync(long id, Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".html" or ".htm" or ".hbs" or ".handlebars"))
        {
            throw new AppException("Upload an .html, .htm or .hbs file.", 400);
        }

        using var reader = new StreamReader(content, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var html = await reader.ReadToEndAsync(cancellationToken);
        return await UpdateHtmlAsync(id, html, cancellationToken);
    }

    public async Task<string> PreviewAsync(long id, string? htmlContent, IReadOnlyList<ReportTemplateField>? fields, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);
        var testingType = SampleTestingType(template);
        var html = string.IsNullOrWhiteSpace(htmlContent) ? _htmlRenderer.ResolveTemplateHtml(template, testingType) : htmlContent;
        var effectiveFields = fields is null ? ReportTemplateFields.Parse(template.CustomFieldsJson) : ReportTemplateFields.Validate(fields);
        var sample = effectiveFields.ToDictionary(f => f.Key, f => string.IsNullOrEmpty(f.DefaultValue) ? $"[{f.Label}]" : f.DefaultValue);
        try
        {
            return template.Kind == ReportTemplateKind.FindingsSummary
                ? await _htmlRenderer.RenderFindingsSummarySampleAsync(html, sample, cancellationToken)
                : await _htmlRenderer.RenderSampleAsync(html, sample, testingType, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new AppException($"Template error: {ex.Message}", 400);
        }
    }

    public async Task<ReportTemplateHtmlDto> ResetHtmlAsync(long id, CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppException("Report template not found.", 404);

        template.SetHtmlContent(null);
        _repository.Update(template);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ReportTemplateHtmlDto(template.Id, false, _htmlRenderer.ResolveTemplateHtml(template, SampleTestingType(template)));
    }

    // Testing type used for the editor's built-in fallback and sample data: the row's built-in type,
    // or White for a user-created template (no engagement is known in the template studio).
    private static TestingType SampleTestingType(ReportTemplate template) => template.BuiltInTestingType ?? TestingType.White;
}
