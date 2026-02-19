using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class HandlerAliasRepositoryTests
{
    [Test]
    public async Task FindByAliasNameAsync_ExistingAlias_ReturnsAlias()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Alias Handler {suffix}",
            NormalizedName = $"alias handler {suffix}",
            Country = "CZE",
            Slug = $"alias-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        var alias = new HandlerAlias
        {
            AliasName = $"alias name {suffix}",
            CanonicalHandlerId = handler.Id,
            Source = AliasSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
        context.HandlerAliases.Add(alias);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerAliasRepository(queryContext);
        var result = await repo.FindByAliasNameAsync($"alias name {suffix}");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CanonicalHandlerId, Is.EqualTo(handler.Id));
    }

    [Test]
    public async Task FindByAliasNameAsync_NonExistent_ReturnsNull()
    {
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerAliasRepository(queryContext);

        var result = await repo.FindByAliasNameAsync("nonexistent-alias-xyz");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByHandlerIdAsync_MultipleAliases_ReturnsAll()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Multi Alias Handler {suffix}",
            NormalizedName = $"multi alias handler {suffix}",
            Country = "GBR",
            Slug = $"multi-alias-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        context.HandlerAliases.AddRange(
            new HandlerAlias
            {
                AliasName = $"alias one {suffix}",
                CanonicalHandlerId = handler.Id,
                Source = AliasSource.Import,
                CreatedAt = DateTime.UtcNow
            },
            new HandlerAlias
            {
                AliasName = $"alias two {suffix}",
                CanonicalHandlerId = handler.Id,
                Source = AliasSource.FuzzyMatch,
                CreatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new HandlerAliasRepository(queryContext);
        var result = await repo.GetByHandlerIdAsync(handler.Id);

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task CreateAsync_ValidAlias_Persists()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Create Alias Handler {suffix}",
            NormalizedName = $"create alias handler {suffix}",
            Country = "USA",
            Slug = $"create-alias-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        // Act
        await using var repoContext = DatabaseFixture.CreateContext();
        var repo = new HandlerAliasRepository(repoContext);
        var alias = new HandlerAlias
        {
            AliasName = $"new alias {suffix}",
            CanonicalHandlerId = handler.Id,
            Source = AliasSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
        await repo.CreateAsync(alias);

        // Assert
        Assert.That(alias.Id, Is.GreaterThan(0));

        await using var verifyContext = DatabaseFixture.CreateContext();
        var persisted = await verifyContext.HandlerAliases.FindAsync(alias.Id);
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.AliasName, Is.EqualTo($"new alias {suffix}"));
    }
}
