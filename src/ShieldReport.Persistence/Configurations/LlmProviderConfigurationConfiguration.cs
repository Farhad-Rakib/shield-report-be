using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class LlmProviderConfigurationConfiguration : IEntityTypeConfiguration<LlmProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<LlmProviderConfiguration> builder)
    {
        builder.ToTable("llm_provider_configurations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).IsRequired().HasConversion<byte>();
        builder.Property(x => x.ApiKeyEncrypted).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(500);
        builder.Property(x => x.Temperature).HasColumnType("decimal(3,2)");
        builder.Property(x => x.PromptTemplate).IsRequired();

        builder.HasIndex(x => x.Provider).IsUnique();
    }
}
