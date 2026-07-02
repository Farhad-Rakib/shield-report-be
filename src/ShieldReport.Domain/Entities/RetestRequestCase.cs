namespace ShieldReport.Domain.Entities;

public sealed class RetestRequestCase : BaseEntity
{
    public long RetestRequestId { get; private set; }
    public string CaseText { get; private set; } = string.Empty;
    public bool IsChecked { get; private set; }

    public RetestRequest RetestRequest { get; private set; } = null!;

    private RetestRequestCase()
    {
    }

    public RetestRequestCase(string caseText)
    {
        CaseText = !string.IsNullOrWhiteSpace(caseText)
            ? caseText.Trim()
            : throw new ArgumentException("Case text is required.", nameof(caseText));
    }

    public void SetChecked(bool isChecked)
    {
        IsChecked = isChecked;
    }
}
