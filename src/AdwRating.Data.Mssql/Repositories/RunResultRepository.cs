using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class RunResultRepository : IRunResultRepository
{
    private readonly AppDbContext _context;

    public RunResultRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RunResult>> GetByRunIdsAsync(IEnumerable<int> runIds)
    {
        var idList = runIds.ToList();
        return await _context.RunResults
            .Include(rr => rr.Team)
            .Where(rr => idList.Contains(rr.RunId))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RunResult>> GetByTeamIdAsync(int teamId, DateOnly? after = null)
    {
        var query = _context.RunResults
            .Include(rr => rr.Run)
                .ThenInclude(r => r.Competition)
            .Where(rr => rr.TeamId == teamId);

        if (after.HasValue)
            query = query.Where(rr => rr.Run.Date >= after.Value);

        return await query
            .OrderByDescending(rr => rr.Run.Date)
            .ThenBy(rr => rr.Run.RunNumber)
            .ToListAsync();
    }

    public async Task CreateBatchAsync(IEnumerable<RunResult> results)
    {
        _context.RunResults.AddRange(results);
        await _context.SaveChangesAsync();
    }
}
