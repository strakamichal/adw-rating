using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql.Repositories;

public class RatingConfigurationRepository : IRatingConfigurationRepository
{
    private readonly AppDbContext _context;

    public RatingConfigurationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RatingConfiguration> GetActiveAsync()
    {
        var config = await _context.RatingConfigurations
            .FirstOrDefaultAsync(c => c.IsActive);

        return config ?? throw new InvalidOperationException(
            "No active rating configuration found.");
    }

    public async Task CreateAsync(RatingConfiguration config)
    {
        // Deactivate all existing configurations
        await _context.RatingConfigurations
            .Where(c => c.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsActive, false));

        config.IsActive = true;
        _context.RatingConfigurations.Add(config);
        await _context.SaveChangesAsync();
    }
}
