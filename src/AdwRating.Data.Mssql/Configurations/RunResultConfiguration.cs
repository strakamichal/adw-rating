using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class RunResultConfiguration : IEntityTypeConfiguration<RunResult>
{
    public void Configure(EntityTypeBuilder<RunResult> builder)
    {
        builder.HasKey(rr => rr.Id);

        builder.HasIndex(rr => new { rr.RunId, rr.TeamId }).IsUnique();
    }
}
