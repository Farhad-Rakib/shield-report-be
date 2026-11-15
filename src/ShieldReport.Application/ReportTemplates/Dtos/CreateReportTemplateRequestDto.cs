using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ReportTemplates.Dtos;

// Kind: Engagement (default) or FindingsSummary.
public sealed record CreateReportTemplateRequestDto(string Name, string CompanyDisplayName, ReportTemplateKind? Kind = null);
