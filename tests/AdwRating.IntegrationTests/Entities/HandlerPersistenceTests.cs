using AdwRating.Domain.Entities;

namespace AdwRating.IntegrationTests.Entities;

[TestFixture]
public class HandlerPersistenceTests
{
    [Test]
    public async Task Handler_CanBeInsertedAndRetrieved()
    {
        // Arrange
        await using var context = DatabaseFixture.CreateContext();
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
        await using var readContext = DatabaseFixture.CreateContext();
        var retrieved = await readContext.Handlers.FindAsync(handler.Id);
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved.Name, Is.EqualTo("John Smith"));
        Assert.That(retrieved.NormalizedName, Is.EqualTo("john smith"));
        Assert.That(retrieved.Country, Is.EqualTo("GBR"));
    }
}
