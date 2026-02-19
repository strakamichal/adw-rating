using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class HandlerAliasConfiguration : IEntityTypeConfiguration<HandlerAlias>
{
    public void Configure(EntityTypeBuilder<HandlerAlias> builder)
    {
        builder.HasKey(ha => ha.Id);

        builder.Property(ha => ha.AliasName).HasMaxLength(200).IsRequired();
        builder.Property(ha => ha.Source).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(ha => ha.AliasName).IsUnique();

        builder.HasOne(ha => ha.CanonicalHandler).WithMany().HasForeignKey(ha => ha.CanonicalHandlerId).OnDelete(DeleteBehavior.Restrict);
    }
}
