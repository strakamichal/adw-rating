using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IRunRepository
{
    Task<IReadOnlyList<Run>> GetByCompetitionIdAsync(int competitionId);
    Task<IReadOnlyList<Run>> GetAllInWindowAsync(DateOnly cutoffDate);
    Task<DateOnly?> GetLatestDateAsync();
    Task CreateBatchAsync(IEnumerable<Run> runs);
}
