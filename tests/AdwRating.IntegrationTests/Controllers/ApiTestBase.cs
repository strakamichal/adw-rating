using System.Text.Json;
using System.Text.Json.Serialization;
using AdwRating.Data.Mssql;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.IntegrationTests.Controllers;

public abstract class ApiTestBase
{
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [OneTimeSetUp]
    public void ApiSetUp()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext registration and re-add with Testcontainers connection
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseSqlServer(DatabaseFixture.ConnectionString));
                });
            });

        Client = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public void ApiTearDown()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    protected static AppDbContext CreateContext() => DatabaseFixture.CreateContext();

    protected async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }
}
