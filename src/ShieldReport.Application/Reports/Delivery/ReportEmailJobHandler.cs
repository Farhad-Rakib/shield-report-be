using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShieldReport.Application.BackgroundJobs;
using ShieldReport.Application.Common.Configuration;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Notifications;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Reports.Delivery;

// Emails an approved report (PDF, optionally Word) to each recipient of a ReportDelivery — one
// message per recipient so client contacts don't see each other's addresses. Progress is saved
// after every email, so a retry (BackgroundJobWorkerService back-off) only sends to the rest.
// When the delivery completes, the client organization's portal users get an in-app notification.
public sealed class ReportEmailJobHandler : IBackgroundJobHandler
{
    public const string Type = "report.send-email";

    private readonly IReportDeliveryRepository _deliveryRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FrontendOptions _frontendOptions;

    public ReportEmailJobHandler(
        IReportDeliveryRepository deliveryRepository,
        IFileStorageService fileStorageService,
        IEmailService emailService,
        IUserRepository userRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        IOptions<FrontendOptions> frontendOptions)
    {
        _deliveryRepository = deliveryRepository;
        _fileStorageService = fileStorageService;
        _emailService = emailService;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _frontendOptions = frontendOptions.Value;
    }

    public string JobType => Type;

    public async Task HandleAsync(string payloadJson, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ReportEmailJobPayload>(payloadJson)
            ?? throw new InvalidOperationException("Invalid report email payload.");

        var delivery = await _deliveryRepository.GetByIdWithReportAsync(payload.DeliveryId, cancellationToken);
        if (delivery is null || delivery.Status == ReportDeliveryStatus.Sent)
        {
            return;
        }

        var report = delivery.Report;
        if (report.Status != ReportStatus.Approved || string.IsNullOrWhiteSpace(report.FinalPdfPath))
        {
            // Not retryable: the report was un-approved or its PDF removed since sending was requested.
            delivery.MarkAttemptFailed("The report is no longer approved or its PDF is missing.");
            _deliveryRepository.Update(delivery);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            return;
        }

        var attachments = new List<EmailAttachment>
        {
            new($"security-report-v{report.Version}.pdf", "application/pdf", await ReadAsync(report.FinalPdfPath, cancellationToken)),
        };
        if (delivery.AttachWord && !string.IsNullOrWhiteSpace(report.DraftDocxPath))
        {
            attachments.Add(new($"security-report-v{report.Version}.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", await ReadAsync(report.DraftDocxPath, cancellationToken)));
        }

        var engagementTitle = report.Engagement.Title;
        var clientName = report.Engagement.ClientOrganization?.Name ?? "your organization";
        var subject = $"[Confidential] Security Assessment Report v{report.Version} — {engagementTitle}";
        var body = BuildBody(clientName, engagementTitle, report.Version, delivery.Message);

        var remaining = delivery.GetRecipients().Except(delivery.GetDeliveredTo(), StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var recipient in remaining)
        {
            try
            {
                await _emailService.SendAsync(recipient, subject, body, attachments, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                delivery.MarkAttemptFailed($"Sending to {recipient} failed: {ex.Message}");
                _deliveryRepository.Update(delivery);
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                throw; // retried by the background job worker
            }

            delivery.MarkDeliveredTo(recipient);
            _deliveryRepository.Update(delivery);
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        if (delivery.Status == ReportDeliveryStatus.Sent)
        {
            await NotifyPortalUsersAsync(report.Engagement.ClientOrganizationId, engagementTitle, report.Version, cancellationToken);
        }
    }

    private async Task NotifyPortalUsersAsync(long clientOrganizationId, string engagementTitle, int version, CancellationToken cancellationToken)
    {
        foreach (var role in new[] { SystemRoles.ClientAdmin, SystemRoles.ClientUser })
        {
            foreach (var user in await _userRepository.GetByClientOrganizationAndRoleAsync(clientOrganizationId, role, cancellationToken))
            {
                if (!user.IsActive) continue;
                try
                {
                    await _notificationService.CreateNotificationAsync(user.Id, "REPORT_AVAILABLE", $"Security report v{version} for \"{engagementTitle}\" is available in your portal.", cancellationToken);
                }
                catch
                {
                    // In-app notification is best-effort; the email has already been delivered.
                }
            }
        }
    }

    private string BuildBody(string clientName, string engagementTitle, int version, string? message)
    {
        var portalUrl = $"{_frontendOptions.BaseUrl.TrimEnd('/')}/portal/reports";
        var note = string.IsNullOrWhiteSpace(message)
            ? string.Empty
            : $"<p style=\"white-space:pre-wrap;border-left:3px solid #0b5394;padding-left:10px\">{WebUtility.HtmlEncode(message)}</p>";

        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#222">
              <p>Dear {WebUtility.HtmlEncode(clientName)} team,</p>
              <p>Please find attached version {version} of the security assessment report for <strong>{WebUtility.HtmlEncode(engagementTitle)}</strong>.</p>
              {note}
              <p>The report is also available in your client portal: <a href="{WebUtility.HtmlEncode(portalUrl)}">{WebUtility.HtmlEncode(portalUrl)}</a></p>
              <p style="font-size:12px;color:#666">This report is confidential. Please share it only with people who need to act on it.</p>
            </div>
            """;
    }

    private async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var stream = _fileStorageService.OpenRead(path) ?? throw new InvalidOperationException($"Report file is missing from storage: {path}");
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
    }
}
