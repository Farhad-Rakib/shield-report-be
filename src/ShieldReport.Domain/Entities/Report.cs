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
    public ReportNarrativeStatus NarrativeStatus { get; private set; } = ReportNarrativeStatus.Manual;
    public string? NarrativeError { get; private set; }

    // Additional per-report editable sections (pre-filled from the testing type's default wording).
    public string ProjectGoals { get; private set; } = string.Empty;
    public string Limitations { get; private set; } = string.Empty;
    // {"<clientAssetId>": "Completed" | "In Progress" | ...} for the Scope of Engagement table.
    public string ScopeStatusesJson { get; private set; } = "{}";
    // Name real scan tools in the methodology / per-finding "Tools Used" (default: generic wording,
    // matching the rest of the client-facing app).
    public bool ShowToolNames { get; private set; }
    // Values of the template's custom fields for this report: {"<key>": "<value>"}.
    public string CustomFieldValuesJson { get; private set; } = "{}";

    public void SetCustomFieldValues(string json)
    {
        if (Status != ReportStatus.PendingApproval)
        {
            throw new InvalidOperationException("Report fields can only be edited while the report is pending approval.");
        }

        CustomFieldValuesJson = string.IsNullOrWhiteSpace(json) ? "{}" : json;
    }
    // Exact HTML the PDF and Word files were produced from — what the approver sees and approves.
    public string? RenderedHtml { get; private set; }

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

    // Set by the repository when the row is inserted with the engagement's next free version.
    public void AssignVersion(int version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        Version = version;
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

    public void SetSections(string projectGoals, string limitations, string scopeStatusesJson, bool showToolNames)
    {
        if (Status != ReportStatus.PendingApproval)
        {
            throw new InvalidOperationException("Report sections can only be edited while the report is pending approval.");
        }

        ProjectGoals = projectGoals;
        Limitations = limitations;
        ScopeStatusesJson = scopeStatusesJson;
        ShowToolNames = showToolNames;
    }

    // HTML engine output: the rendered HTML snapshot plus both files produced from it.
    public void MarkRendered(string renderedHtml, string docxPath, string pdfPath)
    {
        if (Status != ReportStatus.PendingApproval)
        {
            throw new InvalidOperationException("The report can only be regenerated while it is pending approval — generate a new version instead.");
        }

        RenderedHtml = renderedHtml;
        DraftDocxPath = docxPath;
        FinalPdfPath = pdfPath;
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
        // A person's edit wins over a draft still in flight.
        NarrativeStatus = ReportNarrativeStatus.Manual;
        NarrativeError = null;
    }

    public void StartNarrativeDrafting()
    {
        NarrativeStatus = ReportNarrativeStatus.Drafting;
        NarrativeError = null;
    }

    public void ApplyDraftedNarrative(string executiveSummary, string keyFindings, string keyRecommendations)
    {
        ExecutiveSummary = executiveSummary;
        KeyFindings = keyFindings;
        KeyRecommendations = keyRecommendations;
        NarrativeStatus = ReportNarrativeStatus.Drafted;
        NarrativeError = null;
    }

    public void FailNarrativeDrafting(string error)
    {
        NarrativeStatus = ReportNarrativeStatus.Failed;
        NarrativeError = error.Length > 1000 ? error[..1000] : error;
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
