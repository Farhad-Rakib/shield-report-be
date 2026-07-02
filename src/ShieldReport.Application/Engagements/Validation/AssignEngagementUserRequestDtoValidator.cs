using FluentValidation;
using ShieldReport.Application.Engagements.Dtos;

namespace ShieldReport.Application.Engagements.Validation;

public sealed class AssignEngagementUserRequestDtoValidator : AbstractValidator<AssignEngagementUserRequestDto>
{
    public AssignEngagementUserRequestDtoValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
