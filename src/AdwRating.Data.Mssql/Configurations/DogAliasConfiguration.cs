using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class DogAliasConfiguration : IEntityTypeConfiguration<DogAlias>
{
    public void Configure(EntityTypeBuilder<DogAlias> builder)
    {
        builder.HasKey(da => da.Id);

        builder.Property(da => da.AliasName).HasMaxLength(300).IsRequired();
        builder.Property(da => da.AliasType).HasConversion<string>().HasMaxLength(20);
        builder.Property(da => da.Source).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(da => new { da.AliasName, da.AliasType }).IsUnique();

        builder.HasOne(da => da.CanonicalDog).WithMany().HasForeignKey(da => da.CanonicalDogId).OnDelete(DeleteBehavior.Restrict);
    }
}
