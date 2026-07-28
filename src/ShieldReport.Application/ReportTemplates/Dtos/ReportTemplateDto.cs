namespace ShieldReport.Application.ReportTemplates.Dtos;

public sealed record ReportTemplateDto(
    long Id,
    string Name,
    string CompanyDisplayName,
    bool IsDefault,
    bool HasCustomDocxTemplate,
    string? DocxTemplateFileName);
