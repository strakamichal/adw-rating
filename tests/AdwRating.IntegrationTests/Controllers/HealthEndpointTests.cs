using System.Net;
using AdwRating.Data.Mssql;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.IntegrationTests.Controllers;

[TestFixture]
public class HealthEndpointTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Use InMemory database for the health check test (no real DB needed)
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase("HealthTest"));
                });
            });

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("healthy"));
    }
}
