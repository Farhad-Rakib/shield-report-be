using FluentValidation;
using ShieldReport.Application.Scans.Dtos;

namespace ShieldReport.Application.Scans.Validation;

public sealed class CreateScanRequestDtoValidator : AbstractValidator<CreateScanRequestDto>
{
    public CreateScanRequestDtoValidator()
    {
        RuleFor(x => x.Tools).NotEmpty();
        RuleForEach(x => x.Tools).IsInEnum();
    }
}
