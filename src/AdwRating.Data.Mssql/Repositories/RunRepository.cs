using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
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

    public async Task<IReadOnlyList<RunSummaryDto>> GetSummariesByCompetitionIdAsync(int competitionId)
    {
        return await _context.Runs
            .Where(r => r.CompetitionId == competitionId)
            .OrderBy(r => r.Date).ThenBy(r => r.RoundKey)
            .Select(r => new RunSummaryDto(
                r.Id,
                r.RoundKey,
                r.Date,
                r.SizeCategory.ToString(),
                r.Discipline.ToString(),
                r.IsTeamRound,
                r.IsExcluded,
                r.RunResults.Count,
                r.RunResults.Count(rr => !rr.Eliminated),
                r.RunResults.Count(rr => rr.Eliminated)
            ))
            .ToListAsync();
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
            .Where(r => r.Date >= cutoffDate && !r.IsExcluded)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    public async Task CreateBatchAsync(IEnumerable<Run> runs)
    {
        _context.Runs.AddRange(runs);
        await _context.SaveChangesAsync();
    }

    public async Task SetExcludedAsync(IEnumerable<int> runIds, bool excluded)
    {
        var ids = runIds.ToHashSet();
        var runs = await _context.Runs
            .Where(r => ids.Contains(r.Id))
            .ToListAsync();

        foreach (var run in runs)
            run.IsExcluded = excluded;

        await _context.SaveChangesAsync();
    }
}
