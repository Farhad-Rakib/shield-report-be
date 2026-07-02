using ShieldReport.Domain.Enums;

namespace ShieldReport.Domain.Entities;

public sealed class CvssSeverityBand : BaseEntity
{
    public Severity Severity { get; private set; }
    public decimal MinScore { get; private set; }
    public decimal MaxScore { get; private set; }

    private CvssSeverityBand()
    {
    }

    public CvssSeverityBand(Severity severity, decimal minScore, decimal maxScore)
    {
        if (minScore > maxScore)
        {
            throw new ArgumentException("MinScore cannot be greater than MaxScore.", nameof(minScore));
        }

        Severity = severity;
        MinScore = minScore;
        MaxScore = maxScore;
    }
}
