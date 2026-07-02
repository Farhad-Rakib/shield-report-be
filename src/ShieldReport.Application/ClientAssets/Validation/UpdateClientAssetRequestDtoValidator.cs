using FluentValidation;
using ShieldReport.Application.ClientAssets.Dtos;

namespace ShieldReport.Application.ClientAssets.Validation;

public sealed class UpdateClientAssetRequestDtoValidator : AbstractValidator<UpdateClientAssetRequestDto>
{
    public UpdateClientAssetRequestDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Environment).IsInEnum();
        RuleFor(x => x.Criticality).IsInEnum();
    }
}
