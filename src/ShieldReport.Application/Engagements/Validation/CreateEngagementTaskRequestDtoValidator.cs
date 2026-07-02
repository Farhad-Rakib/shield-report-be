using FluentValidation;
using ShieldReport.Application.Engagements.Dtos;

namespace ShieldReport.Application.Engagements.Validation;

public sealed class CreateEngagementTaskRequestDtoValidator : AbstractValidator<CreateEngagementTaskRequestDto>
{
    public CreateEngagementTaskRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.AssignedToUserId).GreaterThan(0);
        RuleForEach(x => x.ClientAssetIds).GreaterThan(0);
    }
}
