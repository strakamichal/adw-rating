using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _context;

    public TeamRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Team?> GetByIdAsync(int id)
    {
        return await _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Team?> GetBySlugAsync(string slug)
    {
        return await _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .FirstOrDefaultAsync(t => t.Slug == slug);
    }

    public async Task<Team?> GetByHandlerAndDogAsync(int handlerId, int dogId)
    {
        return await _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .FirstOrDefaultAsync(t => t.HandlerId == handlerId && t.DogId == dogId);
    }

    public async Task<IReadOnlyList<Team>> GetByHandlerIdAsync(int handlerId)
    {
        return await _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .Where(t => t.HandlerId == handlerId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Team>> GetByDogIdAsync(int dogId)
    {
        return await _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .Where(t => t.DogId == dogId)
            .ToListAsync();
    }

    public async Task<PagedResult<Team>> GetRankedTeamsAsync(RankingFilter filter)
    {
        var query = _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .Where(t => t.IsActive);

        if (filter.Size.HasValue)
            query = query.Where(t => t.Dog.SizeCategory == filter.Size.Value);

        if (!string.IsNullOrEmpty(filter.Country))
        {
            query = query.Where(t => t.Handler.Country == filter.Country);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(t =>
                t.Handler.NormalizedName.Contains(search) ||
                t.Dog.NormalizedCallName.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.Rating)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<Team>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<Dictionary<int, (int Rank, int? PrevRank)>> GetGlobalRanksAsync(
        SizeCategory? size, IEnumerable<int> teamIds)
    {
        var ids = teamIds.ToList();

        if (size == null)
        {
            // When no size filter, compute ranks per-size for each team
            var teamsWithSize = await _context.Teams
                .Include(t => t.Dog)
                .Where(t => ids.Contains(t.Id))
                .Select(t => new { t.Id, t.Dog.SizeCategory })
                .ToListAsync();

            var result = new Dictionary<int, (int Rank, int? PrevRank)>();
            var sizeGroups = teamsWithSize.GroupBy(t => t.SizeCategory);

            foreach (var group in sizeGroups)
            {
                var sizeRanks = await GetGlobalRanksAsync(group.Key, group.Select(t => t.Id));
                foreach (var kvp in sizeRanks)
                    result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        // Get current global ranks: all active teams in this size, ordered by rating desc
        var allRanked = await _context.Teams
            .Include(t => t.Dog)
            .Where(t => t.IsActive && t.Dog.SizeCategory == size.Value)
            .OrderByDescending(t => t.Rating)
            .Select(t => t.Id)
            .ToListAsync();

        var currentRanks = new Dictionary<int, int>();
        for (int i = 0; i < allRanked.Count; i++)
            currentRanks[allRanked[i]] = i + 1;

        // Get previous global ranks: order by PrevRating desc
        var allPrevRanked = await _context.Teams
            .Include(t => t.Dog)
            .Where(t => t.IsActive && t.Dog.SizeCategory == size.Value && t.PrevRating > 0)
            .OrderByDescending(t => t.PrevRating)
            .Select(t => t.Id)
            .ToListAsync();

        var prevRanks = new Dictionary<int, int>();
        for (int i = 0; i < allPrevRanked.Count; i++)
            prevRanks[allPrevRanked[i]] = i + 1;

        {
            var result = new Dictionary<int, (int Rank, int? PrevRank)>();
            foreach (var id in ids)
            {
                var rank = currentRanks.GetValueOrDefault(id, 0);
                int? prevRank = prevRanks.TryGetValue(id, out var pr) ? pr : null;
                result[id] = (rank, prevRank);
            }

            return result;
        }
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync()
    {
        return await _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .ToListAsync();
    }

    public async Task<Team> CreateAsync(Team team)
    {
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    public async Task UpdateBatchAsync(IEnumerable<Team> teams)
    {
        _context.Teams.UpdateRange(teams);
        await _context.SaveChangesAsync();
    }
}
