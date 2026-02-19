using AdwRating.Domain.Models;

namespace AdwRating.Domain.Interfaces;

public interface ITeamProfileService
{
    Task<TeamDetailDto?> GetBySlugAsync(string slug);
    Task<PagedResult<TeamResultDto>> GetResultsAsync(string teamSlug, int page = 1, int pageSize = 20);
}
