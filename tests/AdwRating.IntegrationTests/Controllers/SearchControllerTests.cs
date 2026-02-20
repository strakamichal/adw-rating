using System.Net;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;

namespace AdwRating.IntegrationTests.Controllers;

[TestFixture]
public class SearchControllerTests : ApiTestBase
{
    private string _suffix = null!;

    [OneTimeSetUp]
    public async Task SeedData()
    {
        _suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = CreateContext();

        var handler = new Handler
        {
            Name = $"SearchHandler {_suffix}",
            NormalizedName = $"searchhandler {_suffix}",
            Country = "USA",
            Slug = $"searchhandler-{_suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        var dog = new Dog
        {
            CallName = $"SearchDog {_suffix}",
            NormalizedCallName = $"searchdog {_suffix}",
            SizeCategory = SizeCategory.S
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"searchteam-{_suffix}",
            Mu = 25.0f,
            Sigma = 8.0f,
            Rating = 1.0f,
            IsActive = true
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();
    }

    [Test]
    public async Task Search_ValidQuery_ReturnsResults()
    {
        var response = await Client.GetAsync($"/api/search?q=SearchHandler {_suffix}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var results = await ReadJsonAsync<List<SearchResult>>(response);
        Assert.That(results, Is.Not.Null);
        Assert.That(results!.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(results.Any(r => r.Type == "handler"), Is.True);
    }

    [Test]
    public async Task Search_QueryTooShort_Returns400()
    {
        var response = await Client.GetAsync("/api/search?q=a");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Search_EmptyQuery_Returns400()
    {
        var response = await Client.GetAsync("/api/search?q=");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Search_WithLimit_RespectsLimit()
    {
        var response = await Client.GetAsync($"/api/search?q=SearchHandler {_suffix}&limit=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var results = await ReadJsonAsync<List<SearchResult>>(response);
        Assert.That(results, Is.Not.Null);
    }

    [Test]
    public async Task Search_NoMatchingQuery_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/api/search?q=zzzznonexistent999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var results = await ReadJsonAsync<List<SearchResult>>(response);
        Assert.That(results, Is.Not.Null);
        Assert.That(results!.Count, Is.EqualTo(0));
    }
}
