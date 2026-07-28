using FluentValidation;
using ShieldReport.Application.Scans.Dtos;

namespace ShieldReport.Application.Scans.Validation;

public sealed class CreateScanRequestDtoValidator : AbstractValidator<CreateScanRequestDto>
{
    public CreateScanRequestDtoValidator()
    {
        RuleFor(x => x.Tools).NotEmpty();
        RuleForEach(x => x.Tools).IsInEnum();
        // Every scan must be scoped to an engagement — a scan with no engagement produces
        // vulnerabilities with EngagementId null, which no report can ever pull in (report
        // generation is always scoped to one engagement's included findings).
        RuleFor(x => x.EngagementId).NotNull().GreaterThan(0).WithMessage("A scan must be scoped to an engagement.");
    }
}
