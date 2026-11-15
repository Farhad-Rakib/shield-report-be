namespace ShieldReport.Application.Users.Dtos;

// Staff user created by an admin (Users page). Client-portal users are created through
// registration invites instead, which tie them to their organization.
public sealed record CreateUserRequestDto(string FullName, string Email, string Password, IReadOnlyList<long> RoleIds);
