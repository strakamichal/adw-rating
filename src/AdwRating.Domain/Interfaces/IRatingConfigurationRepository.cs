using AdwRating.Domain.Entities;

namespace AdwRating.Domain.Interfaces;

public interface IRatingConfigurationRepository
{
    Task<RatingConfiguration> GetActiveAsync();
    Task CreateAsync(RatingConfiguration config);
}
