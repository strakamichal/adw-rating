using AdwRating.Domain.Models;

namespace AdwRating.Domain.Interfaces;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int limit = 10);
}
