using AdwRating.Domain.Entities;

namespace AdwRating.IntegrationTests.Entities;

[Collection("Database")]
public class HandlerPersistenceTests
{
    private readonly DatabaseFixture _fixture;

    public HandlerPersistenceTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Handler_CanBeInsertedAndRetrieved()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var handler = new Handler
        {
            Name = "John Smith",
            NormalizedName = "john smith",
            Country = "GBR",
            Slug = "john-smith"
        };

        // Act
        context.Handlers.Add(handler);
        await context.SaveChangesAsync();

        // Assert
        await using var readContext = _fixture.CreateContext();
        var retrieved = await readContext.Handlers.FindAsync(handler.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("John Smith", retrieved.Name);
        Assert.Equal("john smith", retrieved.NormalizedName);
        Assert.Equal("GBR", retrieved.Country);
    }
}
