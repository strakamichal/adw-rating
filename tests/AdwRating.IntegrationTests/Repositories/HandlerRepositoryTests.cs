using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class HandlerRepositoryTests
{
    [Test]
    public async Task GetByIdAsync_ExistingHandler_ReturnsHandler()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Test Handler {suffix}",
            NormalizedName = $"test handler {suffix}",
            Country = "CZE",
            Slug = $"test-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);
        var result = await repo.GetByIdAsync(handler.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo(handler.Name));
        Assert.That(result.Country, Is.EqualTo("CZE"));
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);

        var result = await repo.GetByIdAsync(999999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetBySlugAsync_ExistingSlug_ReturnsHandler()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Slug Handler {suffix}",
            NormalizedName = $"slug handler {suffix}",
            Country = "USA",
            Slug = $"slug-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);
        var result = await repo.GetBySlugAsync($"slug-handler-{suffix}");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo(handler.Name));
    }

    [Test]
    public async Task GetBySlugAsync_NonExistentSlug_ReturnsNull()
    {
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);

        var result = await repo.GetBySlugAsync("nonexistent-slug-xyz");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FindByNormalizedNameAndCountryAsync_Match_ReturnsHandler()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Findme Handler {suffix}",
            NormalizedName = $"findme handler {suffix}",
            Country = "GBR",
            Slug = $"findme-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);
        var result = await repo.FindByNormalizedNameAndCountryAsync($"findme handler {suffix}", "GBR");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(handler.Id));
    }

    [Test]
    public async Task FindByNormalizedNameAndCountryAsync_WrongCountry_ReturnsNull()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Country Handler {suffix}",
            NormalizedName = $"country handler {suffix}",
            Country = "CZE",
            Slug = $"country-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);
        var result = await repo.FindByNormalizedNameAndCountryAsync($"country handler {suffix}", "USA");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task SearchAsync_MatchingQuery_ReturnsResults()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        context.Handlers.Add(new Handler
        {
            Name = $"Searchable Alpha {suffix}",
            NormalizedName = $"searchable alpha {suffix}",
            Country = "CZE",
            Slug = $"searchable-alpha-{suffix}"
        });
        context.Handlers.Add(new Handler
        {
            Name = $"Searchable Beta {suffix}",
            NormalizedName = $"searchable beta {suffix}",
            Country = "USA",
            Slug = $"searchable-beta-{suffix}"
        });
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);
        var result = await repo.SearchAsync($"searchable", 10);

        // Assert
        Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task SearchAsync_LimitApplied_ReturnsLimitedResults()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        for (var i = 0; i < 5; i++)
        {
            context.Handlers.Add(new Handler
            {
                Name = $"Limited {suffix} Handler {i}",
                NormalizedName = $"limited {suffix} handler {i}",
                Country = "CZE",
                Slug = $"limited-{suffix}-handler-{i}"
            });
        }
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(queryContext);
        var result = await repo.SearchAsync($"limited {suffix}", 3);

        // Assert
        Assert.That(result.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task CreateAsync_ValidHandler_PersistsAndReturnsWithId()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(context);
        var handler = new Handler
        {
            Name = $"Created Handler {suffix}",
            NormalizedName = $"created handler {suffix}",
            Country = "DEU",
            Slug = $"created-handler-{suffix}"
        };

        // Act
        var result = await repo.CreateAsync(handler);

        // Assert
        Assert.That(result.Id, Is.GreaterThan(0));

        await using var verifyContext = DatabaseFixture.CreateContext();
        var persisted = await verifyContext.Handlers.FindAsync(result.Id);
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.Name, Is.EqualTo(handler.Name));
    }

    [Test]
    public async Task UpdateAsync_ModifiedHandler_PersistsChanges()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Update Handler {suffix}",
            NormalizedName = $"update handler {suffix}",
            Country = "FRA",
            Slug = $"update-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        // Act
        await using var updateContext = DatabaseFixture.CreateContext();
        var loaded = await updateContext.Handlers.FindAsync(handler.Id);
        loaded!.Country = "ITA";
        var repo = new HandlerRepository(updateContext);
        await repo.UpdateAsync(loaded);

        // Assert
        await using var verifyContext = DatabaseFixture.CreateContext();
        var updated = await verifyContext.Handlers.FindAsync(handler.Id);
        Assert.That(updated!.Country, Is.EqualTo("ITA"));
    }

    [Test]
    public async Task MergeAsync_ReassignsTeamsAndAliasesAndDeletesSource()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();

        var sourceHandler = new Handler
        {
            Name = $"Source Handler {suffix}",
            NormalizedName = $"source handler {suffix}",
            Country = "CZE",
            Slug = $"source-handler-{suffix}"
        };
        var targetHandler = new Handler
        {
            Name = $"Target Handler {suffix}",
            NormalizedName = $"target handler {suffix}",
            Country = "CZE",
            Slug = $"target-handler-{suffix}"
        };
        context.Handlers.AddRange(sourceHandler, targetHandler);
        await context.SaveChangesAsync();

        var dog = new Dog
        {
            CallName = $"MergeDog {suffix}",
            NormalizedCallName = $"mergedog {suffix}",
            SizeCategory = SizeCategory.M
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var team = new Team
        {
            HandlerId = sourceHandler.Id,
            DogId = dog.Id,
            Slug = $"merge-team-{suffix}",
            Mu = 25.0f,
            Sigma = 8.33f,
            Rating = 0.0f
        };
        context.Teams.Add(team);

        var alias = new HandlerAlias
        {
            AliasName = $"source alias {suffix}",
            CanonicalHandlerId = sourceHandler.Id,
            Source = AliasSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
        context.HandlerAliases.Add(alias);
        await context.SaveChangesAsync();

        // Act
        await using var mergeContext = DatabaseFixture.CreateContext();
        var repo = new HandlerRepository(mergeContext);
        await repo.MergeAsync(sourceHandler.Id, targetHandler.Id);

        // Assert
        await using var verifyContext = DatabaseFixture.CreateContext();

        // Source handler deleted
        var deletedHandler = await verifyContext.Handlers.FindAsync(sourceHandler.Id);
        Assert.That(deletedHandler, Is.Null);

        // Team reassigned to target
        var reassignedTeam = await verifyContext.Teams.FindAsync(team.Id);
        Assert.That(reassignedTeam, Is.Not.Null);
        Assert.That(reassignedTeam!.HandlerId, Is.EqualTo(targetHandler.Id));

        // Alias reassigned to target
        var reassignedAlias = await verifyContext.HandlerAliases.FindAsync(alias.Id);
        Assert.That(reassignedAlias, Is.Not.Null);
        Assert.That(reassignedAlias!.CanonicalHandlerId, Is.EqualTo(targetHandler.Id));
    }
}
