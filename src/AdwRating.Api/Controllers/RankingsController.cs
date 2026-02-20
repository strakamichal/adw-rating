using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdwRating.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RankingsController : ControllerBase
{
    private readonly IRankingService _rankingService;
    private readonly ITeamRepository _teamRepo;

    public RankingsController(IRankingService rankingService, ITeamRepository teamRepo)
    {
        _rankingService = rankingService;
        _teamRepo = teamRepo;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TeamRankingDto>>> GetList(
        [FromQuery] SizeCategory? size = null,
        [FromQuery] string? country = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var filter = new RankingFilter(size, country, search, page, pageSize);
        var result = await _rankingService.GetRankingsAsync(filter);

        // Get global ranks for the teams on this page
        var teamIds = result.Items.Select(t => t.Id);
        var globalRanks = await _teamRepo.GetGlobalRanksAsync(size, teamIds);

        // Map Team entities to TeamRankingDto
        var items = result.Items.Select(team =>
        {
            var (rank, prevRank) = globalRanks.GetValueOrDefault(team.Id, (0, null));
            return new TeamRankingDto(
                Id: team.Id,
                Slug: team.Slug,
                HandlerName: team.Handler.Name,
                HandlerCountry: team.Handler.Country,
                DogCallName: team.Dog.CallName,
                SizeCategory: team.Dog.SizeCategory,
                Rating: team.Rating,
                Sigma: team.Sigma,
                Rank: rank,
                PrevRank: prevRank,
                RunCount: team.RunCount,
                Top3RunCount: team.Top3RunCount,
                IsProvisional: team.IsProvisional,
                TierLabel: team.TierLabel
            );
        }).ToList();

        return Ok(new PagedResult<TeamRankingDto>(items, result.TotalCount, result.Page, result.PageSize));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<RankingSummary>> GetSummary()
    {
        var summary = await _rankingService.GetSummaryAsync();
        return Ok(summary);
    }
}
