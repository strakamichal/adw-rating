using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IRunResultRepository
{
    Task<IReadOnlyList<RunResult>> GetByRunIdsAsync(IEnumerable<int> runIds);
    Task CreateBatchAsync(IEnumerable<RunResult> results);
}
