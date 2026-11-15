using ShieldReport.Application.ReportTemplates;
using ShieldReport.Domain.Enums;
namespace ShieldReport.Application.ReportTemplates.Dtos;

public sealed record ReportTemplateDto(
    long Id,
    string Name,
    string CompanyDisplayName,
    ReportTemplateKind Kind,
    bool HasCustomDocxTemplate,
    string? DocxTemplateFileName,
    bool HasCustomHtmlTemplate,
    IReadOnlyList<ReportTemplateField>? CustomFields = null,
    // White / Gray / Black: a built-in SELISE template row; null = created by an admin.
    TestingType? BuiltInTestingType = null);

public sealed record UpdateReportTemplateFieldsRequestDto(IReadOnlyList<ReportTemplateField> Fields);

// Renders an (unsaved) template against sample data for the editor's preview.
public sealed record PreviewReportTemplateRequestDto(string? HtmlContent, IReadOnlyList<ReportTemplateField>? Fields);

// Effective HTML template for a ReportTemplate: its custom one, or its built-in (per testing type).
public sealed record ReportTemplateHtmlDto(long Id, bool IsCustom, string HtmlContent);

public sealed record UpdateReportTemplateHtmlRequestDto(string HtmlContent);
