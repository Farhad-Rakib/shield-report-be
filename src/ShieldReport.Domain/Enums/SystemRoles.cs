namespace ShieldReport.Domain.Enums;

public static class SystemRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string User = "User";

    // PentestOps roles — net-new, not previously seeded (see PRD-PentestOps.md §3).
    public const string Pentester = "PENTESTER";
    public const string ClientAdmin = "CLIENT_ADMIN";
    public const string ClientUser = "CLIENT_USER";
}
