using System.Net;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Models;

namespace AdwRating.IntegrationTests.Controllers;

[TestFixture]
public class CompetitionsControllerTests : ApiTestBase
{
    private string _suffix = null!;

    [OneTimeSetUp]
    public async Task SeedData()
    {
        _suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = CreateContext();

        var competition = new Competition
        {
            Slug = $"comp-list-{_suffix}",
            Name = $"AWC {_suffix}",
            Date = new DateOnly(2025, 10, 1),
            EndDate = new DateOnly(2025, 10, 3),
            Tier = 1,
            Country = "FRA",
            Location = "Paris",
            Organization = "FCI"
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();
    }

    [Test]
    public async Task GetList_ReturnsPagedCompetitions()
    {
        var response = await Client.GetAsync("/api/competitions");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<PagedResult<CompetitionDetailDto>>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Not.Null);
        Assert.That(result.TotalCount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task GetList_WithYearFilter_FiltersCorrectly()
    {
        var response = await Client.GetAsync("/api/competitions?year=2025");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<PagedResult<CompetitionDetailDto>>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.All(c => c.Date.Year == 2025), Is.True);
    }

    [Test]
    public async Task GetList_WithPagination_RespectsPageSize()
    {
        var response = await Client.GetAsync("/api/competitions?page=1&pageSize=5");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<PagedResult<CompetitionDetailDto>>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PageSize, Is.EqualTo(5));
    }

    [Test]
    public async Task GetList_ResponseIsCamelCase()
    {
        var response = await Client.GetAsync("/api/competitions");
        var json = await response.Content.ReadAsStringAsync();

        Assert.That(json, Does.Contain("\"items\""));
        Assert.That(json, Does.Contain("\"totalCount\""));
    }
}
