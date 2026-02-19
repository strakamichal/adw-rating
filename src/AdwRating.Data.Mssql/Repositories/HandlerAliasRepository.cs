using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class HandlerAliasRepository : IHandlerAliasRepository
{
    private readonly AppDbContext _context;

    public HandlerAliasRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HandlerAlias?> FindByAliasNameAsync(string normalizedAliasName)
    {
        return await _context.HandlerAliases
            .FirstOrDefaultAsync(a => a.AliasName == normalizedAliasName);
    }

    public async Task<IReadOnlyList<HandlerAlias>> GetByHandlerIdAsync(int handlerId)
    {
        return await _context.HandlerAliases
            .Where(a => a.CanonicalHandlerId == handlerId)
            .ToListAsync();
    }

    public async Task CreateAsync(HandlerAlias alias)
    {
        _context.HandlerAliases.Add(alias);
        await _context.SaveChangesAsync();
    }
}
