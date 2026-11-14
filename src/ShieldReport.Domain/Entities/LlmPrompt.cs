namespace ShieldReport.Domain.Entities;

// An admin override of one of the built-in LLM prompts (Prompt Library). Absent row = the
// built-in default for that key (see LlmPromptCatalog in Application).
public sealed class LlmPrompt : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Template { get; private set; } = string.Empty;
    public long? UpdatedByUserId { get; private set; }

    private LlmPrompt()
    {
    }

    public LlmPrompt(string key, string template, long? userId)
    {
        Key = key;
        Template = template;
        UpdatedByUserId = userId;
    }

    public void SetTemplate(string template, long? userId)
    {
        Template = template;
        UpdatedByUserId = userId;
    }
}
