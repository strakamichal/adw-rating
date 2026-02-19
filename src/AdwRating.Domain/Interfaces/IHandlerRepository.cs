using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IHandlerRepository
{
    Task<Handler?> GetByIdAsync(int id);
    Task<Handler?> GetBySlugAsync(string slug);
    Task<Handler?> FindByNormalizedNameAndCountryAsync(string normalizedName, string country);
    Task<IReadOnlyList<Handler>> SearchAsync(string query, int limit);
    Task<Handler> CreateAsync(Handler handler);
    Task UpdateAsync(Handler handler);
    Task MergeAsync(int sourceId, int targetId);
}
