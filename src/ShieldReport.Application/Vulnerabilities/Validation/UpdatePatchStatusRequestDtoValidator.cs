using FluentValidation;
using ShieldReport.Application.Vulnerabilities.Dtos;

namespace ShieldReport.Application.Vulnerabilities.Validation;

public sealed class UpdatePatchStatusRequestDtoValidator : AbstractValidator<UpdatePatchStatusRequestDto>
{
    public UpdatePatchStatusRequestDtoValidator()
    {
        RuleFor(x => x.PatchStatus).IsInEnum();
    }
}
