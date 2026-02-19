using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Country).HasMaxLength(3);
        builder.Property(c => c.Location).HasMaxLength(200);
        builder.Property(c => c.Organization).HasMaxLength(50);

        builder.HasMany(c => c.Runs).WithOne(r => r.Competition).HasForeignKey(r => r.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
