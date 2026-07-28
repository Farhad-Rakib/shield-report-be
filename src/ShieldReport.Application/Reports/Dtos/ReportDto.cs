namespace ShieldReport.Application.Reports.Dtos;

public sealed record ReportDto(
    long Id,
    Guid PublicId,
    long EngagementId,
    long ReportTemplateId,
    int Version,
    string Status,
    string ExecutiveSummary,
    string KeyFindings,
    string KeyRecommendations,
    bool DraftAvailable,
    bool PdfAvailable,
    long GeneratedByUserId,
    DateTime GeneratedAt,
    long? ApprovedByUserId,
    DateTime? ApprovedAt,
    long? RejectedByUserId,
    DateTime? RejectedAt,
    string? RejectionReason
);
