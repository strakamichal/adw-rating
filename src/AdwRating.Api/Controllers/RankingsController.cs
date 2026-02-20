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

    public RankingsController(IRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TeamRankingDto>>> GetList(
        [FromQuery] SizeCategory size = SizeCategory.L,
        [FromQuery] string? country = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var filter = new RankingFilter(size, country, search, page, pageSize);
        var result = await _rankingService.GetRankingsAsync(filter);

        // Map Team entities to TeamRankingDto
        var items = result.Items.Select((team, index) =>
        {
            var rank = (result.Page - 1) * result.PageSize + index + 1;
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
                PrevRank: null, // TODO: compute from PrevRating ordering
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
