using System.Net;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;

namespace AdwRating.IntegrationTests.Controllers;

[TestFixture]
public class RankingsControllerTests : ApiTestBase
{
    private string _suffix = null!;

    [OneTimeSetUp]
    public async Task SeedData()
    {
        _suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = CreateContext();

        var handler = new Handler
        {
            Name = $"Rank Handler {_suffix}",
            NormalizedName = $"rank handler {_suffix}",
            Country = "CZE",
            Slug = $"rank-handler-{_suffix}"
        };
        var dog = new Dog
        {
            CallName = $"RankDog {_suffix}",
            NormalizedCallName = $"rankdog {_suffix}",
            SizeCategory = SizeCategory.L
        };
        context.Handlers.Add(handler);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"rank-team-{_suffix}",
            Mu = 30.0f,
            Sigma = 5.0f,
            Rating = 15.0f,
            RunCount = 10,
            FinishedRunCount = 8,
            Top3RunCount = 3,
            IsActive = true,
            IsProvisional = false,
            TierLabel = TierLabel.Expert
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();
    }

    [Test]
    public async Task GetRankings_WithSizeFilter_ReturnsPagedResult()
    {
        var response = await Client.GetAsync("/api/rankings?size=L");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<PagedResult<TeamRankingDto>>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Not.Null);
        Assert.That(result.Page, Is.EqualTo(1));
        Assert.That(result.PageSize, Is.EqualTo(50));
    }

    [Test]
    public async Task GetRankings_WithPagination_RespectsPageSize()
    {
        var response = await Client.GetAsync("/api/rankings?size=L&page=1&pageSize=5");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<PagedResult<TeamRankingDto>>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PageSize, Is.EqualTo(5));
    }

    [Test]
    public async Task GetRankings_ResponseIsCamelCase()
    {
        var response = await Client.GetAsync("/api/rankings?size=L");
        var json = await response.Content.ReadAsStringAsync();

        // Verify camelCase JSON serialization
        Assert.That(json, Does.Contain("\"items\""));
        Assert.That(json, Does.Contain("\"totalCount\""));
        Assert.That(json, Does.Contain("\"pageSize\""));
    }

    [Test]
    public async Task GetSummary_ReturnsRankingSummary()
    {
        var response = await Client.GetAsync("/api/rankings/summary");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<RankingSummary>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.QualifiedTeams, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.Competitions, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.Runs, Is.GreaterThanOrEqualTo(0));
    }
}
