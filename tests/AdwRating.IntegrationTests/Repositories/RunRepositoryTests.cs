using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class RunRepositoryTests
{
    [Test]
    public async Task GetByCompetitionIdAsync_ReturnsRunsOrderedByRunNumber()
    {
        await using var context = DatabaseFixture.CreateContext();
        var competition = new Competition
        {
            Slug = $"comp-runs-{Guid.NewGuid():N}",
            Name = "Run Test Comp",
            Date = new DateOnly(2024, 6, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        context.Runs.AddRange(
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 6, 1), RunNumber = 3, RoundKey = $"rk3-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility },
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 6, 1), RunNumber = 1, RoundKey = $"rk1-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Jumping }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunRepository(queryContext);
        var runs = await repo.GetByCompetitionIdAsync(competition.Id);

        Assert.That(runs, Has.Count.EqualTo(2));
        Assert.That(runs[0].RunNumber, Is.LessThan(runs[1].RunNumber));
    }

    [Test]
    public async Task GetByCompetitionAndRoundKeyAsync_ExistingRun_ReturnsRun()
    {
        var roundKey = $"rk-unique-{Guid.NewGuid():N}";
        await using var context = DatabaseFixture.CreateContext();
        var competition = new Competition
        {
            Slug = $"comp-rk-{Guid.NewGuid():N}",
            Name = "RoundKey Test",
            Date = new DateOnly(2024, 6, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        context.Runs.Add(new Run
        {
            CompetitionId = competition.Id,
            Date = new DateOnly(2024, 6, 1),
            RunNumber = 1,
            RoundKey = roundKey,
            SizeCategory = SizeCategory.M,
            Discipline = Discipline.Agility
        });
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunRepository(queryContext);
        var result = await repo.GetByCompetitionAndRoundKeyAsync(competition.Id, roundKey);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.RoundKey, Is.EqualTo(roundKey));
    }

    [Test]
    public async Task GetByCompetitionAndRoundKeyAsync_NonExistent_ReturnsNull()
    {
        await using var context = DatabaseFixture.CreateContext();
        var repo = new RunRepository(context);
        var result = await repo.GetByCompetitionAndRoundKeyAsync(-999, "non-existent-rk");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAllInWindowAsync_ReturnsRunsAfterCutoffWithCompetition()
    {
        await using var context = DatabaseFixture.CreateContext();
        var competition = new Competition
        {
            Slug = $"comp-window-{Guid.NewGuid():N}",
            Name = "Window Test",
            Date = new DateOnly(2024, 6, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        context.Runs.AddRange(
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 1, 1), RunNumber = 1, RoundKey = $"rk-old-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility },
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 7, 1), RunNumber = 2, RoundKey = $"rk-new-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Jumping }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunRepository(queryContext);
        var runs = await repo.GetAllInWindowAsync(new DateOnly(2024, 6, 1));

        Assert.That(runs.Any(r => r.CompetitionId == competition.Id && r.Date >= new DateOnly(2024, 6, 1)), Is.True);
        // Verify Competition navigation property is loaded
        var windowRun = runs.First(r => r.CompetitionId == competition.Id && r.Date >= new DateOnly(2024, 6, 1));
        Assert.That(windowRun.Competition, Is.Not.Null);
    }

    [Test]
    public async Task GetAllInWindowAsync_OrderedByDateAsc()
    {
        await using var context = DatabaseFixture.CreateContext();
        var competition = new Competition
        {
            Slug = $"comp-winord-{Guid.NewGuid():N}",
            Name = "Window Order Test",
            Date = new DateOnly(2025, 1, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        context.Runs.AddRange(
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2025, 3, 1), RunNumber = 1, RoundKey = $"rk-mar-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility },
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2025, 1, 15), RunNumber = 2, RoundKey = $"rk-jan-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Jumping }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RunRepository(queryContext);
        var runs = await repo.GetAllInWindowAsync(new DateOnly(2025, 1, 1));

        var compRuns = runs.Where(r => r.CompetitionId == competition.Id).ToList();
        Assert.That(compRuns, Has.Count.EqualTo(2));
        Assert.That(compRuns[0].Date, Is.LessThanOrEqualTo(compRuns[1].Date));
    }

    [Test]
    public async Task CreateBatchAsync_InsertsMultipleRuns()
    {
        await using var context = DatabaseFixture.CreateContext();
        var competition = new Competition
        {
            Slug = $"comp-batch-{Guid.NewGuid():N}",
            Name = "Batch Test",
            Date = new DateOnly(2024, 6, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        await using var insertContext = DatabaseFixture.CreateContext();
        var repo = new RunRepository(insertContext);
        var runs = new[]
        {
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 6, 1), RunNumber = 1, RoundKey = $"rk-b1-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility },
            new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 6, 1), RunNumber = 2, RoundKey = $"rk-b2-{Guid.NewGuid():N}", SizeCategory = SizeCategory.M, Discipline = Discipline.Jumping }
        };
        await repo.CreateBatchAsync(runs);

        await using var verifyContext = DatabaseFixture.CreateContext();
        var repo2 = new RunRepository(verifyContext);
        var result = await repo2.GetByCompetitionIdAsync(competition.Id);

        Assert.That(result, Has.Count.EqualTo(2));
    }
}
