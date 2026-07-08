namespace ShieldReport.Api.Common;

public static class RefreshTokenCookie
{
    public const string CookieName = "refreshToken";

    public static void Set(HttpResponse response, IWebHostEnvironment environment, IConfiguration configuration, string token, DateTime expiresAtUtc)
    {
        response.Cookies.Append(CookieName, token, BuildOptions(environment, configuration, expiresAtUtc));
    }

    public static void Delete(HttpResponse response, IWebHostEnvironment environment, IConfiguration configuration)
    {
        response.Cookies.Delete(CookieName, BuildOptions(environment, configuration, DateTime.UnixEpoch));
    }

    private static CookieOptions BuildOptions(IWebHostEnvironment environment, IConfiguration configuration, DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,
        // Browsers silently refuse to store/send Secure cookies over a plain-HTTP origin (no
        // exception for LAN IPs, only literal "localhost"). Cookies:RequireSecure lets an internal
        // HTTP-only deployment (e.g. an office LAN demo) opt out without touching ASPNETCORE_ENVIRONMENT.
        Secure = configuration.GetValue("Cookies:RequireSecure", true),
        // The FE dev server (http://localhost:5173) and BE (https://localhost:5001) differ in
        // scheme, so browsers treat them as cross-site under schemeful same-site rules — Strict
        // (or even Lax) would silently drop the cookie in local dev. Only Production, where FE
        // and BE are expected to share a registrable domain/scheme, gets the hardened Strict value.
        SameSite = environment.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Strict,
        Expires = expiresAtUtc,
        Path = "/api"
    };
}
