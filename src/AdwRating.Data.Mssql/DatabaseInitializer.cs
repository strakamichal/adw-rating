using AdwRating.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace AdwRating.Data.Mssql;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly AppDbContext _context;

    public DatabaseInitializer(AppDbContext context)
    {
        _context = context;
    }

    public async Task BootstrapAsync()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("ADW_RATING_ADMIN_CONNECTION");
        if (string.IsNullOrEmpty(adminConnectionString))
            return;

        var appConnectionString = _context.Database.GetConnectionString()!;
        var appConnBuilder = new SqlConnectionStringBuilder(appConnectionString);
        var dbName = appConnBuilder.InitialCatalog;
        var appLogin = appConnBuilder.UserID;
        var appPassword = appConnBuilder.Password;

        await using var conn = new SqlConnection(adminConnectionString);
        await conn.OpenAsync();

        // DDL statements (CREATE LOGIN/DATABASE/USER) don't support parameterized queries.
        // Values come from our own environment variables, not user input.
        var safePassword = appPassword.Replace("'", "''");
        var safeLogin = appLogin.Replace("]", "]]");
        var safeDbName = dbName.Replace("]", "]]");

        // Create login if not exists
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = '{safeLogin}')
                    CREATE LOGIN [{safeLogin}] WITH PASSWORD = '{safePassword}';
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Create database if not exists
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = '{safeDbName}')
                    CREATE DATABASE [{safeDbName}];
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Create user and grant db_owner
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                USE [{safeDbName}];
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{safeLogin}')
                BEGIN
                    CREATE USER [{safeLogin}] FOR LOGIN [{safeLogin}];
                    ALTER ROLE db_owner ADD MEMBER [{safeLogin}];
                END
                """;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task MigrateAsync()
    {
        await _context.Database.MigrateAsync();
    }
}
