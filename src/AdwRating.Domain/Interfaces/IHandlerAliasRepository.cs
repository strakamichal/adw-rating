using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IHandlerAliasRepository
{
    Task<HandlerAlias?> FindByAliasNameAsync(string normalizedAliasName);
    Task<IReadOnlyList<HandlerAlias>> GetByHandlerIdAsync(int handlerId);
    Task CreateAsync(HandlerAlias alias);
}
