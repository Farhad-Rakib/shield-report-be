using System.Net.Mail;
using ShieldReport.Application.BackgroundJobs;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Reports.Delivery;

public interface IReportDeliveryService
{
    Task<IReadOnlyList<ReportRecipientOptionDto>> GetRecipientOptionsAsync(long reportId, CancellationToken cancellationToken = default);
    Task<ReportDeliveryDto> SendAsync(long reportId, SendReportRequestDto request, long sentByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportDeliveryDto>> ListAsync(long reportId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientReportDto>> ListForClientAsync(long clientOrganizationId, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName, string ContentType)> GetClientFileAsync(long reportId, long clientOrganizationId, bool word, CancellationToken cancellationToken = default);
}

// Sending approved reports to the client, and the client portal's read-only access to them.
// Recipients are restricted to the client organization's own contacts (primary contact + its portal
// users) so a confidential report can't be emailed to an arbitrary address by mistake.
public sealed class ReportDeliveryService : IReportDeliveryService
{
    private const int MaxRecipients = 20;

    private readonly IReportRepository _reportRepository;
    private readonly IReportDeliveryRepository _deliveryRepository;
    private readonly IEngagementRepository _engagementRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBackgroundJobQueue _jobQueue;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public ReportDeliveryService(
        IReportRepository reportRepository,
        IReportDeliveryRepository deliveryRepository,
        IEngagementRepository engagementRepository,
        IUserRepository userRepository,
        IBackgroundJobQueue jobQueue,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork)
    {
        _reportRepository = reportRepository;
        _deliveryRepository = deliveryRepository;
        _engagementRepository = engagementRepository;
        _userRepository = userRepository;
        _jobQueue = jobQueue;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ReportRecipientOptionDto>> GetRecipientOptionsAsync(long reportId, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(reportId, cancellationToken)
            ?? throw new AppException("Report not found.", 404);
        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(report.EngagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);
        return await BuildRecipientOptionsAsync(engagement, cancellationToken);
    }

    public async Task<ReportDeliveryDto> SendAsync(long reportId, SendReportRequestDto request, long sentByUserId, CancellationToken cancellationToken = default)
    {
        var report = await _reportRepository.GetByIdAsync(reportId, cancellationToken)
            ?? throw new AppException("Report not found.", 404);

        if (report.Status != ReportStatus.Approved)
        {
            throw new AppException("Only an approved report can be sent to the client.", 409);
        }

        if (string.IsNullOrWhiteSpace(report.FinalPdfPath))
        {
            throw new AppException("This report has no PDF to send. Regenerate or convert it first.", 409);
        }

        if (request.AttachWord && string.IsNullOrWhiteSpace(report.DraftDocxPath))
        {
            throw new AppException("This report has no Word document to attach.", 409);
        }

        var recipients = (request.Recipients ?? [])
            .Select(r => r.Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0 || recipients.Count > MaxRecipients)
        {
            throw new AppException($"Choose between 1 and {MaxRecipients} recipients.", 400);
        }

        if (request.Message is { Length: > 4000 })
        {
            throw new AppException("The message must be 4,000 characters or fewer.", 400);
        }

        var engagement = await _engagementRepository.GetByIdWithDetailsAsync(report.EngagementId, cancellationToken)
            ?? throw new AppException("Engagement not found.", 404);
        var allowed = (await BuildRecipientOptionsAsync(engagement, cancellationToken))
            .Select(o => o.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var notAllowed = recipients.Where(r => !allowed.Contains(r) || !IsValidEmail(r)).ToList();
        if (notAllowed.Count > 0)
        {
            throw new AppException($"Reports can only be sent to this client's contacts. Not allowed: {string.Join(", ", notAllowed)}. Add them as the client's primary contact or invite them to the portal first.", 400);
        }

        var delivery = new ReportDelivery(report.Id, sentByUserId, recipients, request.Message, request.AttachWord);
        await _deliveryRepository.AddAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Durable job, retried with back-off; queued only once the delivery row exists.
        await _jobQueue.EnqueueAsync(ReportEmailJobHandler.Type, new ReportEmailJobPayload(delivery.Id), maxAttempts: 5, cancellationToken: cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(delivery, cancellationToken);
    }

    public async Task<IReadOnlyList<ReportDeliveryDto>> ListAsync(long reportId, CancellationToken cancellationToken = default)
    {
        var deliveries = await _deliveryRepository.GetByReportIdAsync(reportId, cancellationToken);
        var result = new List<ReportDeliveryDto>();
        foreach (var delivery in deliveries)
        {
            result.Add(await ToDtoAsync(delivery, cancellationToken));
        }

        return result;
    }

    public async Task<IReadOnlyList<ClientReportDto>> ListForClientAsync(long clientOrganizationId, CancellationToken cancellationToken = default)
    {
        var reports = await _reportRepository.GetApprovedByClientOrganizationAsync(clientOrganizationId, cancellationToken);
        return reports
            .Select(r => new ClientReportDto(r.Id, r.Engagement.Title, r.Version, r.ApprovedAt, !string.IsNullOrWhiteSpace(r.FinalPdfPath), !string.IsNullOrWhiteSpace(r.DraftDocxPath)))
            .ToList();
    }

    public async Task<(Stream Content, string FileName, string ContentType)> GetClientFileAsync(long reportId, long clientOrganizationId, bool word, CancellationToken cancellationToken = default)
    {
        // Same 404 for "doesn't exist", "other organization" and "not approved": never reveal that
        // a draft or another client's report exists.
        var report = (await _reportRepository.GetApprovedByClientOrganizationAsync(clientOrganizationId, cancellationToken))
            .FirstOrDefault(r => r.Id == reportId)
            ?? throw new AppException("Report not found.", 404);

        var path = word ? report.DraftDocxPath : report.FinalPdfPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new AppException("Report not found.", 404);
        }

        var stream = _fileStorageService.OpenRead(path) ?? throw new AppException("Report not found.", 404);
        return word
            ? (stream, $"security-report-v{report.Version}.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            : (stream, $"security-report-v{report.Version}.pdf", "application/pdf");
    }

    private async Task<IReadOnlyList<ReportRecipientOptionDto>> BuildRecipientOptionsAsync(Engagement engagement, CancellationToken cancellationToken)
    {
        var options = new List<ReportRecipientOptionDto>();
        var organization = engagement.ClientOrganization;
        if (organization is not null && !string.IsNullOrWhiteSpace(organization.PrimaryContactEmail) && IsValidEmail(organization.PrimaryContactEmail))
        {
            options.Add(new ReportRecipientOptionDto(organization.PrimaryContactEmail.Trim(), organization.PrimaryContactName ?? organization.PrimaryContactEmail, "Primary contact"));
        }

        foreach (var (role, label) in new[] { (SystemRoles.ClientAdmin, "Client admin"), (SystemRoles.ClientUser, "Client user") })
        {
            foreach (var user in await _userRepository.GetByClientOrganizationAndRoleAsync(engagement.ClientOrganizationId, role, cancellationToken))
            {
                if (user.IsActive && IsValidEmail(user.Email))
                {
                    options.Add(new ReportRecipientOptionDto(user.Email, user.FullName, label));
                }
            }
        }

        return options.DistinctBy(o => o.Email.ToLowerInvariant()).ToList();
    }

    private async Task<ReportDeliveryDto> ToDtoAsync(ReportDelivery d, CancellationToken cancellationToken)
    {
        var sender = await _userRepository.GetByIdAsync(d.SentByUserId, cancellationToken);
        return new ReportDeliveryDto(d.Id, d.ReportId, d.Status.ToString(), d.GetRecipients(), d.GetDeliveredTo(), d.Message, d.AttachWord,
            d.SentByUserId, sender?.FullName, d.CreatedAt, d.CompletedAt, d.LastError);
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            return new MailAddress(value).Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
