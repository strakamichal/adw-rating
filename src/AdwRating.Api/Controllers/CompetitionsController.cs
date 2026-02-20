using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdwRating.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetitionsController : ControllerBase
{
    private readonly ICompetitionRepository _competitionRepo;
    private readonly IRunRepository _runRepo;

    public CompetitionsController(ICompetitionRepository competitionRepo, IRunRepository runRepo)
    {
        _competitionRepo = competitionRepo;
        _runRepo = runRepo;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<CompetitionDetailDto>>> GetList(
        [FromQuery] int? year = null,
        [FromQuery] int? tier = null,
        [FromQuery] string? country = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var filter = new CompetitionFilter(year, tier, country, search, page, pageSize);
        var result = await _competitionRepo.GetListAsync(filter);

        var competitionIds = result.Items.Select(c => c.Id).ToList();
        var teamCounts = await _competitionRepo.GetTeamCountsAsync(competitionIds);
        var runCounts = await _competitionRepo.GetRunCountsAsync(competitionIds);

        var items = result.Items.Select(c =>
        {
            var (active, excluded) = runCounts.GetValueOrDefault(c.Id, (0, 0));
            return new CompetitionDetailDto(
                Id: c.Id,
                Slug: c.Slug,
                Name: c.Name,
                Date: c.Date,
                EndDate: c.EndDate,
                Country: c.Country,
                Location: c.Location,
                Tier: c.Tier,
                Organization: c.Organization,
                RunCount: active,
                ExcludedRunCount: excluded,
                ParticipantCount: teamCounts.GetValueOrDefault(c.Id, 0)
            );
        }).ToList();

        return Ok(new PagedResult<CompetitionDetailDto>(items, result.TotalCount, result.Page, result.PageSize));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult> GetBySlug(string slug)
    {
        var competition = await _competitionRepo.GetBySlugAsync(slug);
        if (competition is null)
            return NotFound();

        var runs = await _runRepo.GetSummariesByCompetitionIdAsync(competition.Id);
        var teamCounts = await _competitionRepo.GetTeamCountsAsync([competition.Id]);

        return Ok(new
        {
            competition.Id,
            competition.Slug,
            competition.Name,
            competition.Date,
            competition.EndDate,
            competition.Country,
            competition.Location,
            competition.Tier,
            competition.Organization,
            RunCount = runs.Count(r => !r.IsExcluded),
            ExcludedRunCount = runs.Count(r => r.IsExcluded),
            ParticipantCount = teamCounts.GetValueOrDefault(competition.Id, 0),
            Runs = runs
        });
    }
}
