using AdwRating.Data.Mssql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.MsSql;

namespace AdwRating.IntegrationTests;

[SetUpFixture]
public class DatabaseFixture
{
    private static MsSqlContainer? _container;
    public static string ConnectionString { get; private set; } = string.Empty;

    [OneTimeSetUp]
    public async Task GlobalSetUp()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task GlobalTearDown()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new AppDbContext(options);
    }
}
