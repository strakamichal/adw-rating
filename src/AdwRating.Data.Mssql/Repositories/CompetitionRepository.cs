using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class CompetitionRepository : ICompetitionRepository
{
    private readonly AppDbContext _context;

    public CompetitionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Competition?> GetByIdAsync(int id)
    {
        return await _context.Competitions.FindAsync(id);
    }

    public async Task<Competition?> GetBySlugAsync(string slug)
    {
        return await _context.Competitions
            .FirstOrDefaultAsync(c => c.Slug == slug);
    }

    public async Task<PagedResult<Competition>> GetListAsync(CompetitionFilter filter)
    {
        var query = _context.Competitions.AsQueryable();

        if (filter.Year.HasValue)
            query = query.Where(c => c.Date.Year == filter.Year.Value);

        if (filter.Tier.HasValue)
            query = query.Where(c => c.Tier == filter.Tier.Value);

        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(c => c.Country == filter.Country);

        if (!string.IsNullOrWhiteSpace(filter.Search))
            query = query.Where(c => c.Name.Contains(filter.Search));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(c => c.Date)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<Competition>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<Dictionary<int, int>> GetTeamCountsAsync(IEnumerable<int> competitionIds)
    {
        var ids = competitionIds.ToList();
        return await _context.Runs
            .Where(r => ids.Contains(r.CompetitionId))
            .SelectMany(r => r.RunResults.Select(rr => new { r.CompetitionId, rr.TeamId }))
            .Distinct()
            .GroupBy(x => x.CompetitionId)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<int, (int Active, int Excluded)>> GetRunCountsAsync(IEnumerable<int> competitionIds)
    {
        var ids = competitionIds.ToList();
        var counts = await _context.Runs
            .Where(r => ids.Contains(r.CompetitionId))
            .GroupBy(r => r.CompetitionId)
            .Select(g => new
            {
                CompetitionId = g.Key,
                Active = g.Count(r => !r.IsExcluded),
                Excluded = g.Count(r => r.IsExcluded)
            })
            .ToDictionaryAsync(x => x.CompetitionId, x => (x.Active, x.Excluded));

        return counts;
    }

    public async Task<Competition> CreateAsync(Competition competition)
    {
        _context.Competitions.Add(competition);
        await _context.SaveChangesAsync();
        return competition;
    }

    public async Task DeleteCascadeAsync(int id)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Delete RunResults for all runs of this competition
            var runIds = await _context.Runs
                .Where(r => r.CompetitionId == id)
                .Select(r => r.Id)
                .ToListAsync();

            if (runIds.Count > 0)
            {
                await _context.RunResults
                    .Where(rr => runIds.Contains(rr.RunId))
                    .ExecuteDeleteAsync();

                await _context.Runs
                    .Where(r => r.CompetitionId == id)
                    .ExecuteDeleteAsync();
            }

            await _context.Competitions
                .Where(c => c.Id == id)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
