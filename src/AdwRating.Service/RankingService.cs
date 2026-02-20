using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;

namespace AdwRating.Service;

public class RankingService : IRankingService
{
    private readonly ITeamRepository _teamRepo;
    private readonly ICompetitionRepository _competitionRepo;
    private readonly IRunRepository _runRepo;

    public RankingService(
        ITeamRepository teamRepo,
        ICompetitionRepository competitionRepo,
        IRunRepository runRepo)
    {
        _teamRepo = teamRepo;
        _competitionRepo = competitionRepo;
        _runRepo = runRepo;
    }

    public async Task<PagedResult<Team>> GetRankingsAsync(RankingFilter filter)
    {
        return await _teamRepo.GetRankedTeamsAsync(filter);
    }

    public async Task<RankingSummary> GetSummaryAsync()
    {
        // Count qualified teams (active = meeting min run threshold)
        var allTeams = await _teamRepo.GetAllAsync();
        var qualifiedTeams = allTeams.Count(t => t.IsActive);

        // Count competitions
        var competitionsPage = await _competitionRepo.GetListAsync(new CompetitionFilter(
            Year: null, Tier: null, Country: null, Search: null, Page: 1, PageSize: 1));
        var competitions = competitionsPage.TotalCount;

        // Count runs — use a large window to get all
        var runs = await _runRepo.GetAllInWindowAsync(DateOnly.MinValue);
        var runCount = runs.Count;

        return new RankingSummary(qualifiedTeams, competitions, runCount);
    }
}
