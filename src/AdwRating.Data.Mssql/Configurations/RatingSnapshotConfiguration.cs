using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdwRating.Data.Mssql.Configurations;

public class RatingSnapshotConfiguration : IEntityTypeConfiguration<RatingSnapshot>
{
    public void Configure(EntityTypeBuilder<RatingSnapshot> builder)
    {
        builder.HasKey(rs => rs.Id);

        builder.HasIndex(rs => new { rs.TeamId, rs.RunResultId }).IsUnique();

        builder.HasOne(rs => rs.Team).WithMany().HasForeignKey(rs => rs.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rs => rs.RunResult).WithMany().HasForeignKey(rs => rs.RunResultId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rs => rs.Competition).WithMany().HasForeignKey(rs => rs.CompetitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
