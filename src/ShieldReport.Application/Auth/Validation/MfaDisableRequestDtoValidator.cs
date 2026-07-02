using FluentValidation;
using ShieldReport.Application.Auth.Dtos;

namespace ShieldReport.Application.Auth.Validation;

public sealed class MfaDisableRequestDtoValidator : AbstractValidator<MfaDisableRequestDto>
{
    public MfaDisableRequestDtoValidator()
    {
        RuleFor(x => x.Code).NotEmpty();
    }
}
