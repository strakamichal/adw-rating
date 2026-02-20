using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class DogAliasRepositoryTests
{
    [Test]
    public async Task FindByAliasNameAndTypeAsync_ExistingAlias_ReturnsAlias()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"AliasDog {suffix}",
            NormalizedCallName = $"aliasdog {suffix}",
            SizeCategory = SizeCategory.M
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var alias = new DogAlias
        {
            AliasName = $"dog alias {suffix}",
            CanonicalDogId = dog.Id,
            AliasType = DogAliasType.CallName,
            Source = AliasSource.Import,
            CreatedAt = DateTime.UtcNow
        };
        context.DogAliases.Add(alias);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogAliasRepository(queryContext);
        var result = await repo.FindByAliasNameAndTypeAsync($"dog alias {suffix}", DogAliasType.CallName);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CanonicalDogId, Is.EqualTo(dog.Id));
    }

    [Test]
    public async Task FindByAliasNameAndTypeAsync_WrongType_ReturnsNull()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"TypeDog {suffix}",
            NormalizedCallName = $"typedog {suffix}",
            SizeCategory = SizeCategory.I
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        var alias = new DogAlias
        {
            AliasName = $"type alias {suffix}",
            CanonicalDogId = dog.Id,
            AliasType = DogAliasType.CallName,
            Source = AliasSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
        context.DogAliases.Add(alias);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogAliasRepository(queryContext);
        var result = await repo.FindByAliasNameAndTypeAsync($"type alias {suffix}", DogAliasType.RegisteredName);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FindByAliasNameAndTypeAsync_NonExistent_ReturnsNull()
    {
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogAliasRepository(queryContext);

        var result = await repo.FindByAliasNameAndTypeAsync("nonexistent-xyz", DogAliasType.CallName);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByDogIdAsync_MultipleAliases_ReturnsAll()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"MultiAlias Dog {suffix}",
            NormalizedCallName = $"multialias dog {suffix}",
            SizeCategory = SizeCategory.L
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        context.DogAliases.AddRange(
            new DogAlias
            {
                AliasName = $"call alias {suffix}",
                CanonicalDogId = dog.Id,
                AliasType = DogAliasType.CallName,
                Source = AliasSource.Import,
                CreatedAt = DateTime.UtcNow
            },
            new DogAlias
            {
                AliasName = $"reg alias {suffix}",
                CanonicalDogId = dog.Id,
                AliasType = DogAliasType.RegisteredName,
                Source = AliasSource.Manual,
                CreatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogAliasRepository(queryContext);
        var result = await repo.GetByDogIdAsync(dog.Id);

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task CreateAsync_ValidAlias_Persists()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"CreateAlias Dog {suffix}",
            NormalizedCallName = $"createalias dog {suffix}",
            SizeCategory = SizeCategory.S
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        // Act
        await using var repoContext = DatabaseFixture.CreateContext();
        var repo = new DogAliasRepository(repoContext);
        var alias = new DogAlias
        {
            AliasName = $"new dog alias {suffix}",
            CanonicalDogId = dog.Id,
            AliasType = DogAliasType.RegisteredName,
            Source = AliasSource.FuzzyMatch,
            CreatedAt = DateTime.UtcNow
        };
        await repo.CreateAsync(alias);

        // Assert
        Assert.That(alias.Id, Is.GreaterThan(0));

        await using var verifyContext = DatabaseFixture.CreateContext();
        var persisted = await verifyContext.DogAliases.FindAsync(alias.Id);
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.AliasType, Is.EqualTo(DogAliasType.RegisteredName));
    }
}
