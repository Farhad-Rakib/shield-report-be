using FluentValidation;

namespace ShieldReport.Application.EmailSettings;

// The password is never returned: HasPassword says whether one is stored.
// IsConfigured is false until an admin saves the form; mail then falls back to the
// server's Smtp:* configuration (SMTP_* env vars in docker-compose).
public sealed record EmailSettingsDto(
    bool IsConfigured,
    string Host,
    int Port,
    bool UseSsl,
    string Username,
    bool HasPassword,
    string FromAddress,
    string FromName,
    DateTime? UpdatedAt);

// Password: null keeps the stored one, "" clears it, anything else replaces it.
public sealed record UpdateEmailSettingsRequestDto(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string FromAddress,
    string? FromName);

public sealed record SendTestEmailRequestDto(string To);

public sealed class UpdateEmailSettingsRequestDtoValidator : AbstractValidator<UpdateEmailSettingsRequestDto>
{
    public UpdateEmailSettingsRequestDtoValidator()
    {
        RuleFor(x => x.Host).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Port).InclusiveBetween(1, 65535);
        RuleFor(x => x.Username).MaximumLength(255);
        RuleFor(x => x.Password).MaximumLength(500);
        RuleFor(x => x.FromAddress).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.FromName).MaximumLength(150);
    }
}

public sealed class SendTestEmailRequestDtoValidator : AbstractValidator<SendTestEmailRequestDto>
{
    public SendTestEmailRequestDtoValidator()
    {
        RuleFor(x => x.To).NotEmpty().EmailAddress().MaximumLength(255);
    }
}
