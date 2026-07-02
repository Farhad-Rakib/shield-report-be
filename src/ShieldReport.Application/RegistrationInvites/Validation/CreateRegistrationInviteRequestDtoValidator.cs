using FluentValidation;
using ShieldReport.Application.RegistrationInvites.Dtos;

namespace ShieldReport.Application.RegistrationInvites.Validation;

public sealed class CreateRegistrationInviteRequestDtoValidator : AbstractValidator<CreateRegistrationInviteRequestDto>
{
    public CreateRegistrationInviteRequestDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.RoleId).GreaterThan(0);
        RuleFor(x => x.Lifetime).IsInEnum();
    }
}
