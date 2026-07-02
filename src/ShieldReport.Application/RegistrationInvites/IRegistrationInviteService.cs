using ShieldReport.Application.Auth;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.RegistrationInvites.Dtos;

namespace ShieldReport.Application.RegistrationInvites;

public interface IRegistrationInviteService
{
    Task<RegistrationInviteDto> CreateInviteAsync(CreateRegistrationInviteRequestDto request, long createdByUserId, CancellationToken cancellationToken = default);
    Task RevokeInviteAsync(long id, long revokedByUserId, CancellationToken cancellationToken = default);
    Task<PagedResult<RegistrationInviteDto>> ListInvitesAsync(PagedRequest request, CancellationToken cancellationToken = default);
    Task<RegistrationInviteValidationDto> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<UserRegistrationResult> CompleteRegistrationAsync(CompleteRegistrationRequestDto request, CancellationToken cancellationToken = default);
}
