using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdwRating.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ITeamProfileService _teamProfileService;
    private readonly ITeamRepository _teamRepo;
    private readonly IRatingSnapshotRepository _snapshotRepo;

    public TeamsController(
        ITeamProfileService teamProfileService,
        ITeamRepository teamRepo,
        IRatingSnapshotRepository snapshotRepo)
    {
        _teamProfileService = teamProfileService;
        _teamRepo = teamRepo;
        _snapshotRepo = snapshotRepo;
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<TeamDetailDto>> GetBySlug(string slug)
    {
        var team = await _teamProfileService.GetBySlugAsync(slug);
        if (team is null)
            return NotFound();

        return Ok(team);
    }

    [HttpGet("{slug}/history")]
    public async Task<IActionResult> GetHistory(string slug)
    {
        var team = await _teamRepo.GetBySlugAsync(slug);
        if (team is null)
            return NotFound();

        var snapshots = await _snapshotRepo.GetByTeamIdAsync(team.Id);
        var result = snapshots.Select(s => new RatingSnapshotDto(s.Date, s.Mu, s.Sigma, s.Rating, s.Competition?.Name)).ToList();
        return Ok(result);
    }

    [HttpGet("{slug}/results")]
    public async Task<ActionResult<PagedResult<TeamResultDto>>> GetResults(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var results = await _teamProfileService.GetResultsAsync(slug, page, pageSize);
        return Ok(results);
    }
}
