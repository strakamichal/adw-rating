using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IImportLogRepository
{
    Task CreateAsync(ImportLog log);
    Task<IReadOnlyList<ImportLog>> GetRecentAsync(int count);
}
