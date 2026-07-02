using System.Security.Cryptography;
using System.Text;
using ShieldReport.Application.Auth;
using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Application.Common.Interfaces.Security;
using ShieldReport.Application.Common.Interfaces.Services;
using ShieldReport.Application.Common.Models;
using ShieldReport.Application.RegistrationInvites.Dtos;
using ShieldReport.Application.Users.Dtos;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.RegistrationInvites;

public sealed class RegistrationInviteService : IRegistrationInviteService
{
    private static readonly string[] ClientFacingRoles = [SystemRoles.ClientAdmin, SystemRoles.ClientUser];

    private readonly IRegistrationInviteRepository _registrationInviteRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRegistrationInviteTokenGenerator _tokenGenerator;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public RegistrationInviteService(
        IRegistrationInviteRepository registrationInviteRepository,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IRegistrationInviteTokenGenerator tokenGenerator,
        IEmailService emailService,
        IUnitOfWork unitOfWork)
    {
        _registrationInviteRepository = registrationInviteRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegistrationInviteDto> CreateInviteAsync(CreateRegistrationInviteRequestDto request, long createdByUserId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken)
            ?? throw new AppException("Role not found.", 404);

        var requiresClientOrganization = ClientFacingRoles.Contains(role.Name);
        if (requiresClientOrganization && request.ClientOrganizationId is null)
        {
            throw new AppException($"Client organization is required when inviting a {role.Name}.", 400);
        }

        if (!requiresClientOrganization && request.ClientOrganizationId is not null)
        {
            throw new AppException("Client organization only applies to client-facing roles.", 400);
        }

        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            throw new AppException("A user with this email already exists.", 400);
        }

        var generated = _tokenGenerator.Generate(request.Lifetime);
        var tokenHash = ComputeSha256(generated.Token);

        var invite = new RegistrationInvite(request.Email, tokenHash, role.Id, request.ClientOrganizationId, generated.ExpiresAtUtc, createdByUserId);
        await _registrationInviteRepository.AddAsync(invite, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var registrationLink = $"https://localhost:3000/register/{generated.Token}";
        var htmlBody = $@"
            <h2>You've Been Invited</h2>
            <p>You've been invited to join ShieldReport as a {role.Name}.</p>
            <p><a href='{registrationLink}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Complete Registration</a></p>
            <p>This invitation expires on {generated.ExpiresAtUtc:f} UTC.</p>
            <p>If you weren't expecting this invitation, you can safely ignore this email.</p>
        ";

        await _emailService.SendAsync(request.Email, "You've Been Invited to ShieldReport", htmlBody, cancellationToken);

        return ToDto(invite, role, clientOrganization: null, includeToken: generated.Token);
    }

    public async Task RevokeInviteAsync(long id, long revokedByUserId, CancellationToken cancellationToken = default)
    {
        var invite = await _registrationInviteRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new AppException("Invite not found.", 404);

        invite.Revoke(revokedByUserId);
        _registrationInviteRepository.Update(invite);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<RegistrationInviteDto>> ListInvitesAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _registrationInviteRepository.GetPagedAsync(request.Page, request.PageSize, request.Search, cancellationToken);
        var dtos = items.Select(invite => ToDto(invite, invite.Role, invite.ClientOrganization, includeToken: null)).ToList();

        return PagedResult<RegistrationInviteDto>.Create(dtos, total, request.Page, request.PageSize);
    }

    public async Task<RegistrationInviteValidationDto> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var invite = await GetValidInviteByTokenAsync(token, cancellationToken);
        return new RegistrationInviteValidationDto(invite.Email, invite.Role.Name, invite.ClientOrganization?.Name);
    }

    public async Task<UserRegistrationResult> CompleteRegistrationAsync(CompleteRegistrationRequestDto request, CancellationToken cancellationToken = default)
    {
        var invite = await GetValidInviteByTokenAsync(request.Token, cancellationToken);

        var existingUser = await _userRepository.GetByEmailAsync(invite.Email, cancellationToken);
        if (existingUser is not null)
        {
            throw new AppException("A user with this email already exists.", 400);
        }

        var hashedPassword = _passwordHasher.Hash(request.Password);
        var user = new User(request.FullName, invite.Email, hashedPassword);

        if (invite.ClientOrganizationId.HasValue)
        {
            user.SetClientPortalContext(invite.ClientOrganizationId.Value);
        }

        user.SetRoles([new UserRole { User = user, Role = invite.Role }]);

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        invite.MarkConsumed(user.Id);
        _registrationInviteRepository.Update(invite);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserRegistrationResult(new UserDto(user.Id, user.FullName, user.Email, user.IsActive, [invite.Role.Name]));
    }

    private async Task<RegistrationInvite> GetValidInviteByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var tokenHash = ComputeSha256(token);
        var invite = await _registrationInviteRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (invite is null || !invite.IsValid)
        {
            throw new AppException("Invalid or expired invite.", 400);
        }

        return invite;
    }

    private static RegistrationInviteDto ToDto(RegistrationInvite invite, Role role, ClientOrganization? clientOrganization, string? includeToken)
    {
        var status = invite switch
        {
            { RevokedAtUtc: not null } => "Revoked",
            { ConsumedAtUtc: not null } => "Consumed",
            _ when invite.ExpiresAtUtc <= DateTime.UtcNow => "Expired",
            _ => "Pending"
        };

        return new RegistrationInviteDto(
            invite.Id,
            invite.Email,
            role.Name,
            invite.ClientOrganizationId,
            clientOrganization?.Name,
            invite.ExpiresAtUtc,
            invite.ConsumedAtUtc,
            invite.RevokedAtUtc,
            status,
            includeToken);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
