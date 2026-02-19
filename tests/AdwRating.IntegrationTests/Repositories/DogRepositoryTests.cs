using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class DogRepositoryTests
{
    [Test]
    public async Task GetByIdAsync_ExistingDog_ReturnsDog()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"Buddy {suffix}",
            NormalizedCallName = $"buddy {suffix}",
            SizeCategory = SizeCategory.L
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogRepository(queryContext);
        var result = await repo.GetByIdAsync(dog.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CallName, Is.EqualTo(dog.CallName));
        Assert.That(result.SizeCategory, Is.EqualTo(SizeCategory.L));
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogRepository(queryContext);

        var result = await repo.GetByIdAsync(999999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FindByNormalizedNameAndSizeAsync_Match_ReturnsDog()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"FindDog {suffix}",
            NormalizedCallName = $"finddog {suffix}",
            SizeCategory = SizeCategory.S
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogRepository(queryContext);
        var result = await repo.FindByNormalizedNameAndSizeAsync($"finddog {suffix}", SizeCategory.S);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(dog.Id));
    }

    [Test]
    public async Task FindByNormalizedNameAndSizeAsync_WrongSize_ReturnsNull()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"SizeDog {suffix}",
            NormalizedCallName = $"sizedog {suffix}",
            SizeCategory = SizeCategory.M
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogRepository(queryContext);
        var result = await repo.FindByNormalizedNameAndSizeAsync($"sizedog {suffix}", SizeCategory.L);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task SearchAsync_MatchingQuery_ReturnsResults()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        context.Dogs.AddRange(
            new Dog
            {
                CallName = $"SearchDog Alpha {suffix}",
                NormalizedCallName = $"searchdog alpha {suffix}",
                SizeCategory = SizeCategory.M
            },
            new Dog
            {
                CallName = $"SearchDog Beta {suffix}",
                NormalizedCallName = $"searchdog beta {suffix}",
                SizeCategory = SizeCategory.L
            }
        );
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new DogRepository(queryContext);
        var result = await repo.SearchAsync($"searchdog", 10);

        // Assert
        Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task CreateAsync_ValidDog_PersistsAndReturnsWithId()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var repo = new DogRepository(context);
        var dog = new Dog
        {
            CallName = $"NewDog {suffix}",
            NormalizedCallName = $"newdog {suffix}",
            RegisteredName = $"Registered NewDog {suffix}",
            Breed = "Border Collie",
            SizeCategory = SizeCategory.L
        };

        // Act
        var result = await repo.CreateAsync(dog);

        // Assert
        Assert.That(result.Id, Is.GreaterThan(0));

        await using var verifyContext = DatabaseFixture.CreateContext();
        var persisted = await verifyContext.Dogs.FindAsync(result.Id);
        Assert.That(persisted, Is.Not.Null);
        Assert.That(persisted!.Breed, Is.EqualTo("Border Collie"));
    }

    [Test]
    public async Task UpdateAsync_ModifiedDog_PersistsChanges()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();
        var dog = new Dog
        {
            CallName = $"UpdateDog {suffix}",
            NormalizedCallName = $"updatedog {suffix}",
            SizeCategory = SizeCategory.M
        };
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();

        // Act
        await using var updateContext = DatabaseFixture.CreateContext();
        var loaded = await updateContext.Dogs.FindAsync(dog.Id);
        loaded!.Breed = "Sheltie";
        var repo = new DogRepository(updateContext);
        await repo.UpdateAsync(loaded);

        // Assert
        await using var verifyContext = DatabaseFixture.CreateContext();
        var updated = await verifyContext.Dogs.FindAsync(dog.Id);
        Assert.That(updated!.Breed, Is.EqualTo("Sheltie"));
    }

    [Test]
    public async Task MergeAsync_ReassignsTeamsAndAliasesAndDeletesSource()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var context = DatabaseFixture.CreateContext();

        var sourceDog = new Dog
        {
            CallName = $"SourceDog {suffix}",
            NormalizedCallName = $"sourcedog {suffix}",
            SizeCategory = SizeCategory.S
        };
        var targetDog = new Dog
        {
            CallName = $"TargetDog {suffix}",
            NormalizedCallName = $"targetdog {suffix}",
            SizeCategory = SizeCategory.S
        };
        context.Dogs.AddRange(sourceDog, targetDog);
        await context.SaveChangesAsync();

        var handler = new Handler
        {
            Name = $"DogMerge Handler {suffix}",
            NormalizedName = $"dogmerge handler {suffix}",
            Country = "CZE",
            Slug = $"dogmerge-handler-{suffix}"
        };
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = sourceDog.Id,
            Slug = $"dogmerge-team-{suffix}",
            Mu = 25.0f,
            Sigma = 8.33f,
            Rating = 0.0f
        };
        context.Teams.Add(team);

        var alias = new DogAlias
        {
            AliasName = $"source dog alias {suffix}",
            CanonicalDogId = sourceDog.Id,
            AliasType = DogAliasType.CallName,
            Source = AliasSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
        context.DogAliases.Add(alias);
        await context.SaveChangesAsync();

        // Act
        await using var mergeContext = DatabaseFixture.CreateContext();
        var repo = new DogRepository(mergeContext);
        await repo.MergeAsync(sourceDog.Id, targetDog.Id);

        // Assert
        await using var verifyContext = DatabaseFixture.CreateContext();

        // Source dog deleted
        var deletedDog = await verifyContext.Dogs.FindAsync(sourceDog.Id);
        Assert.That(deletedDog, Is.Null);

        // Team reassigned to target
        var reassignedTeam = await verifyContext.Teams.FindAsync(team.Id);
        Assert.That(reassignedTeam, Is.Not.Null);
        Assert.That(reassignedTeam!.DogId, Is.EqualTo(targetDog.Id));

        // Alias reassigned to target
        var reassignedAlias = await verifyContext.DogAliases.FindAsync(alias.Id);
        Assert.That(reassignedAlias, Is.Not.Null);
        Assert.That(reassignedAlias!.CanonicalDogId, Is.EqualTo(targetDog.Id));
    }
}
