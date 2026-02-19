using AdwRating.Domain.Entities;
using AdwRating.Domain.Models;

namespace AdwRating.Domain.Interfaces;

public interface IRankingService
{
    Task<PagedResult<Team>> GetRankingsAsync(RankingFilter filter);
    Task<RankingSummary> GetSummaryAsync();
}
