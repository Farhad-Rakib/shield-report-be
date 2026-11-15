using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;
using ShieldReport.Domain.Enums;

namespace ShieldReport.Persistence.Configurations;

public sealed class ReportTemplateConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> builder)
    {
        builder.ToTable("report_templates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CompanyDisplayName).HasMaxLength(300);
        builder.Property(x => x.DocxTemplatePath).HasMaxLength(500);
        builder.Property(x => x.DocxTemplateFileName).HasMaxLength(300);
        builder.Property(x => x.BuiltInTestingType).HasConversion<byte?>();
        builder.Property(x => x.Kind).HasConversion<byte>().HasDefaultValue(ReportTemplateKind.Engagement);
    }
}
