using FluentValidation;
using ShieldReport.Application.RetestRequests.Dtos;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.RetestRequests.Validation;

public sealed class ResolveRetestRequestDtoValidator : AbstractValidator<ResolveRetestRequestDto>
{
    public ResolveRetestRequestDtoValidator()
    {
        RuleFor(x => x.Outcome)
            .Must(o => o is RetestRequestStatus.VerifiedClosed or RetestRequestStatus.Reopened)
            .WithMessage("Outcome must be VerifiedClosed or Reopened.");
        RuleFor(x => x.ResolutionNotes).MaximumLength(2000);
    }
}
