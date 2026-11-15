using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class LlmProviderCatalogItemConfiguration : IEntityTypeConfiguration<LlmProviderCatalogItem>
{
    public void Configure(EntityTypeBuilder<LlmProviderCatalogItem> builder)
    {
        builder.ToTable("llm_providers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(200);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
