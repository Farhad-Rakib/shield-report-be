using FluentValidation;
using ShieldReport.Application.ClientAssets.Dtos;

namespace ShieldReport.Application.ClientAssets.Validation;

public sealed class CreateClientAssetRequestDtoValidator : AbstractValidator<CreateClientAssetRequestDto>
{
    public CreateClientAssetRequestDtoValidator()
    {
        RuleFor(x => x.ClientOrganizationId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AssetType).IsInEnum();
        RuleFor(x => x.Environment).IsInEnum();
        RuleFor(x => x.Criticality).IsInEnum();
    }
}
