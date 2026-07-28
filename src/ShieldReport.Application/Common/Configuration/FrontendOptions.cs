namespace ShieldReport.Application.Common.Configuration;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    // Origin the SPA is actually served from — used to build links (registration invite,
    // password reset) that get emailed out. Must match wherever the frontend is really
    // running (npm run dev's 5173, or the LAN docker-compose FE_PORT), not a guess.
    public string BaseUrl { get; set; } = "http://localhost:5173";
}
