using ShieldReport.Application.ReportTemplates;

namespace ShieldReport.Application.FindingsReport.Dtos;

// POST /findings-report/export-pdf. Filters are the same as GET /findings-report.
public sealed record ExportFindingsReportPdfRequestDto(
    long TemplateId,
    FindingsReportFilter? Filters,
    Dictionary<string, string>? CustomFieldValues);

// GET /findings-report/templates: Findings summary templates a findings-report user can export with.
public sealed record FindingsReportTemplateOptionDto(
    long Id,
    string Name,
    string CompanyDisplayName,
    IReadOnlyList<ReportTemplateField> CustomFields);

public sealed record FindingsReportPdfFile(byte[] Content, string FileName, long TemplateId, string TemplateName);
