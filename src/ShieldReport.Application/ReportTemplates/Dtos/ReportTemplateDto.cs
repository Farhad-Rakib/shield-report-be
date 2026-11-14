using ShieldReport.Application.ReportTemplates;
namespace ShieldReport.Application.ReportTemplates.Dtos;

public sealed record ReportTemplateDto(
    long Id,
    string Name,
    string CompanyDisplayName,
    bool IsDefault,
    bool HasCustomDocxTemplate,
    string? DocxTemplateFileName,
    bool HasCustomHtmlTemplate,
    IReadOnlyList<ReportTemplateField>? CustomFields = null);

public sealed record UpdateReportTemplateFieldsRequestDto(IReadOnlyList<ReportTemplateField> Fields);

// Renders an (unsaved) template against sample data for the editor's preview.
public sealed record PreviewReportTemplateRequestDto(string? HtmlContent, IReadOnlyList<ReportTemplateField>? Fields);

// Effective HTML template for a ReportTemplate: its custom one, or the built-in default.
public sealed record ReportTemplateHtmlDto(long Id, bool IsCustom, string HtmlContent);

public sealed record UpdateReportTemplateHtmlRequestDto(string HtmlContent);
