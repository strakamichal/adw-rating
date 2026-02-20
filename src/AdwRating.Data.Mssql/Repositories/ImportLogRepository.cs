using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class ImportLogRepository : IImportLogRepository
{
    private readonly AppDbContext _context;

    public ImportLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(ImportLog log)
    {
        _context.ImportLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ImportLog>> GetRecentAsync(int count)
    {
        return await _context.ImportLogs
            .Include(l => l.Competition)
            .OrderByDescending(l => l.ImportedAt)
            .Take(count)
            .ToListAsync();
    }
}
