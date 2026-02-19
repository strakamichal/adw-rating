using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IRunResultRepository
{
    Task<IReadOnlyList<RunResult>> GetByRunIdAsync(int runId);
    Task<IReadOnlyList<RunResult>> GetByRunIdsAsync(IEnumerable<int> runIds);
    Task<IReadOnlyList<RunResult>> GetByTeamIdAsync(int teamId, DateOnly? after = null);
    Task CreateBatchAsync(IEnumerable<RunResult> results);
}
