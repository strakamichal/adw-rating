using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;

namespace AdwRating.Service;

public class TeamProfileService : ITeamProfileService
{
    private readonly ITeamRepository _teamRepo;
    private readonly IRunResultRepository _runResultRepo;

    public TeamProfileService(
        ITeamRepository teamRepo,
        IRunResultRepository runResultRepo)
    {
        _teamRepo = teamRepo;
        _runResultRepo = runResultRepo;
    }

    public async Task<TeamDetailDto?> GetBySlugAsync(string slug)
    {
        var team = await _teamRepo.GetBySlugAsync(slug);
        if (team is null)
            return null;

        // Compute stats from run results
        var results = await _runResultRepo.GetByTeamIdAsync(team.Id);

        var finishedPct = team.RunCount > 0
            ? (float)team.FinishedRunCount / team.RunCount * 100f
            : 0f;

        var top3Pct = team.RunCount > 0
            ? (float)team.Top3RunCount / team.RunCount * 100f
            : 0f;

        float? avgRank = null;
        var rankedResults = results.Where(r => !r.Eliminated && r.Rank.HasValue).ToList();
        if (rankedResults.Count > 0)
            avgRank = (float)rankedResults.Average(r => r.Rank!.Value);

        return new TeamDetailDto(
            Id: team.Id,
            Slug: team.Slug,
            HandlerName: team.Handler.Name,
            HandlerSlug: team.Handler.Slug,
            HandlerCountry: team.Handler.Country,
            DogCallName: team.Dog.CallName,
            DogRegisteredName: team.Dog.RegisteredName,
            DogBreed: team.Dog.Breed,
            SizeCategory: team.Dog.SizeCategory,
            Rating: team.Rating,
            Sigma: team.Sigma,
            PrevRating: team.PrevRating,
            PeakRating: team.PeakRating,
            RunCount: team.RunCount,
            FinishedRunCount: team.FinishedRunCount,
            Top3RunCount: team.Top3RunCount,
            IsActive: team.IsActive,
            IsProvisional: team.IsProvisional,
            TierLabel: team.TierLabel,
            FinishedPct: finishedPct,
            Top3Pct: top3Pct,
            AvgRank: avgRank
        );
    }

    public async Task<PagedResult<TeamResultDto>> GetResultsAsync(string teamSlug, int page = 1, int pageSize = 20)
    {
        var team = await _teamRepo.GetBySlugAsync(teamSlug);
        if (team is null)
            return new PagedResult<TeamResultDto>([], 0, page, pageSize);

        var allResults = await _runResultRepo.GetByTeamIdAsync(team.Id);

        var totalCount = allResults.Count;
        var pagedResults = allResults
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(rr => new TeamResultDto(
                CompetitionSlug: rr.Run.Competition.Slug,
                CompetitionName: rr.Run.Competition.Name,
                Date: rr.Run.Date,
                SizeCategory: rr.Run.SizeCategory,
                Discipline: rr.Run.Discipline,
                IsTeamRound: rr.Run.IsTeamRound,
                Rank: rr.Rank,
                Faults: rr.Faults,
                TimeFaults: rr.TimeFaults,
                Time: rr.Time,
                Speed: rr.Speed,
                Eliminated: rr.Eliminated,
                IsExcluded: rr.Run.IsExcluded
            ))
            .ToList();

        return new PagedResult<TeamResultDto>(pagedResults, totalCount, page, pageSize);
    }
}
