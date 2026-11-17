using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Infrastructure.Email;

// Sends with the settings saved on Administration -> Email Configuration (email_settings). Until
// those are saved, the Smtp:* configuration (SMTP_* env vars) is used, so an existing deployment
// keeps working after upgrade.
public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _fallback;
    private readonly IRepository<EmailSetting> _settings;
    private readonly IApiKeyEncryptor _encryptor;

    public SmtpEmailService(IOptions<SmtpOptions> smtpOptions, IRepository<EmailSetting> settings, IApiKeyEncryptor encryptor)
    {
        _fallback = smtpOptions.Value;
        _settings = settings;
        _encryptor = encryptor;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
        SendAsync(to, subject, htmlBody, Array.Empty<EmailAttachment>(), cancellationToken);

    public async Task SendAsync(string to, string subject, string htmlBody, IReadOnlyList<EmailAttachment> attachments, CancellationToken cancellationToken = default)
    {
        var smtp = await ResolveAsync(cancellationToken);

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress, string.IsNullOrWhiteSpace(smtp.FromName) ? null : smtp.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(to));

        // MailMessage disposes the attachments (and their streams) with the message.
        foreach (var attachment in attachments)
        {
            message.Attachments.Add(new Attachment(new MemoryStream(attachment.Content), attachment.FileName, attachment.ContentType));
        }

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl,
            // An empty username means the server accepts mail without authentication.
            Credentials = string.IsNullOrWhiteSpace(smtp.Username) ? null : new NetworkCredential(smtp.Username, smtp.Password)
        };

        using var registration = cancellationToken.Register(client.SendAsyncCancel);
        await client.SendMailAsync(message, cancellationToken);
    }

    private async Task<SmtpOptions> ResolveAsync(CancellationToken cancellationToken)
    {
        var saved = (await _settings.GetAllAsync(cancellationToken)).OrderBy(s => s.Id).FirstOrDefault();
        if (saved is not null && !string.IsNullOrWhiteSpace(saved.Host))
        {
            return new SmtpOptions
            {
                Host = saved.Host,
                Port = saved.Port,
                UseSsl = saved.UseSsl,
                Username = saved.Username,
                Password = saved.PasswordEncrypted.Length > 0 ? _encryptor.Decrypt(saved.PasswordEncrypted) : string.Empty,
                FromAddress = saved.FromAddress,
                FromName = saved.FromName
            };
        }

        if (!string.IsNullOrWhiteSpace(_fallback.Host) && !string.IsNullOrWhiteSpace(_fallback.FromAddress))
        {
            return _fallback;
        }

        throw new AppException("Email is not configured. Set it up under Administration → Email Configuration.", 503);
    }
}
