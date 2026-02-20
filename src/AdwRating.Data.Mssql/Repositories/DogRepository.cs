using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class DogRepository : IDogRepository
{
    private readonly AppDbContext _context;

    public DogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Dog?> GetByIdAsync(int id)
    {
        return await _context.Dogs.FindAsync(id);
    }

    public async Task<IReadOnlyList<Dog>> FindAllByNormalizedNameAndSizeAsync(string normalizedCallName, SizeCategory size)
    {
        return await _context.Dogs
            .Where(d => d.NormalizedCallName == normalizedCallName && d.SizeCategory == size)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Dog>> SearchAsync(string query, int limit)
    {
        var normalizedQuery = query.ToLower();
        return await _context.Dogs
            .Where(d => d.NormalizedCallName.Contains(normalizedQuery))
            .OrderBy(d => d.NormalizedCallName)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dog> CreateAsync(Dog dog)
    {
        _context.Dogs.Add(dog);
        await _context.SaveChangesAsync();
        return dog;
    }

    public async Task UpdateAsync(Dog dog)
    {
        _context.Dogs.Update(dog);
        await _context.SaveChangesAsync();
    }

    public async Task MergeAsync(int sourceId, int targetId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Reassign all teams from source dog to target dog
        await _context.Teams
            .Where(t => t.DogId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DogId, targetId));

        // Reassign all dog aliases from source to target
        await _context.DogAliases
            .Where(a => a.CanonicalDogId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CanonicalDogId, targetId));

        // Delete the source dog
        await _context.Dogs
            .Where(d => d.Id == sourceId)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }
}
