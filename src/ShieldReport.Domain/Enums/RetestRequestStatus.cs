namespace ShieldReport.Domain.Enums;

public enum RetestRequestStatus : byte
{
    Pending = 1,
    VerifiedClosed = 2,
    Reopened = 3
}
