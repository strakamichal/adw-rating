using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class ImportLogConfiguration : IEntityTypeConfiguration<ImportLog>
{
    public void Configure(EntityTypeBuilder<ImportLog> builder)
    {
        builder.HasKey(il => il.Id);

        builder.Property(il => il.FileName).HasMaxLength(500).IsRequired();
        builder.Property(il => il.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(il => il.Competition).WithMany().HasForeignKey(il => il.CompetitionId).OnDelete(DeleteBehavior.SetNull);
    }
}
