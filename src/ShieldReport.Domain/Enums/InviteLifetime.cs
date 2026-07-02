namespace ShieldReport.Domain.Enums;

public enum InviteLifetime
{
    TwentyFourHours,
    SevenDays,
    ThirtyDays
}

public static class InviteLifetimeExtensions
{
    public static TimeSpan ToTimeSpan(this InviteLifetime lifetime) => lifetime switch
    {
        InviteLifetime.TwentyFourHours => TimeSpan.FromHours(24),
        InviteLifetime.SevenDays => TimeSpan.FromDays(7),
        InviteLifetime.ThirtyDays => TimeSpan.FromDays(30),
        _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported invite lifetime.")
    };
}
