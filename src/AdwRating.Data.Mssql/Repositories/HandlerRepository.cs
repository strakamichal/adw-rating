using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class HandlerRepository : IHandlerRepository
{
    private readonly AppDbContext _context;

    public HandlerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Handler?> GetByIdAsync(int id)
    {
        return await _context.Handlers.FindAsync(id);
    }

    public async Task<Handler?> GetBySlugAsync(string slug)
    {
        return await _context.Handlers
            .FirstOrDefaultAsync(h => h.Slug == slug);
    }

    public async Task<Handler?> FindByNormalizedNameAndCountryAsync(string normalizedName, string country)
    {
        return await _context.Handlers
            .FirstOrDefaultAsync(h => h.NormalizedName == normalizedName && h.Country == country);
    }

    public async Task<IReadOnlyList<Handler>> SearchAsync(string query, int limit)
    {
        var normalizedQuery = query.ToLower();
        return await _context.Handlers
            .Where(h => h.NormalizedName.Contains(normalizedQuery))
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Handler> CreateAsync(Handler handler)
    {
        _context.Handlers.Add(handler);
        await _context.SaveChangesAsync();
        return handler;
    }

    public async Task UpdateAsync(Handler handler)
    {
        _context.Handlers.Update(handler);
        await _context.SaveChangesAsync();
    }

    public async Task MergeAsync(int sourceId, int targetId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Reassign all teams from source handler to target handler
        await _context.Teams
            .Where(t => t.HandlerId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.HandlerId, targetId));

        // Reassign all handler aliases from source to target
        await _context.HandlerAliases
            .Where(a => a.CanonicalHandlerId == sourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CanonicalHandlerId, targetId));

        // Delete the source handler
        await _context.Handlers
            .Where(h => h.Id == sourceId)
            .ExecuteDeleteAsync();

        await transaction.CommitAsync();
    }
}
