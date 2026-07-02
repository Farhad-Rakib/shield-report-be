namespace ShieldReport.Api.Common;

public static class RefreshTokenCookie
{
    public const string CookieName = "refreshToken";

    public static void Set(HttpResponse response, IWebHostEnvironment environment, string token, DateTime expiresAtUtc)
    {
        response.Cookies.Append(CookieName, token, BuildOptions(environment, expiresAtUtc));
    }

    public static void Delete(HttpResponse response, IWebHostEnvironment environment)
    {
        response.Cookies.Delete(CookieName, BuildOptions(environment, DateTime.UnixEpoch));
    }

    private static CookieOptions BuildOptions(IWebHostEnvironment environment, DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,
        // The FE dev server (http://localhost:5173) and BE (https://localhost:5001) differ in
        // scheme, so browsers treat them as cross-site under schemeful same-site rules — Strict
        // (or even Lax) would silently drop the cookie in local dev. Only Production, where FE
        // and BE are expected to share a registrable domain/scheme, gets the hardened Strict value.
        SameSite = environment.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Strict,
        Expires = expiresAtUtc,
        Path = "/api"
    };
}
