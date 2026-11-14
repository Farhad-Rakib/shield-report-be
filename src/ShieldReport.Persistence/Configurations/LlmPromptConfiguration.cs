using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class LlmPromptConfiguration : IEntityTypeConfiguration<LlmPrompt>
{
    public void Configure(EntityTypeBuilder<LlmPrompt> builder)
    {
        builder.ToTable("llm_prompts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Template).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
    }
}
