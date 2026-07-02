using FluentValidation;
using ShieldReport.Application.Engagements.Dtos;

namespace ShieldReport.Application.Engagements.Validation;

public sealed class UpdateEngagementTaskRequestDtoValidator : AbstractValidator<UpdateEngagementTaskRequestDto>
{
    public UpdateEngagementTaskRequestDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}
