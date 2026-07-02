using FluentValidation;
using ShieldReport.Application.Engagements.Dtos;

namespace ShieldReport.Application.Engagements.Validation;

public sealed class UpdateEngagementRequestDtoValidator : AbstractValidator<UpdateEngagementRequestDto>
{
    public UpdateEngagementRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}
