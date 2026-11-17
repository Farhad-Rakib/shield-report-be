using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Application.EmailSettings;

public interface IEmailSettingsService
{
    Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsRequestDto request, long updatedByUserId, CancellationToken cancellationToken = default);
    Task SendTestAsync(string to, CancellationToken cancellationToken = default);
}

public sealed class EmailSettingsService : IEmailSettingsService
{
    private readonly IRepository<EmailSetting> _repository;
    private readonly IApiKeyEncryptor _encryptor;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public EmailSettingsService(IRepository<EmailSetting> repository, IApiKeyEncryptor encryptor, IEmailService emailService, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _encryptor = encryptor;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public async Task<EmailSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var setting = await GetRowAsync(cancellationToken);
        return setting is null
            ? new EmailSettingsDto(false, string.Empty, 587, true, string.Empty, false, string.Empty, "ShieldReport", null)
            : ToDto(setting);
    }

    public async Task<EmailSettingsDto> UpdateAsync(UpdateEmailSettingsRequestDto request, long updatedByUserId, CancellationToken cancellationToken = default)
    {
        var setting = await GetRowAsync(cancellationToken);
        if (setting is null)
        {
            setting = new EmailSetting();
            await _repository.AddAsync(setting, cancellationToken);
        }
        else
        {
            _repository.Update(setting);
        }

        var passwordEncrypted = request.Password switch
        {
            null => null,
            "" => string.Empty,
            var password => _encryptor.Encrypt(password)
        };

        setting.Update(
            request.Host.Trim(), request.Port, request.UseSsl, request.Username?.Trim() ?? string.Empty, passwordEncrypted,
            request.FromAddress.Trim(), request.FromName?.Trim() ?? string.Empty, updatedByUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(setting);
    }

    // Sends with the saved settings, so a failure surfaces the SMTP server's own error.
    public async Task SendTestAsync(string to, CancellationToken cancellationToken = default)
    {
        const string body = "<p>This is a test email from ShieldReport.</p><p>If you can read it, outgoing mail is configured correctly.</p>";
        try
        {
            await _emailService.SendAsync(to, "ShieldReport test email", body, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var detail = ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";
            throw new AppException($"Test email failed: {detail}", 400);
        }
    }

    private async Task<EmailSetting?> GetRowAsync(CancellationToken cancellationToken) =>
        (await _repository.GetAllAsync(cancellationToken)).OrderBy(s => s.Id).FirstOrDefault();

    private static EmailSettingsDto ToDto(EmailSetting s) => new(
        !string.IsNullOrWhiteSpace(s.Host), s.Host, s.Port, s.UseSsl, s.Username, s.PasswordEncrypted.Length > 0,
        s.FromAddress, s.FromName, s.UpdatedAt == default ? null : s.UpdatedAt);
}
