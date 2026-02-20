using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class RunRepository : IRunRepository
{
    private readonly AppDbContext _context;

    public RunRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Run>> GetByCompetitionIdAsync(int competitionId)
    {
        return await _context.Runs
            .Where(r => r.CompetitionId == competitionId)
            .OrderBy(r => r.RunNumber)
            .ToListAsync();
    }

    public async Task<Run?> GetByCompetitionAndRoundKeyAsync(int competitionId, string roundKey)
    {
        return await _context.Runs
            .FirstOrDefaultAsync(r => r.CompetitionId == competitionId && r.RoundKey == roundKey);
    }

    public async Task<DateOnly?> GetLatestDateAsync()
    {
        if (!await _context.Runs.AnyAsync())
            return null;

        return await _context.Runs.MaxAsync(r => r.Date);
    }

    public async Task<IReadOnlyList<Run>> GetAllInWindowAsync(DateOnly cutoffDate)
    {
        return await _context.Runs
            .Include(r => r.Competition)
            .Where(r => r.Date >= cutoffDate)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    public async Task CreateBatchAsync(IEnumerable<Run> runs)
    {
        _context.Runs.AddRange(runs);
        await _context.SaveChangesAsync();
    }
}
