using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class CompetitionRepositoryTests
{
    [Test]
    public async Task GetByIdAsync_ExistingCompetition_ReturnsCompetition()
    {
        await using var context = DatabaseFixture.CreateContext();
        var competition = new Competition
        {
            Slug = $"comp-byid-{Guid.NewGuid():N}",
            Name = "Test Competition ById",
            Date = new DateOnly(2024, 6, 1),
            Tier = 1
        };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetByIdAsync(competition.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Test Competition ById"));
    }

    [Test]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        await using var context = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(context);
        var result = await repo.GetByIdAsync(-999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetBySlugAsync_ExistingCompetition_ReturnsCompetition()
    {
        var slug = $"comp-slug-{Guid.NewGuid():N}";
        await using var context = DatabaseFixture.CreateContext();
        context.Competitions.Add(new Competition
        {
            Slug = slug,
            Name = "Slug Test Competition",
            Date = new DateOnly(2024, 5, 15),
            Tier = 2
        });
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetBySlugAsync(slug);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Slug, Is.EqualTo(slug));
    }

    [Test]
    public async Task GetBySlugAsync_NonExistent_ReturnsNull()
    {
        await using var context = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(context);
        var result = await repo.GetBySlugAsync("non-existent-slug-xyz");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetListAsync_FilterByYear_ReturnsMatchingCompetitions()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        context.Competitions.AddRange(
            new Competition { Slug = $"comp-y2024-{prefix}", Name = $"Year2024 {prefix}", Date = new DateOnly(2024, 3, 1), Tier = 1 },
            new Competition { Slug = $"comp-y2023-{prefix}", Name = $"Year2023 {prefix}", Date = new DateOnly(2023, 7, 1), Tier = 1 }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetListAsync(new CompetitionFilter(Year: 2024, Tier: null, Country: null, Search: prefix));

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Name, Does.Contain("Year2024"));
    }

    [Test]
    public async Task GetListAsync_FilterByTier_ReturnsMatchingCompetitions()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        context.Competitions.AddRange(
            new Competition { Slug = $"comp-t1-{prefix}", Name = $"Tier1 {prefix}", Date = new DateOnly(2024, 1, 1), Tier = 1 },
            new Competition { Slug = $"comp-t3-{prefix}", Name = $"Tier3 {prefix}", Date = new DateOnly(2024, 1, 2), Tier = 3 }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetListAsync(new CompetitionFilter(Year: null, Tier: 3, Country: null, Search: prefix));

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Tier, Is.EqualTo(3));
    }

    [Test]
    public async Task GetListAsync_FilterByCountry_ReturnsMatchingCompetitions()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        context.Competitions.AddRange(
            new Competition { Slug = $"comp-cz-{prefix}", Name = $"CZ {prefix}", Date = new DateOnly(2024, 1, 1), Tier = 1, Country = "CZE" },
            new Competition { Slug = $"comp-de-{prefix}", Name = $"DE {prefix}", Date = new DateOnly(2024, 1, 2), Tier = 1, Country = "DEU" }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetListAsync(new CompetitionFilter(Year: null, Tier: null, Country: "CZE", Search: prefix));

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Country, Is.EqualTo("CZE"));
    }

    [Test]
    public async Task GetListAsync_FilterBySearch_ReturnsMatchingCompetitions()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        context.Competitions.AddRange(
            new Competition { Slug = $"comp-srch1-{unique}", Name = $"UniqueSearchTerm{unique}", Date = new DateOnly(2024, 1, 1), Tier = 1 },
            new Competition { Slug = $"comp-srch2-{unique}", Name = $"OtherName{unique}", Date = new DateOnly(2024, 1, 2), Tier = 1 }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetListAsync(new CompetitionFilter(Year: null, Tier: null, Country: null, Search: $"UniqueSearchTerm{unique}"));

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Name, Does.Contain("UniqueSearchTerm"));
    }

    [Test]
    public async Task GetListAsync_Pagination_ReturnsCorrectPage()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        for (var i = 0; i < 5; i++)
        {
            context.Competitions.Add(new Competition
            {
                Slug = $"comp-page-{prefix}-{i}",
                Name = $"Paged {prefix} {i}",
                Date = new DateOnly(2024, 1, i + 1),
                Tier = 1
            });
        }
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetListAsync(new CompetitionFilter(Year: null, Tier: null, Country: null, Search: $"Paged {prefix}", Page: 2, PageSize: 2));

        Assert.That(result.TotalCount, Is.EqualTo(5));
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Page, Is.EqualTo(2));
        Assert.That(result.PageSize, Is.EqualTo(2));
    }

    [Test]
    public async Task GetListAsync_OrderByDateDesc()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        context.Competitions.AddRange(
            new Competition { Slug = $"comp-ord-early-{prefix}", Name = $"Early {prefix}", Date = new DateOnly(2024, 1, 1), Tier = 1 },
            new Competition { Slug = $"comp-ord-late-{prefix}", Name = $"Late {prefix}", Date = new DateOnly(2024, 12, 1), Tier = 1 }
        );
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(queryContext);
        var result = await repo.GetListAsync(new CompetitionFilter(Year: null, Tier: null, Country: null, Search: prefix));

        Assert.That(result.Items[0].Date, Is.GreaterThan(result.Items[1].Date));
    }

    [Test]
    public async Task CreateAsync_ReturnsCompetitionWithId()
    {
        await using var context = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(context);

        var competition = new Competition
        {
            Slug = $"comp-create-{Guid.NewGuid():N}",
            Name = "Created Competition",
            Date = new DateOnly(2024, 8, 1),
            Tier = 2
        };

        var result = await repo.CreateAsync(competition);

        Assert.That(result.Id, Is.GreaterThan(0));
        Assert.That(result.Name, Is.EqualTo("Created Competition"));
    }

    [Test]
    public async Task DeleteCascadeAsync_DeletesCompetitionAndRelatedRunsAndResults()
    {
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler { Name = "DelHandler", NormalizedName = "delhandler", Country = "CZE", Slug = $"del-handler-{Guid.NewGuid():N}" };
        var dog = new Dog { CallName = "DelDog", NormalizedCallName = "deldog", SizeCategory = SizeCategory.L };
        context.Handlers.Add(handler);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team { HandlerId = handler.Id, DogId = dog.Id, Slug = $"del-team-{Guid.NewGuid():N}" };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        var competition = new Competition { Slug = $"comp-del-{Guid.NewGuid():N}", Name = "ToDelete", Date = new DateOnly(2024, 1, 1), Tier = 1 };
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();

        var run = new Run { CompetitionId = competition.Id, Date = new DateOnly(2024, 1, 1), RunNumber = 1, RoundKey = $"rk-del-{Guid.NewGuid():N}", SizeCategory = SizeCategory.L, Discipline = Discipline.Agility };
        context.Runs.Add(run);
        await context.SaveChangesAsync();

        var result = new RunResult { RunId = run.Id, TeamId = team.Id, Eliminated = false };
        context.RunResults.Add(result);
        await context.SaveChangesAsync();

        await using var deleteContext = DatabaseFixture.CreateContext();
        var repo = new CompetitionRepository(deleteContext);
        await repo.DeleteCascadeAsync(competition.Id);

        await using var verifyContext = DatabaseFixture.CreateContext();
        Assert.That(await verifyContext.Competitions.FindAsync(competition.Id), Is.Null);
        Assert.That(await verifyContext.Runs.FindAsync(run.Id), Is.Null);
        Assert.That(await verifyContext.RunResults.FindAsync(result.Id), Is.Null);
    }
}
