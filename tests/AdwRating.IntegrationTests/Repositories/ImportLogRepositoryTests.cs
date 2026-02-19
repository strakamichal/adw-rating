using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class ImportLogRepositoryTests
{
    [Test]
    public async Task CreateAsync_InsertsImportLog()
    {
        await using var context = DatabaseFixture.CreateContext();
        var repo = new ImportLogRepository(context);
        var log = new ImportLog
        {
            FileName = $"test-{Guid.NewGuid():N}.csv",
            ImportedAt = DateTime.UtcNow,
            Status = ImportStatus.Success,
            RowCount = 100,
            NewHandlersCount = 5,
            NewDogsCount = 3,
            NewTeamsCount = 4
        };
        await repo.CreateAsync(log);

        Assert.That(log.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task GetRecentAsync_ReturnsOrderedByImportedAtDesc()
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();

        var competition = new Competition
        {
            Slug = $"comp-imp-{guid}",
            Name = $"Import Comp {guid}",
            Date = new DateOnly(2024, 6, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        context.ImportLogs.AddRange(
            new ImportLog { CompetitionId = competition.Id, FileName = $"old-{guid}.csv", ImportedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = ImportStatus.Success, RowCount = 50 },
            new ImportLog { CompetitionId = competition.Id, FileName = $"new-{guid}.csv", ImportedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), Status = ImportStatus.Success, RowCount = 100 }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new ImportLogRepository(queryContext);
        var logs = await repo.GetRecentAsync(100);

        // Find our logs
        var ourLogs = logs.Where(l => l.FileName.Contains(guid)).ToList();
        Assert.That(ourLogs, Has.Count.EqualTo(2));
        Assert.That(ourLogs[0].ImportedAt, Is.GreaterThanOrEqualTo(ourLogs[1].ImportedAt));
    }

    [Test]
    public async Task GetRecentAsync_IncludesCompetitionNavigation()
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();

        var competition = new Competition
        {
            Slug = $"comp-impnav-{guid}",
            Name = $"Import Nav Comp {guid}",
            Date = new DateOnly(2024, 6, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        context.ImportLogs.Add(new ImportLog
        {
            CompetitionId = competition.Id,
            FileName = $"nav-{guid}.csv",
            ImportedAt = DateTime.UtcNow,
            Status = ImportStatus.Success,
            RowCount = 75
        });
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new ImportLogRepository(queryContext);
        var logs = await repo.GetRecentAsync(100);

        var ourLog = logs.FirstOrDefault(l => l.FileName == $"nav-{guid}.csv");
        Assert.That(ourLog, Is.Not.Null);
        Assert.That(ourLog!.Competition, Is.Not.Null);
        Assert.That(ourLog.Competition!.Name, Does.Contain(guid));
    }

    [Test]
    public async Task GetRecentAsync_RespectsCountLimit()
    {
        await using var context = DatabaseFixture.CreateContext();
        var repo = new ImportLogRepository(context);

        // Insert several logs
        for (var i = 0; i < 5; i++)
        {
            await repo.CreateAsync(new ImportLog
            {
                FileName = $"limit-{Guid.NewGuid():N}.csv",
                ImportedAt = DateTime.UtcNow.AddMinutes(-i),
                Status = ImportStatus.Success,
                RowCount = 10
            });
        }

        await using var queryContext = DatabaseFixture.CreateContext();
        var queryRepo = new ImportLogRepository(queryContext);
        var logs = await queryRepo.GetRecentAsync(3);

        Assert.That(logs, Has.Count.EqualTo(3));
    }
}
