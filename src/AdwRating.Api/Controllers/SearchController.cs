using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdwRating.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchResult>>> Search(
        [FromQuery] string q = "",
        [FromQuery] int limit = 10)
    {
        if (q.Length < 2)
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Bad Request",
                Detail = "Search query must be at least 2 characters."
            });

        var results = await _searchService.SearchAsync(q, limit);
        return Ok(results);
    }
}
