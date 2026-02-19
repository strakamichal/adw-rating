using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class RatingSnapshotRepositoryTests
{
    private static async Task<(Team Team, Competition Competition, RunResult RunResult1, RunResult RunResult2)> CreatePrerequisitesAsync(
        AdwRating.Data.Mssql.AppDbContext context)
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        var handler = new Handler { Name = $"SnapH {guid}", NormalizedName = $"snaph {guid}", Country = "CZE", Slug = $"snaph-{guid}" };
        var dog = new Dog { CallName = $"SnapD{guid}", NormalizedCallName = $"snapd{guid}", SizeCategory = SizeCategory.L };
        context.Handlers.Add(handler);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team { HandlerId = handler.Id, DogId = dog.Id, Slug = $"snapt-{guid}" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var competition = new Competition { Slug = $"comp-snap-{guid}", Name = $"Snap Comp {guid}", Date = new DateOnly(2024, 6, 1), Tier = 1 };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        var run1 = new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 6, 1), RunNumber = 1, RoundKey = $"rk-snap1-{guid}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility };
        var run2 = new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 7, 1), RunNumber = 2, RoundKey = $"rk-snap2-{guid}", SizeCategory = SizeCategory.L, Discipline = Discipline.Jumping };
        context.Runs.AddRange(run1, run2);
        await context.SaveChangesAsync();

        var runResult1 = new RunResult { RunId = run1.Id, TeamId = team.Id, Rank = 1, Eliminated = false };
        var runResult2 = new RunResult { RunId = run2.Id, TeamId = team.Id, Rank = 2, Eliminated = false };
        context.RunResults.AddRange(runResult1, runResult2);
        await context.SaveChangesAsync();

        return (team, competition, runResult1, runResult2);
    }

    [Test]
    public async Task GetByTeamIdAsync_ReturnsSnapshotsOrderedByDate()
    {
        await using var context = DatabaseFixture.CreateContext();
        var (team, competition, runResult1, runResult2) = await CreatePrerequisitesAsync(context);

        context.RatingSnapshots.AddRange(
            new RatingSnapshot { TeamId = team.Id, RunResultId = runResult2.Id, CompetitionId = competition.Id, Date = new DateOnly(2024, 7, 1), Mu = 25.0f, Sigma = 8.0f, Rating = 1.0f },
            new RatingSnapshot { TeamId = team.Id, RunResultId = runResult1.Id, CompetitionId = competition.Id, Date = new DateOnly(2024, 6, 1), Mu = 25.0f, Sigma = 8.3f, Rating = 0.1f }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RatingSnapshotRepository(queryContext);
        var snapshots = await repo.GetByTeamIdAsync(team.Id);

        Assert.That(snapshots, Has.Count.EqualTo(2));
        Assert.That(snapshots[0].Date, Is.LessThanOrEqualTo(snapshots[1].Date));
    }

    [Test]
    public async Task ReplaceAllAsync_DeletesExistingAndInsertsNew()
    {
        await using var context = DatabaseFixture.CreateContext();
        var (team, competition, runResult1, runResult2) = await CreatePrerequisitesAsync(context);

        // Insert initial snapshot
        context.RatingSnapshots.Add(new RatingSnapshot
        {
            TeamId = team.Id,
            RunResultId = runResult1.Id,
            CompetitionId = competition.Id,
            Date = new DateOnly(2024, 6, 1),
            Mu = 25.0f,
            Sigma = 8.3f,
            Rating = 0.1f
        });
        await context.SaveChangesAsync();

        // Create additional prerequisites for the replacement snapshots (need unique RunResultIds)
        var guid2 = Guid.NewGuid().ToString("N")[..8];
        var handler2 = new Handler { Name = $"SnapH2 {guid2}", NormalizedName = $"snaph2 {guid2}", Country = "CZE", Slug = $"snaph2-{guid2}" };
        var dog2 = new Dog { CallName = $"SnapD2{guid2}", NormalizedCallName = $"snapd2{guid2}", SizeCategory = SizeCategory.L };
        context.Handlers.Add(handler2);
        context.Dogs.Add(dog2);
        await context.SaveChangesAsync();

        var team2 = new Team { HandlerId = handler2.Id, DogId = dog2.Id, Slug = $"snapt2-{guid2}" };
        context.Teams.Add(team2);
        await context.SaveChangesAsync();

        var run3 = new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 7, 1), RunNumber = 3, RoundKey = $"rk-rep1-{guid2}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility };
        var run4 = new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 8, 1), RunNumber = 4, RoundKey = $"rk-rep2-{guid2}", SizeCategory = SizeCategory.L, Discipline = Discipline.Jumping };
        context.Runs.AddRange(run3, run4);
        await context.SaveChangesAsync();

        var rr3 = new RunResult { RunId = run3.Id, TeamId = team2.Id, Rank = 1, Eliminated = false };
        var rr4 = new RunResult { RunId = run4.Id, TeamId = team2.Id, Rank = 1, Eliminated = false };
        context.RunResults.AddRange(rr3, rr4);
        await context.SaveChangesAsync();

        // Replace all with new snapshots using distinct RunResultIds
        await using var replaceContext = DatabaseFixture.CreateContext();
        var repo = new RatingSnapshotRepository(replaceContext);
        await repo.ReplaceAllAsync(new[]
        {
            new RatingSnapshot { TeamId = team2.Id, RunResultId = rr3.Id, CompetitionId = competition.Id, Date = new DateOnly(2024, 7, 1), Mu = 26.0f, Sigma = 7.5f, Rating = 3.5f },
            new RatingSnapshot { TeamId = team2.Id, RunResultId = rr4.Id, CompetitionId = competition.Id, Date = new DateOnly(2024, 8, 1), Mu = 27.0f, Sigma = 7.0f, Rating = 6.0f }
        });

        await using var verifyContext = DatabaseFixture.CreateContext();
        var repo2 = new RatingSnapshotRepository(verifyContext);

        // Old team1 snapshot should be gone (all deleted)
        var team1Snapshots = await repo2.GetByTeamIdAsync(team.Id);
        Assert.That(team1Snapshots, Has.Count.EqualTo(0));

        // New team2 snapshots should exist
        var team2Snapshots = await repo2.GetByTeamIdAsync(team2.Id);
        Assert.That(team2Snapshots, Has.Count.EqualTo(2));
        Assert.That(team2Snapshots.All(s => s.Mu >= 26.0f), Is.True);
    }
}
