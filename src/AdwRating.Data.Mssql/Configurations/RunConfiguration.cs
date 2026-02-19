using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoundKey).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Judge).HasMaxLength(200);
        builder.Property(r => r.OriginalSizeCategory).HasMaxLength(50);
        builder.Property(r => r.SizeCategory).HasConversion<string>().HasMaxLength(1);
        builder.Property(r => r.Discipline).HasConversion<string>().HasMaxLength(10);

        builder.HasIndex(r => new { r.CompetitionId, r.RoundKey }).IsUnique();

        builder.HasMany(r => r.RunResults).WithOne(rr => rr.Run).HasForeignKey(rr => rr.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
