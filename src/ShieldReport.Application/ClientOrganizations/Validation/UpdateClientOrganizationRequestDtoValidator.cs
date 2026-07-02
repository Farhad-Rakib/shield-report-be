using FluentValidation;
using ShieldReport.Application.ClientOrganizations.Dtos;

namespace ShieldReport.Application.ClientOrganizations.Validation;

public sealed class UpdateClientOrganizationRequestDtoValidator : AbstractValidator<UpdateClientOrganizationRequestDto>
{
    public UpdateClientOrganizationRequestDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PrimaryContactName).MaximumLength(200);
        RuleFor(x => x.PrimaryContactEmail).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.PrimaryContactEmail));
    }
}
