using FluentValidation;
using ShieldReport.Application.Auth.Dtos;

namespace ShieldReport.Application.Auth.Validation;

public sealed class MfaVerifyRequestDtoValidator : AbstractValidator<MfaVerifyRequestDto>
{
    public MfaVerifyRequestDtoValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty();
    }
}
