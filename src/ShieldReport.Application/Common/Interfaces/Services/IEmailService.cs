namespace ShieldReport.Application.Common.Interfaces.Services;

public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);

    Task SendAsync(string to, string subject, string htmlBody, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default);
}

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
