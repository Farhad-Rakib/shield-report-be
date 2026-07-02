using FluentValidation;
using ShieldReport.Application.Auth.Dtos;

namespace ShieldReport.Application.Auth.Validation;

public sealed class RevokeRefreshTokenRequestDtoValidator : AbstractValidator<RevokeRefreshTokenRequestDto>
{
    public RevokeRefreshTokenRequestDtoValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
