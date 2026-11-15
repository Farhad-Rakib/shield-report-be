using Microsoft.EntityFrameworkCore;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Seeding;

// Starter rows for AI -> Provider List, only when the table is empty (admins own it afterwards).
public static class DefaultLlmProviderCatalogSeeder
{
    public static async Task SeedAsync(DbContext dbContext)
    {
        var items = dbContext.Set<LlmProviderCatalogItem>();
        if (await items.AnyAsync())
        {
            return;
        }

        items.AddRange(
            new LlmProviderCatalogItem("Anthropic", "claude-opus-5-5"),
            new LlmProviderCatalogItem("OpenAI", null),
            new LlmProviderCatalogItem("Google Gemini", "gemini-flash-latest"),
            new LlmProviderCatalogItem("OpenRouter", null));
        await dbContext.SaveChangesAsync();
    }
}
