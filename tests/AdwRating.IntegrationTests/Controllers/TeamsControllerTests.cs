using System.Net;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;

namespace AdwRating.IntegrationTests.Controllers;

[TestFixture]
public class TeamsControllerTests : ApiTestBase
{
    private string _suffix = null!;
    private string _teamSlug = null!;

    [OneTimeSetUp]
    public async Task SeedData()
    {
        _suffix = Guid.NewGuid().ToString("N")[..8];
        _teamSlug = $"team-detail-{_suffix}";

        await using var context = CreateContext();

        var handler = new Handler
        {
            Name = $"Team Handler {_suffix}",
            NormalizedName = $"team handler {_suffix}",
            Country = "GBR",
            Slug = $"team-handler-{_suffix}"
        };
        var dog = new Dog
        {
            CallName = $"TeamDog {_suffix}",
            NormalizedCallName = $"teamdog {_suffix}",
            SizeCategory = SizeCategory.M,
            Breed = "Border Collie"
        };
        context.Handlers.Add(handler);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = _teamSlug,
            Mu = 28.0f,
            Sigma = 4.0f,
            Rating = 16.0f,
            PrevRating = 14.0f,
            PeakRating = 18.0f,
            RunCount = 15,
            FinishedRunCount = 12,
            Top3RunCount = 5,
            IsActive = true,
            IsProvisional = false,
            TierLabel = TierLabel.Champion
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        // Add a competition, run, and run result for the results endpoint
        var competition = new Competition
        {
            Slug = $"comp-{_suffix}",
            Name = $"Test Competition {_suffix}",
            Date = new DateOnly(2025, 6, 15),
            Tier = 1,
            Country = "GBR"
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        var run = new Run
        {
            CompetitionId = competition.Id,
            Date = new DateOnly(2025, 6, 15),
            RunNumber = 1,
            RoundKey = $"2025-06-15-M-Agility-1-{_suffix}",
            SizeCategory = SizeCategory.M,
            Discipline = Discipline.Agility,
            IsTeamRound = false
        };
        context.Runs.Add(run);
        await context.SaveChangesAsync();

        var runResult = new RunResult
        {
            RunId = run.Id,
            TeamId = team.Id,
            Rank = 2,
            Faults = 0,
            TimeFaults = 1.5f,
            Time = 32.5f,
            Speed = 4.8f,
            Eliminated = false
        };
        context.RunResults.Add(runResult);
        await context.SaveChangesAsync();

        // Add a rating snapshot for the history endpoint
        var snapshot = new RatingSnapshot
        {
            TeamId = team.Id,
            RunResultId = runResult.Id,
            CompetitionId = competition.Id,
            Date = new DateOnly(2025, 6, 15),
            Mu = 28.0f,
            Sigma = 4.0f,
            Rating = 16.0f
        };
        context.RatingSnapshots.Add(snapshot);
        await context.SaveChangesAsync();
    }

    [Test]
    public async Task GetBySlug_ExistingTeam_ReturnsTeamDetail()
    {
        var response = await Client.GetAsync($"/api/teams/{_teamSlug}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<TeamDetailDto>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Slug, Is.EqualTo(_teamSlug));
        Assert.That(result.HandlerCountry, Is.EqualTo("GBR"));
        Assert.That(result.DogBreed, Is.EqualTo("Border Collie"));
        Assert.That(result.RunCount, Is.EqualTo(15));
    }

    [Test]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        var response = await Client.GetAsync("/api/teams/nonexistent-team-slug");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetHistory_ExistingTeam_ReturnsSnapshots()
    {
        var response = await Client.GetAsync($"/api/teams/{_teamSlug}/history");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        Assert.That(json, Does.Contain("rating"));
    }

    [Test]
    public async Task GetHistory_UnknownSlug_Returns404()
    {
        var response = await Client.GetAsync("/api/teams/nonexistent-team-slug/history");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetResults_ExistingTeam_ReturnsPaginatedResults()
    {
        var response = await Client.GetAsync($"/api/teams/{_teamSlug}/results");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await ReadJsonAsync<PagedResult<TeamResultDto>>(response);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.Items[0].CompetitionName, Does.Contain("Test Competition"));
    }
}
