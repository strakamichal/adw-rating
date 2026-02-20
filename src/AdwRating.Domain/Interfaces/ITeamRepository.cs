using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;

namespace AdwRating.Domain.Interfaces;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(int id);
    Task<Team?> GetBySlugAsync(string slug);
    Task<Team?> GetByHandlerAndDogAsync(int handlerId, int dogId);
    Task<IReadOnlyList<Team>> GetByHandlerIdAsync(int handlerId);
    Task<IReadOnlyList<Team>> GetByDogIdAsync(int dogId);
    Task<PagedResult<Team>> GetRankedTeamsAsync(RankingFilter filter);
    Task<Dictionary<int, (int Rank, int? PrevRank)>> GetGlobalRanksAsync(SizeCategory? size, IEnumerable<int> teamIds);
    Task<IReadOnlyList<Team>> GetAllAsync();
    Task<Team> CreateAsync(Team team);
    Task UpdateBatchAsync(IEnumerable<Team> teams);
}
