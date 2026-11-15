using FluentValidation;
using ShieldReport.Application.Users.Dtos;

namespace ShieldReport.Application.Users.Validation;

public sealed class CreateUserRequestDtoValidator : AbstractValidator<CreateUserRequestDto>
{
    public CreateUserRequestDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.RoleIds).NotNull().NotEmpty().WithMessage("Choose at least one role.");
    }
}
