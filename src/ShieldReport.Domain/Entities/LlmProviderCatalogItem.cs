namespace ShieldReport.Domain.Entities;

// AI -> Provider List: a simple lookup of LLM providers (name + optional default model) that the
// Providers & Models form offers as a starting point. It does not change how calls are made:
// each LlmProviderConfiguration still names its engine (LlmProvider enum), key and base URL.
public sealed class LlmProviderCatalogItem : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Model { get; private set; }

    private LlmProviderCatalogItem()
    {
    }

    public LlmProviderCatalogItem(string name, string? model)
    {
        Update(name, model);
    }

    public void Update(string name, string? model)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : throw new ArgumentException("Name is required.", nameof(name));
        Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
    }
}
