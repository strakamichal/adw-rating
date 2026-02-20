using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IRatingSnapshotRepository
{
    Task<IReadOnlyList<RatingSnapshot>> GetByTeamIdAsync(int teamId);
    Task ReplaceAllAsync(IEnumerable<RatingSnapshot> snapshots);
}
