using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IRunResultRepository
{
    Task<IReadOnlyList<RunResult>> GetByRunIdsAsync(IEnumerable<int> runIds);
    /// <summary>
    /// Returns run results for a team, optionally filtered to runs on or after the given date.
    /// Results are returned ordered by Run.Date descending, then Run.RunNumber.
    /// Includes Run and Run.Competition navigation properties.
    /// </summary>
    Task<IReadOnlyList<RunResult>> GetByTeamIdAsync(int teamId, DateOnly? after = null);
    Task CreateBatchAsync(IEnumerable<RunResult> results);
}
