using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Interfaces;

public interface IDogAliasRepository
{
    Task<DogAlias?> FindByAliasNameAndTypeAsync(string normalizedAliasName, DogAliasType type);
    Task<IReadOnlyList<DogAlias>> GetByDogIdAsync(int dogId);
    Task CreateAsync(DogAlias alias);
}
