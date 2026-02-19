using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.IntegrationTests.Repositories;

[TestFixture]
public class RatingConfigurationRepositoryTests
{
    private static RatingConfiguration CreateDefaultConfig() => new()
    {
        CreatedAt = DateTime.UtcNow,
        IsActive = true,
        Mu0 = 25.0f,
        Sigma0 = 8.333f,
        LiveWindowDays = 730,
        MinRunsForLiveRanking = 6,
        MinFieldSize = 5,
        MajorEventWeight = 1.5f,
        SigmaDecay = 0.98f,
        SigmaMin = 2.0f,
        DisplayBase = 1500.0f,
        DisplayScale = 40.0f,
        RatingSigmaMultiplier = 3.0f,
        PodiumBoostBase = 0.5f,
        PodiumBoostRange = 0.25f,
        PodiumBoostTarget = 0.8f,
        ProvisionalSigmaThreshold = 6.0f,
        NormTargetMean = 1500.0f,
        NormTargetStd = 200.0f,
        EliteTopPercent = 1.0f,
        ChampionTopPercent = 5.0f,
        ExpertTopPercent = 15.0f,
        CountryTopN = 20,
        MinTeamsForCountryRanking = 10
    };

    [Test]
    public async Task GetActiveAsync_WithActiveConfig_ReturnsConfig()
    {
        await using var context = DatabaseFixture.CreateContext();
        var config = CreateDefaultConfig();
        context.RatingConfigurations.Add(config);
        await context.SaveChangesAsync();

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RatingConfigurationRepository(queryContext);
        var result = await repo.GetActiveAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsActive, Is.True);
        Assert.That(result.Mu0, Is.EqualTo(25.0f));
    }

    [Test]
    public async Task GetActiveAsync_NoActiveConfig_ThrowsInvalidOperationException()
    {
        // This test relies on no active configs existing.
        // We deactivate all configs first, then verify the exception.
        await using var context = DatabaseFixture.CreateContext();
        await context.RatingConfigurations
            .Where(c => c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false));

        await using var queryContext = DatabaseFixture.CreateContext();
        var repo = new RatingConfigurationRepository(queryContext);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await repo.GetActiveAsync());

        // Restore: create an active config so other tests aren't affected
        await using var restoreContext = DatabaseFixture.CreateContext();
        restoreContext.RatingConfigurations.Add(CreateDefaultConfig());
        await restoreContext.SaveChangesAsync();
    }

    [Test]
    public async Task CreateAsync_DeactivatesPreviousAndActivatesNew()
    {
        await using var context = DatabaseFixture.CreateContext();
        // Ensure there's an active config
        var existing = CreateDefaultConfig();
        context.RatingConfigurations.Add(existing);
        await context.SaveChangesAsync();
        var existingId = existing.Id;

        await using var createContext = DatabaseFixture.CreateContext();
        var repo = new RatingConfigurationRepository(createContext);
        var newConfig = CreateDefaultConfig();
        newConfig.Mu0 = 30.0f; // Different value to distinguish
        newConfig.IsActive = false; // Should be set to true by CreateAsync
        await repo.CreateAsync(newConfig);

        await using var verifyContext = DatabaseFixture.CreateContext();
        var oldConfig = await verifyContext.RatingConfigurations.FindAsync(existingId);
        Assert.That(oldConfig!.IsActive, Is.False);

        var activeConfig = await verifyContext.RatingConfigurations
            .FirstOrDefaultAsync(c => c.IsActive);
        Assert.That(activeConfig, Is.Not.Null);
        Assert.That(activeConfig!.Mu0, Is.EqualTo(30.0f));
    }
}
