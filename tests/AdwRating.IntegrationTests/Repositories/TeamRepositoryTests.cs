using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class TeamRepositoryTests
{
    private static async Task<(Handler handler, Dog dog)> CreateHandlerAndDogAsync(
        string suffix, SizeCategory size = SizeCategory.M, string country = "CZE")
    {
        await using var context = DatabaseFixture.CreateContext();
        var handler = new Handler
        {
            Name = $"Handler {suffix}",
            NormalizedName = $"handler {suffix}",
            Country = country,
            Slug = $"handler-{suffix}"
        };
        var dog = new Dog
        {
            CallName = $"Dog {suffix}",
            NormalizedCallName = $"dog {suffix}",
            SizeCategory = size
        };
        context.Handlers.Add(handler);
        context.Dogs.Add(dog);
        await context.SaveChangesAsync();
        return (handler, dog);
    }

    [Test]
    public async Task GetByIdAsync_ExistingTeam_ReturnsTeamWithNavProperties()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"getid-{suffix}");

        await using var context = DatabaseFixture.CreateContext();
        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"team-getid-{suffix}",
            Mu = 25.0f,
            Sigma = 8.33f,
            Rating = 0.01f,
            IsActive = true
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var result = await repo.GetByIdAsync(team.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Handler, Is.Not.Null);
        Assert.That(result.Dog, Is.Not.Null);
        Assert.That(result.Handler.Name, Is.EqualTo($"Handler getid-{suffix}"));
    }

    [Test]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);

        var result = await repo.GetByIdAsync(999999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetBySlugAsync_ExistingSlug_ReturnsTeam()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"slug-{suffix}");

        await using var context = DatabaseFixture.CreateContext();
        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"team-slug-{suffix}",
            Mu = 25.0f,
            Sigma = 8.33f,
            Rating = 0.0f,
            IsActive = true
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var result = await repo.GetBySlugAsync($"team-slug-{suffix}");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Slug, Is.EqualTo($"team-slug-{suffix}"));
    }

    [Test]
    public async Task GetByHandlerAndDogAsync_ExistingPair_ReturnsTeam()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"pair-{suffix}");

        await using var context = DatabaseFixture.CreateContext();
        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"team-pair-{suffix}",
            Mu = 25.0f,
            Sigma = 8.33f,
            Rating = 0.0f,
            IsActive = true
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var result = await repo.GetByHandlerAndDogAsync(handler.Id, dog.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(team.Id));
    }

    [Test]
    public async Task GetByHandlerAndDogAsync_NonExistentPair_ReturnsNull()
    {
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);

        var result = await repo.GetByHandlerAndDogAsync(999998, 999999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByHandlerIdAsync_MultipleTeams_ReturnsAll()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog1) = await CreateHandlerAndDogAsync($"multi1-{suffix}");

        await using var dogContext = DatabaseFixture.CreateContext();
        var dog2 = new Dog
        {
            CallName = $"Dog multi2-{suffix}",
            NormalizedCallName = $"dog multi2-{suffix}",
            SizeCategory = SizeCategory.S
        };
        dogContext.Dogs.Add(dog2);
        await dogContext.SaveChangesAsync();

        await using var context = DatabaseFixture.CreateContext();
        context.Teams.AddRange(
            new Team
            {
                HandlerId = handler.Id,
                DogId = dog1.Id,
                Slug = $"team-multi1-{suffix}",
                Mu = 25.0f, Sigma = 8.33f, Rating = 0.0f, IsActive = true
            },
            new Team
            {
                HandlerId = handler.Id,
                DogId = dog2.Id,
                Slug = $"team-multi2-{suffix}",
                Mu = 25.0f, Sigma = 8.33f, Rating = 0.0f, IsActive = true
            }
        );
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var result = await repo.GetByHandlerIdAsync(handler.Id);

        // Assert
        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task CreateAsync_ValidTeam_PersistsAndReturnsWithId()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"create-{suffix}");

        await using var context = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(context);
        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"team-create-{suffix}",
            Mu = 25.0f,
            Sigma = 8.33f,
            Rating = 0.01f,
            IsActive = true
        };

        // Act
        var result = await repo.CreateAsync(team);

        // Assert
        Assert.That(result.Id, Is.GreaterThan(0));

        await using var verifyContext = DatabaseFixture.CreateContext();
        var persisted = await verifyContext.Teams.FindAsync(result.Id);
        Assert.That(persisted, Is.Not.Null);
    }

    [Test]
    public async Task UpdateBatchAsync_ModifiedTeams_PersistsChanges()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"batch-{suffix}");

        await using var context = DatabaseFixture.CreateContext();
        var team = new Team
        {
            HandlerId = handler.Id,
            DogId = dog.Id,
            Slug = $"team-batch-{suffix}",
            Mu = 25.0f,
            Sigma = 8.33f,
            Rating = 0.0f,
            IsActive = true
        };
        context.Teams.Add(team);
        await context.SaveChangesAsync();

        // Act
        await using var updateContext = DatabaseFixture.CreateContext();
        var loaded = await updateContext.Teams.FindAsync(team.Id);
        loaded!.Rating = 42.5f;
        loaded.Mu = 30.0f;

        var repo = new TeamRepository(updateContext);
        await repo.UpdateBatchAsync(new[] { loaded });

        // Assert
        await using var verifyContext = DatabaseFixture.CreateContext();
        var updated = await verifyContext.Teams.FindAsync(team.Id);
        Assert.That(updated!.Rating, Is.EqualTo(42.5f));
        Assert.That(updated.Mu, Is.EqualTo(30.0f));
    }

    [Test]
    public async Task GetAllAsync_ReturnsAllTeams()
    {
        // We just verify it returns a non-null list and includes nav properties
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);

        var result = await repo.GetAllAsync();

        Assert.That(result, Is.Not.Null);
        // All returned teams should have Handler and Dog loaded
        foreach (var team in result)
        {
            Assert.That(team.Handler, Is.Not.Null);
            Assert.That(team.Dog, Is.Not.Null);
        }
    }

    [Test]
    public async Task GetRankedTeamsAsync_FiltersBySize_ReturnsCorrectResults()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler1, dogL) = await CreateHandlerAndDogAsync($"rank-l-{suffix}", SizeCategory.L);
        var (handler2, dogS) = await CreateHandlerAndDogAsync($"rank-s-{suffix}", SizeCategory.S);

        await using var context = DatabaseFixture.CreateContext();
        context.Teams.AddRange(
            new Team
            {
                HandlerId = handler1.Id, DogId = dogL.Id,
                Slug = $"team-rank-l-{suffix}",
                Mu = 30.0f, Sigma = 5.0f, Rating = 15.0f,
                IsActive = true
            },
            new Team
            {
                HandlerId = handler2.Id, DogId = dogS.Id,
                Slug = $"team-rank-s-{suffix}",
                Mu = 28.0f, Sigma = 6.0f, Rating = 10.0f,
                IsActive = true
            }
        );
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var filter = new RankingFilter(SizeCategory.L, null, null);
        var result = await repo.GetRankedTeamsAsync(filter);

        // Assert
        Assert.That(result.Items.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.Items.All(t => t.Dog.SizeCategory == SizeCategory.L), Is.True);
    }

    [Test]
    public async Task GetRankedTeamsAsync_FiltersByCountry_ReturnsCorrectResults()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handlerCze, dogCze) = await CreateHandlerAndDogAsync($"rank-cze-{suffix}", SizeCategory.I, "CZE");
        var (handlerUsa, dogUsa) = await CreateHandlerAndDogAsync($"rank-usa-{suffix}", SizeCategory.I, "USA");

        await using var context = DatabaseFixture.CreateContext();
        context.Teams.AddRange(
            new Team
            {
                HandlerId = handlerCze.Id, DogId = dogCze.Id,
                Slug = $"team-rank-cze-{suffix}",
                Mu = 30.0f, Sigma = 5.0f, Rating = 15.0f,
                IsActive = true
            },
            new Team
            {
                HandlerId = handlerUsa.Id, DogId = dogUsa.Id,
                Slug = $"team-rank-usa-{suffix}",
                Mu = 28.0f, Sigma = 6.0f, Rating = 10.0f,
                IsActive = true
            }
        );
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var filter = new RankingFilter(SizeCategory.I, "CZE", null);
        var result = await repo.GetRankedTeamsAsync(filter);

        // Assert
        Assert.That(result.Items.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.Items.All(t => t.Handler.Country == "CZE"), Is.True);
    }

    [Test]
    public async Task GetRankedTeamsAsync_FiltersBySearch_ReturnsCorrectResults()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"searchrank-{suffix}", SizeCategory.M);

        await using var context = DatabaseFixture.CreateContext();
        context.Teams.Add(new Team
        {
            HandlerId = handler.Id, DogId = dog.Id,
            Slug = $"team-searchrank-{suffix}",
            Mu = 30.0f, Sigma = 5.0f, Rating = 15.0f,
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var filter = new RankingFilter(SizeCategory.M, null, $"searchrank-{suffix}");
        var result = await repo.GetRankedTeamsAsync(filter);

        // Assert
        Assert.That(result.Items.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task GetRankedTeamsAsync_ExcludesInactiveTeams()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"inactive-{suffix}", SizeCategory.L);

        await using var context = DatabaseFixture.CreateContext();
        context.Teams.Add(new Team
        {
            HandlerId = handler.Id, DogId = dog.Id,
            Slug = $"team-inactive-{suffix}",
            Mu = 30.0f, Sigma = 5.0f, Rating = 15.0f,
            IsActive = false
        });
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var filter = new RankingFilter(SizeCategory.L, null, $"inactive-{suffix}");
        var result = await repo.GetRankedTeamsAsync(filter);

        // Assert — the inactive team should not appear in ranked results
        Assert.That(result.Items.Any(t => t.Slug == $"team-inactive-{suffix}"), Is.False);
    }

    [Test]
    public async Task GetRankedTeamsAsync_OrdersByRatingDescending()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler1, dog1) = await CreateHandlerAndDogAsync($"order1-{suffix}", SizeCategory.S);
        var (handler2, dog2) = await CreateHandlerAndDogAsync($"order2-{suffix}", SizeCategory.S);
        var (handler3, dog3) = await CreateHandlerAndDogAsync($"order3-{suffix}", SizeCategory.S);

        await using var context = DatabaseFixture.CreateContext();
        context.Teams.AddRange(
            new Team
            {
                HandlerId = handler1.Id, DogId = dog1.Id,
                Slug = $"team-order1-{suffix}",
                Mu = 20.0f, Sigma = 5.0f, Rating = 5.0f,
                IsActive = true
            },
            new Team
            {
                HandlerId = handler2.Id, DogId = dog2.Id,
                Slug = $"team-order2-{suffix}",
                Mu = 40.0f, Sigma = 5.0f, Rating = 25.0f,
                IsActive = true
            },
            new Team
            {
                HandlerId = handler3.Id, DogId = dog3.Id,
                Slug = $"team-order3-{suffix}",
                Mu = 30.0f, Sigma = 5.0f, Rating = 15.0f,
                IsActive = true
            }
        );
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var filter = new RankingFilter(SizeCategory.S, null, null);
        var result = await repo.GetRankedTeamsAsync(filter);

        // Assert — results are ordered by Rating descending
        for (var i = 1; i < result.Items.Count; i++)
        {
            Assert.That(result.Items[i].Rating, Is.LessThanOrEqualTo(result.Items[i - 1].Rating));
        }
    }

    [Test]
    public async Task GetRankedTeamsAsync_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Create 3 teams
        for (var i = 0; i < 3; i++)
        {
            var (h, d) = await CreateHandlerAndDogAsync($"page{i}-{suffix}", SizeCategory.I);
            await using var ctx = DatabaseFixture.CreateContext();
            ctx.Teams.Add(new Team
            {
                HandlerId = h.Id, DogId = d.Id,
                Slug = $"team-page{i}-{suffix}",
                Mu = 30.0f - i, Sigma = 5.0f, Rating = 15.0f - i,
                IsActive = true
            });
            await ctx.SaveChangesAsync();
        }

        // Act — get page 1 with page size 2
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var filter = new RankingFilter(SizeCategory.I, null, $"page", Page: 1, PageSize: 2);
        var result = await repo.GetRankedTeamsAsync(filter);

        // Assert
        Assert.That(result.Items.Count, Is.LessThanOrEqualTo(2));
        Assert.That(result.Page, Is.EqualTo(1));
        Assert.That(result.PageSize, Is.EqualTo(2));
        Assert.That(result.TotalCount, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public async Task GetRankedTeamsAsync_IncludesNavigationProperties()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (handler, dog) = await CreateHandlerAndDogAsync($"nav-{suffix}", SizeCategory.M);

        await using var context = DatabaseFixture.CreateContext();
        context.Teams.Add(new Team
        {
            HandlerId = handler.Id, DogId = dog.Id,
            Slug = $"team-nav-{suffix}",
            Mu = 30.0f, Sigma = 5.0f, Rating = 15.0f,
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new TeamRepository(queryContext);
        var filter = new RankingFilter(SizeCategory.M, null, $"nav-{suffix}");
        var result = await repo.GetRankedTeamsAsync(filter);

        // Assert
        Assert.That(result.Items.Count, Is.GreaterThanOrEqualTo(1));
        var team = result.Items.First();
        Assert.That(team.Handler, Is.Not.Null);
        Assert.That(team.Dog, Is.Not.Null);
    }
}
