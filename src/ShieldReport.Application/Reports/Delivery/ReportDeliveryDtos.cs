namespace ShieldReport.Application.Reports.Delivery;

public sealed record ReportRecipientOptionDto(string Email, string Name, string Source);

public sealed record SendReportRequestDto(IReadOnlyList<string> Recipients, string? Message, bool AttachWord);

public sealed record ReportDeliveryDto(
    long Id,
    long ReportId,
    string Status,                       // Pending | Sent | PartiallySent | Failed
    IReadOnlyList<string> Recipients,
    IReadOnlyList<string> DeliveredTo,
    string? Message,
    bool AttachWord,
    long SentByUserId,
    string? SentByName,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? LastError);

// Client portal view of an approved report.
public sealed record ClientReportDto(long Id, string EngagementTitle, int Version, DateTime? ApprovedAt, bool PdfAvailable, bool WordAvailable);

public sealed record ReportEmailJobPayload(long DeliveryId);
