using FluentValidation;
using ShieldReport.Application.Engagements.Dtos;

namespace ShieldReport.Application.Engagements.Validation;

public sealed class CreateEngagementRequestDtoValidator : AbstractValidator<CreateEngagementRequestDto>
{
    public CreateEngagementRequestDtoValidator()
    {
        RuleFor(x => x.ClientOrganizationId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.LeadPentesterId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}
