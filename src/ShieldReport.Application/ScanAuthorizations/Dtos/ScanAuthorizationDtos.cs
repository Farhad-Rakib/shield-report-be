using FluentValidation;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.ScanAuthorizations.Dtos;

// Status: Active | Scheduled (starts in the future) | Expired | Revoked
public sealed record ScanAuthorizationDto(
    long Id,
    long ClientAssetId,
    long? EngagementId,
    string Status,
    DateTime ValidFrom,
    DateTime? ValidUntil,
    IReadOnlyList<string> AllowedTools,
    string? ClientContactName,
    string? ClientContactEmail,
    string? Notes,
    long AuthorizedByUserId,
    string? AuthorizedByEmail,
    DateTime CreatedAt,
    bool HasEvidence,
    string? EvidenceFileName,
    DateTime? EvidenceUploadedAt,
    DateTime? RevokedAt,
    long? RevokedByUserId,
    string? RevokedByEmail,
    string? RevocationReason);

// All optional: dates default to the engagement's start/end (or now → +30 days without one),
// tools default to all.
public sealed record CreateScanAuthorizationRequestDto(
    long? EngagementId,
    DateTime? ValidFrom,
    DateTime? ValidUntil,
    ScanTool[]? AllowedTools,
    string? ClientContactName,
    string? ClientContactEmail,
    string? Notes);

public sealed record RevokeScanAuthorizationRequestDto(string Reason);

public sealed class CreateScanAuthorizationRequestDtoValidator : AbstractValidator<CreateScanAuthorizationRequestDto>
{
    public CreateScanAuthorizationRequestDtoValidator()
    {
        RuleForEach(x => x.AllowedTools).IsInEnum();
        RuleFor(x => x.ClientContactName).MaximumLength(200);
        RuleFor(x => x.ClientContactEmail).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ClientContactEmail));
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ValidUntil).GreaterThan(x => x.ValidFrom!.Value)
            .When(x => x.ValidFrom.HasValue && x.ValidUntil.HasValue)
            .WithMessage("The authorization must end after it starts.");
    }
}

public sealed class RevokeScanAuthorizationRequestDtoValidator : AbstractValidator<RevokeScanAuthorizationRequestDto>
{
    public RevokeScanAuthorizationRequestDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
