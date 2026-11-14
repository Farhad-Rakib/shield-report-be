using FluentValidation;
using ShieldReport.Application.Scans.Dtos;

namespace ShieldReport.Application.Scans.Validation;

public sealed class CreateScanRequestDtoValidator : AbstractValidator<CreateScanRequestDto>
{
    public CreateScanRequestDtoValidator()
    {
        // Tools may be empty when only custom tools are selected (CustomToolIds), but not both.
        RuleFor(x => x)
            .Must(x => (x.Tools?.Length ?? 0) > 0 || (x.CustomToolIds?.Length ?? 0) > 0)
            .WithName("Tools")
            .WithMessage("Select at least one scan tool.");
        RuleForEach(x => x.Tools).IsInEnum();
        // Every scan must be scoped to an engagement — a scan with no engagement produces
        // vulnerabilities with EngagementId null, which no report can ever pull in (report
        // generation is always scoped to one engagement's included findings).
        RuleFor(x => x.EngagementId).NotNull().GreaterThan(0).WithMessage("A scan must be scoped to an engagement.");
    }
}
