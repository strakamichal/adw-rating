using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Interfaces;

public interface IDogRepository
{
    Task<Dog?> GetByIdAsync(int id);
    Task<Dog?> FindByNormalizedNameAndSizeAsync(string normalizedCallName, SizeCategory size);
    Task<IReadOnlyList<Dog>> SearchAsync(string query, int limit);
    Task<Dog> CreateAsync(Dog dog);
    Task UpdateAsync(Dog dog);
    Task MergeAsync(int sourceId, int targetId);
}
