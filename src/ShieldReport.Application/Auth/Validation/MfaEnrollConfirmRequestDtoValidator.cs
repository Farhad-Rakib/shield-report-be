using FluentValidation;
using ShieldReport.Application.Auth.Dtos;

namespace ShieldReport.Application.Auth.Validation;

public sealed class MfaEnrollConfirmRequestDtoValidator : AbstractValidator<MfaEnrollConfirmRequestDto>
{
    public MfaEnrollConfirmRequestDtoValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}
