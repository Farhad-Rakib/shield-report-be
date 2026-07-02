using FluentValidation;
using ShieldReport.Application.Auth.Dtos;

namespace ShieldReport.Application.Auth.Validation;

public sealed class ForgotPasswordRequestDtoValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
