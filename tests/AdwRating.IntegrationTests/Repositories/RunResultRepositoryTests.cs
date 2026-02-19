using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class RunResultRepositoryTests
{
    private static async Task<(Competition Competition, Run Run, Team Team)> CreatePrerequisitesAsync(
        AdwRating.Data.Mssql.AppDbContext context,
        DateOnly? runDate = null)
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        var handler = new Handler
        {
            Name = $"Handler {guid}",
            NormalizedName = $"handler {guid}",
            Country = "CZE",
            Slug = $"handler-{guid}"
        };
        var dog = new Dog
        {
            CallName = $"Dog{guid}",
            NormalizedCallName = $"dog{guid}",
            SizeCategory = SizeCategory.L
        };
        context.Handlers.Add(handler);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"team-{guid}"
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var date = runDate ?? new DateOnly(2024, 6, 1);
        var competition = new Competition
        {
            Slug = $"comp-rr-{guid}",
            Name = $"RR Comp {guid}",
            Date = date,
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        var run = new Run
        {
            CompetitionId = competition.Id,
            Date = date,
            RunNumber = 1,
            RoundKey = $"rk-rr-{guid}",
            SizeCategory = SizeCategory.L,
            Discipline = Discipline.Agility
        };
        context.Runs.Add(run);
        await context.SaveChangesAsync();

        return (competition, run, team);
    }

    [Test]
    public async Task GetByRunIdAsync_ReturnsResultsForRun()
    {
        await using var context = DatabaseFixture.CreateContext();
        var (_, run, team) = await CreatePrerequisitesAsync(context);

        context.RunResults.Add(new RunResult { RunId = run.Id, TeamId = team.Id, Rank = 1, Eliminated = false });
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunResultRepository(queryContext);
        var results = await repo.GetByRunIdAsync(run.Id);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].RunId, Is.EqualTo(run.Id));
    }

    [Test]
    public async Task GetByRunIdsAsync_ReturnsBatchResultsWithTeam()
    {
        await using var context = DatabaseFixture.CreateContext();
        var (comp, run1, team) = await CreatePrerequisitesAsync(context);

        // Create a second run for the same competition
        var run2 = new Run
        {
            CompetitionId = comp.Id,
            Date = new DateOnly(2024, 6, 1),
            RunNumber = 2,
            RoundKey = $"rk-batch-{Guid.NewGuid():N}",
            SizeCategory = SizeCategory.L,
            Discipline = Discipline.Jumping
        };
        context.Runs.Add(run2);
        await context.SaveChangesAsync();

        context.RunResults.AddRange(
            new RunResult { RunId = run1.Id, TeamId = team.Id, Rank = 1, Eliminated = false },
            new RunResult { RunId = run2.Id, TeamId = team.Id, Rank = 2, Eliminated = false }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunResultRepository(queryContext);
        var results = await repo.GetByRunIdsAsync(new[] { run1.Id, run2.Id });

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results.All(r => r.Team is not null), Is.True);
    }

    [Test]
    public async Task GetByTeamIdAsync_ReturnsResultsWithRunAndCompetition()
    {
        await using var context = DatabaseFixture.CreateContext();
        var (_, run, team) = await CreatePrerequisitesAsync(context);

        context.RunResults.Add(new RunResult { RunId = run.Id, TeamId = team.Id, Rank = 3, Eliminated = false });
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunResultRepository(queryContext);
        var results = await repo.GetByTeamIdAsync(team.Id);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Run, Is.Not.Null);
        Assert.That(results[0].Run.Competition, Is.Not.Null);
    }

    [Test]
    public async Task GetByTeamIdAsync_WithAfterFilter_ReturnsOnlyRecentResults()
    {
        await using var context = DatabaseFixture.CreateContext();
        // Create two competitions at different dates
        var guid = Guid.NewGuid().ToString("N")[..8];
        var handler = new Handler { Name = $"H {guid}", NormalizedName = $"h {guid}", Country = "CZE", Slug = $"h-{guid}" };
        var dog = new Dog { CallName = $"D{guid}", NormalizedCallName = $"d{guid}", SizeCategory = SizeCategory.L };
        context.Handlers.Add(handler);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team { HandlerId = handler.Id, DogId = dog.Id, Slug = $"t-{guid}" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var comp1 = new Competition { Slug = $"comp-old-{guid}", Name = $"Old {guid}", Date = new DateOnly(2023, 1, 1), Tier = 1 };
        var comp2 = new Competition { Slug = $"comp-new-{guid}", Name = $"New {guid}", Date = new DateOnly(2024, 6, 1), Tier = 1 };
        context.Competitions.AddRange(comp1, comp2);
        await context.SaveChangesAsync();

        var run1 = new Run { CompetitionId = comp1.Id, Date = new DateOnly(2023, 1, 1), RunNumber = 1, RoundKey = $"rk-old-{guid}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility };
        var run2 = new Run { CompetitionId = comp2.Id, Date = new DateOnly(2024, 6, 1), RunNumber = 1, RoundKey = $"rk-new-{guid}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility };
        context.Runs.AddRange(run1, run2);
        await context.SaveChangesAsync();

        context.RunResults.AddRange(
            new RunResult { RunId = run1.Id, TeamId = team.Id, Rank = 5, Eliminated = false },
            new RunResult { RunId = run2.Id, TeamId = team.Id, Rank = 1, Eliminated = false }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunResultRepository(queryContext);
        var results = await repo.GetByTeamIdAsync(team.Id, after: new DateOnly(2024, 1, 1));

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Run.Date, Is.GreaterThanOrEqualTo(new DateOnly(2024, 1, 1)));
    }

    [Test]
    public async Task CreateBatchAsync_InsertsMultipleResults()
    {
        await using var context = DatabaseFixture.CreateContext();
        var (_, run, team) = await CreatePrerequisitesAsync(context);

        // Create a second team
        var guid2 = Guid.NewGuid().ToString("N")[..8];
        var handler2 = new Handler { Name = $"H2 {guid2}", NormalizedName = $"h2 {guid2}", Country = "DEU", Slug = $"h2-{guid2}" };
        var dog2 = new Dog { CallName = $"D2{guid2}", NormalizedCallName = $"d2{guid2}", SizeCategory = SizeCategory.L };
        context.Handlers.Add(handler2);
        context.Dogs.Add(dog2);
        await context.SaveChangesAsync();

        var team2 = new Team { HandlerId = handler2.Id, DogId = dog2.Id, Slug = $"t2-{guid2}" };
        context.Teams.Add(team2);
        await context.SaveChangesAsync();

        await using var insertContext = DatabaseFixture.CreateContext();
        var repo = new RunResultRepository(insertContext);
        await repo.CreateBatchAsync(new[]
        {
            new RunResult { RunId = run.Id, TeamId = team.Id, Rank = 1, Eliminated = false },
            new RunResult { RunId = run.Id, TeamId = team2.Id, Rank = 2, Eliminated = false }
        });

        await using var verifyContext = DatabaseFixture.CreateContext();
        var repo2 = new RunResultRepository(verifyContext);
        var results = await repo2.GetByRunIdAsync(run.Id);

        Assert.That(results, Has.Count.EqualTo(2));
    }
}
