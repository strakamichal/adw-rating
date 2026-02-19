using AdwRating.Domain.Entities;
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

    public async Task<PagedResult<Team>> GetRankedTeamsAsync(RankingFilter filter)
    {
        var query = _context.Teams
            .Include(t => t.Handler)
            .Include(t => t.Dog)
            .Where(t => t.IsActive)
            .Where(t => t.Dog.SizeCategory == filter.Size);

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
