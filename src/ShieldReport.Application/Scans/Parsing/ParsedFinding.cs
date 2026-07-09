namespace ShieldReport.Application.Scans.Parsing;

// Description/ProofOfConcept/CvssVector/CvssScore/ReferenceLinks map straight onto the matching
// Vulnerability fields (VulnerabilityDedupService) — all optional since Naabu/Reconftw have no
// equivalent data to offer, only Nuclei populates most of them.
public sealed record ParsedFinding(
    string Title,
    string Endpoint,
    string? SeverityRaw,
    string? Description = null,
    string? ProofOfConcept = null,
    string? CvssVector = null,
    decimal? CvssScore = null,
    IReadOnlyList<string>? ReferenceLinks = null);
