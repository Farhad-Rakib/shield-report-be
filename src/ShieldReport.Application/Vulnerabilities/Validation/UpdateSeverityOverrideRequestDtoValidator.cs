using FluentValidation;
using ShieldReport.Application.Vulnerabilities.Dtos;

namespace ShieldReport.Application.Vulnerabilities.Validation;

public sealed class UpdateSeverityOverrideRequestDtoValidator : AbstractValidator<UpdateSeverityOverrideRequestDto>
{
    public UpdateSeverityOverrideRequestDtoValidator()
    {
        RuleFor(x => x.SeverityOverride).IsInEnum().When(x => x.SeverityOverride.HasValue);
    }
}
