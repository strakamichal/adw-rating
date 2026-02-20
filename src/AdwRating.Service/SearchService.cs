using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;

namespace AdwRating.Service;

public class SearchService : ISearchService
{
    private readonly IHandlerRepository _handlerRepo;
    private readonly IDogRepository _dogRepo;
    private readonly ICompetitionRepository _competitionRepo;
    private readonly ITeamRepository _teamRepo;

    public SearchService(
        IHandlerRepository handlerRepo,
        IDogRepository dogRepo,
        ICompetitionRepository competitionRepo,
        ITeamRepository teamRepo)
    {
        _handlerRepo = handlerRepo;
        _dogRepo = dogRepo;
        _competitionRepo = competitionRepo;
        _teamRepo = teamRepo;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int limit = 10)
    {
        var results = new List<SearchResult>();

        // Search handlers
        var handlers = await _handlerRepo.SearchAsync(query, limit);
        foreach (var h in handlers)
        {
            results.Add(new SearchResult(
                Type: "handler",
                Slug: h.Slug,
                DisplayName: h.Name,
                Subtitle: h.Country
            ));
        }

        // Search dogs → map to teams for meaningful results
        var dogs = await _dogRepo.SearchAsync(query, limit);
        foreach (var dog in dogs)
        {
            var teams = await _teamRepo.GetByDogIdAsync(dog.Id);
            foreach (var team in teams)
            {
                results.Add(new SearchResult(
                    Type: "team",
                    Slug: team.Slug,
                    DisplayName: $"{team.Handler.Name} & {dog.CallName}",
                    Subtitle: $"{team.Rating:F0}"
                ));
            }
        }

        // Search competitions
        var competitions = await _competitionRepo.GetListAsync(new CompetitionFilter(
            Year: null, Tier: null, Country: null, Search: query, Page: 1, PageSize: limit));
        foreach (var c in competitions.Items)
        {
            results.Add(new SearchResult(
                Type: "competition",
                Slug: c.Slug,
                DisplayName: c.Name,
                Subtitle: c.Date.ToString("yyyy-MM-dd")
            ));
        }

        return results.Take(limit).ToList();
    }
}
