using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class DogAliasRepository : IDogAliasRepository
{
    private readonly AppDbContext _context;

    public DogAliasRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DogAlias?> FindByAliasNameAndTypeAsync(string normalizedAliasName, DogAliasType type)
    {
        return await _context.DogAliases
            .FirstOrDefaultAsync(a => a.AliasName == normalizedAliasName && a.AliasType == type);
    }

    public async Task<IReadOnlyList<DogAlias>> GetByDogIdAsync(int dogId)
    {
        return await _context.DogAliases
            .Where(a => a.CanonicalDogId == dogId)
            .ToListAsync();
    }

    public async Task CreateAsync(DogAlias alias)
    {
        _context.DogAliases.Add(alias);
        await _context.SaveChangesAsync();
    }
}
