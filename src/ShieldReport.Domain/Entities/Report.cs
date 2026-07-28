using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class Report : BaseEntity
{
    public Guid PublicId { get; private set; } = Guid.NewGuid();
    public long EngagementId { get; private set; }
    public long ReportTemplateId { get; private set; }
    public int Version { get; private set; }
    public ReportStatus Status { get; private set; } = ReportStatus.PendingApproval;

    // LLM-drafted from the included findings' AiExplanation/Description/Impact, then
    // pentester-editable via SetNarrative before approval — see IReportNarrativeDraftingService.
    public string ExecutiveSummary { get; private set; } = string.Empty;
    public string KeyFindings { get; private set; } = string.Empty;
    public string KeyRecommendations { get; private set; } = string.Empty;

    public string? DraftDocxPath { get; private set; }
    public string? FinalPdfPath { get; private set; }
    public long GeneratedByUserId { get; private set; }
    public DateTime GeneratedAt { get; private set; } = DateTime.UtcNow;
    public long? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public long? RejectedByUserId { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public Engagement Engagement { get; private set; } = null!;
    public ReportTemplate ReportTemplate { get; private set; } = null!;

    private Report()
    {
    }

    public Report(long engagementId, long reportTemplateId, int version, long generatedByUserId, string executiveSummary, string keyFindings, string keyRecommendations)
    {
        EngagementId = engagementId;
        ReportTemplateId = reportTemplateId;
        Version = version;
        GeneratedByUserId = generatedByUserId;
        ExecutiveSummary = executiveSummary;
        KeyFindings = keyFindings;
        KeyRecommendations = keyRecommendations;
    }

    public void MarkDraftGenerated(string draftDocxPath)
    {
        if (Status != ReportStatus.PendingApproval)
        {
            throw new InvalidOperationException("The draft can only be regenerated while the report is pending approval — generate a new version instead of editing an approved or rejected report.");
        }

        DraftDocxPath = draftDocxPath;
        // A new draft invalidates any previously converted PDF — it no longer reflects the
        // current narrative/findings until Convert to PDF is run again.
        FinalPdfPath = null;
    }

    public void MarkPdfGenerated(string finalPdfPath)
    {
        FinalPdfPath = finalPdfPath;
    }

    public void SetNarrative(string executiveSummary, string keyFindings, string keyRecommendations)
    {
        if (Status != ReportStatus.PendingApproval)
        {
            throw new InvalidOperationException("Narrative can only be edited while the report is pending approval.");
        }

        ExecutiveSummary = executiveSummary;
        KeyFindings = keyFindings;
        KeyRecommendations = keyRecommendations;
    }

    public void Approve(long approvedByUserId)
    {
        if (Status != ReportStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only a pending-approval report can be approved.");
        }

        Status = ReportStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = DateTime.UtcNow;
    }

    public void Reject(long rejectedByUserId, string reason)
    {
        if (Status != ReportStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only a pending-approval report can be rejected.");
        }

        Status = ReportStatus.Rejected;
        RejectedByUserId = rejectedByUserId;
        RejectedAt = DateTime.UtcNow;
        RejectionReason = reason;
    }
}
