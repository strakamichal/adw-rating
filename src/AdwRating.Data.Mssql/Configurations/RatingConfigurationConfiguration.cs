using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class RatingConfigurationConfiguration : IEntityTypeConfiguration<RatingConfiguration>
{
    public void Configure(EntityTypeBuilder<RatingConfiguration> builder)
    {
        builder.HasKey(rc => rc.Id);

        builder.HasIndex(rc => rc.IsActive);
    }
}
