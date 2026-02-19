using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class DogConfiguration : IEntityTypeConfiguration<Dog>
{
    public void Configure(EntityTypeBuilder<Dog> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.CallName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.NormalizedCallName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.RegisteredName).HasMaxLength(300);
        builder.Property(d => d.Breed).HasMaxLength(100);
        builder.Property(d => d.SizeCategory).HasConversion<string>().HasMaxLength(1);
        builder.Property(d => d.SizeCategoryOverride).HasConversion<string>().HasMaxLength(1);

        builder.HasMany(d => d.Teams).WithOne(t => t.Dog).HasForeignKey(t => t.DogId);
    }
}
