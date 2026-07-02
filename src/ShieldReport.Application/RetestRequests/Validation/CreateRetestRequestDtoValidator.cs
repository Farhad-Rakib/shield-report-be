using FluentValidation;
using ShieldReport.Application.RetestRequests.Dtos;

namespace ShieldReport.Application.RetestRequests.Validation;

public sealed class CreateRetestRequestDtoValidator : AbstractValidator<CreateRetestRequestDto>
{
    public CreateRetestRequestDtoValidator()
    {
        RuleFor(x => x.AssignedToUserId).GreaterThan(0).When(x => x.AssignedToUserId.HasValue);
        RuleFor(x => x.Instructions).MaximumLength(2000);
        RuleForEach(x => x.Cases).NotEmpty().MaximumLength(500).When(x => x.Cases is not null);
        RuleFor(x => x.Cases).Must(c => c == null || c.Count <= 50)
            .WithMessage("A retest request can include at most 50 checklist items.");
    }
}
