using ShieldReport.Application.Common.Exceptions;
using ShieldReport.Application.Common.Interfaces;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Application.Vulnerabilities;

public sealed class CvssSeverityMappingService : ICvssSeverityMappingService
{
    private readonly ICvssSeverityBandRepository _bandRepository;

    public CvssSeverityMappingService(ICvssSeverityBandRepository bandRepository)
    {
        _bandRepository = bandRepository;
    }

    public async Task<Severity> MapAsync(decimal cvssScore, CancellationToken cancellationToken = default)
    {
        if (cvssScore < 0m || cvssScore > 10m)
        {
            throw new AppException("CVSS score must be between 0.0 and 10.0.", 400);
        }

        var band = await _bandRepository.FindBandForScoreAsync(cvssScore, cancellationToken)
            ?? throw new AppException($"No severity band is configured for CVSS score {cvssScore}.", 500);

        return band.Severity;
    }
}
