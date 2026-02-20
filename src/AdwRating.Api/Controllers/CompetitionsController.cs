using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdwRating.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetitionsController : ControllerBase
{
    private readonly ICompetitionRepository _competitionRepo;

    public CompetitionsController(ICompetitionRepository competitionRepo)
    {
        _competitionRepo = competitionRepo;
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

        var competitionIds = result.Items.Select(c => c.Id);
        var teamCounts = await _competitionRepo.GetTeamCountsAsync(competitionIds);

        var items = result.Items.Select(c => new CompetitionDetailDto(
            Id: c.Id,
            Slug: c.Slug,
            Name: c.Name,
            Date: c.Date,
            EndDate: c.EndDate,
            Country: c.Country,
            Location: c.Location,
            Tier: c.Tier,
            Organization: c.Organization,
            RunCount: 0,
            ParticipantCount: teamCounts.GetValueOrDefault(c.Id, 0)
        )).ToList();

        return Ok(new PagedResult<CompetitionDetailDto>(items, result.TotalCount, result.Page, result.PageSize));
    }
}
