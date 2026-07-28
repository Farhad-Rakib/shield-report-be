namespace ShieldReport.Application.Common.Llm;

// Shared by every Application-layer service that sends an LLM a strict-JSON-only prompt contract
// — tolerates the model still wrapping its reply in a ```json fence despite being told not to,
// since that's a common enough deviation to be worth a cheap defensive strip rather than a
// failed parse on an otherwise-usable response.
public static class LlmJsonResponseHelper
{
    public static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        var withoutOpeningFence = firstNewline >= 0 ? trimmed[(firstNewline + 1)..] : trimmed;
        var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence;
    }
}
