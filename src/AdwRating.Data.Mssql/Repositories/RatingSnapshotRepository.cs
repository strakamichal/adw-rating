using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class RatingSnapshotRepository : IRatingSnapshotRepository
{
    private readonly AppDbContext _context;

    public RatingSnapshotRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RatingSnapshot>> GetByTeamIdAsync(int teamId)
    {
        return await _context.RatingSnapshots
            .Where(rs => rs.TeamId == teamId)
            .OrderBy(rs => rs.Date)
            .ToListAsync();
    }

    public async Task ReplaceAllAsync(IEnumerable<RatingSnapshot> snapshots)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await _context.RatingSnapshots.ExecuteDeleteAsync();
            _context.RatingSnapshots.AddRange(snapshots);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
