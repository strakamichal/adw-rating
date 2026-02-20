using AdwRating.Domain.Entities;
using AdwRating.Domain.Models;

namespace AdwRating.Domain.Interfaces;

public interface IRunRepository
{
    Task<IReadOnlyList<Run>> GetByCompetitionIdAsync(int competitionId);
    Task<IReadOnlyList<RunSummaryDto>> GetSummariesByCompetitionIdAsync(int competitionId);
    Task<IReadOnlyList<Run>> GetAllInWindowAsync(DateOnly cutoffDate);
    Task<DateOnly?> GetLatestDateAsync();
    Task CreateBatchAsync(IEnumerable<Run> runs);
    Task SetExcludedAsync(IEnumerable<int> runIds, bool excluded);
}
