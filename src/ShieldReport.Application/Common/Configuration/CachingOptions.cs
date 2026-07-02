namespace ShieldReport.Application.Common.Configuration;

public sealed class CachingOptions
{
    public int PermissionsTtlMinutes { get; set; } = 30;
    public int MenusTtlMinutes { get; set; } = 30;
    public int RolesTtlMinutes { get; set; } = 30;
    public int RolePermissionsTtlMinutes { get; set; } = 30;
}
