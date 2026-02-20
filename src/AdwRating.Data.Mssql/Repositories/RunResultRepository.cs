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

    public async Task CreateBatchAsync(IEnumerable<RunResult> results)
    {
        _context.RunResults.AddRange(results);
        await _context.SaveChangesAsync();
    }
}
