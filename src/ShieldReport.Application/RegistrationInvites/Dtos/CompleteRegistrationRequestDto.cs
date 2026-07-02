namespace ShieldReport.Application.RegistrationInvites.Dtos;

public sealed record CompleteRegistrationRequestDto(
    string Token,
    string FullName,
    string Password);
