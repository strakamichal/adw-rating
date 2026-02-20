using AdwRating.Domain.Entities;
using AdwRating.Domain.Models;

namespace AdwRating.Domain.Interfaces;

public interface ICompetitionRepository
{
    Task<Competition?> GetByIdAsync(int id);
    Task<Competition?> GetBySlugAsync(string slug);
    Task<PagedResult<Competition>> GetListAsync(CompetitionFilter filter);
    Task<Dictionary<int, int>> GetTeamCountsAsync(IEnumerable<int> competitionIds);
    Task<Competition> CreateAsync(Competition competition);
    Task DeleteCascadeAsync(int id);
}
