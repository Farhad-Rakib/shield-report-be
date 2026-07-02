namespace ShieldReport.Application.Common.Interfaces.Services;

public sealed record NvdCveResult(string CveId, string? Description, decimal? CvssScore, string? CvssVector);

// Wraps services.nvd.nist.gov/rest/json/cves/2.0 — implemented in Infrastructure since it's a
// plain HTTP client wrapper, same layering reason as IEmailService/IJwtTokenGenerator.
public interface INvdClient
{
    Task<NvdCveResult?> LookupAsync(string cveId, CancellationToken cancellationToken = default);
}
