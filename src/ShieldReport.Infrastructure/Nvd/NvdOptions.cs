namespace ShieldReport.Infrastructure.Nvd;

public sealed class NvdOptions
{
    public const string SectionName = "Nvd";

    public string BaseUrl { get; set; } = "https://services.nvd.nist.gov/rest/json/cves/2.0";

    // Optional — NVD allows unauthenticated requests at a much lower rate limit; set this in
    // config to raise it. Never required for the lookup to function.
    public string? ApiKey { get; set; }
}
