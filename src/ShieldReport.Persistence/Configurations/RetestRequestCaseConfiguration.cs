using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShieldReport.Domain.Entities;

namespace ShieldReport.Persistence.Configurations;

public sealed class RetestRequestCaseConfiguration : IEntityTypeConfiguration<RetestRequestCase>
{
    public void Configure(EntityTypeBuilder<RetestRequestCase> builder)
    {
        builder.ToTable("retest_request_cases");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.CaseText).IsRequired().HasMaxLength(500);
        builder.Property(x => x.IsChecked).HasDefaultValue(false);

        builder.HasIndex(x => x.RetestRequestId);
    }
}
