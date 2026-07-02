using FluentValidation;
using ShieldReport.Application.RegistrationInvites.Dtos;

namespace ShieldReport.Application.RegistrationInvites.Validation;

public sealed class CompleteRegistrationRequestDtoValidator : AbstractValidator<CompleteRegistrationRequestDto>
{
    public CompleteRegistrationRequestDtoValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}
