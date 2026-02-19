using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Slug).HasMaxLength(300).IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.HasIndex(t => new { t.HandlerId, t.DogId }).IsUnique();

        builder.Property(t => t.TierLabel).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.Handler).WithMany(h => h.Teams).HasForeignKey(t => t.HandlerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Dog).WithMany(d => d.Teams).HasForeignKey(t => t.DogId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.RunResults).WithOne(rr => rr.Team).HasForeignKey(rr => rr.TeamId);
    }
}
