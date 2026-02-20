using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class HandlerConfiguration : IEntityTypeConfiguration<Handler>
{
    public void Configure(EntityTypeBuilder<Handler> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name).HasMaxLength(200).IsRequired();
        builder.Property(h => h.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(h => h.Country).HasMaxLength(3).IsRequired();
        builder.Property(h => h.Slug).HasMaxLength(200).IsRequired();

        builder.HasIndex(h => h.Slug).IsUnique();
        builder.HasIndex(h => new { h.NormalizedName, h.Country }).IsUnique();

        builder.HasMany(h => h.Teams).WithOne(t => t.Handler).HasForeignKey(t => t.HandlerId);
    }
}
