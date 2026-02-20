using System.Text.Json;
using System.Text.Json.Serialization;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using AdwRating.Service;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Database + Services
var connectionString = Environment.GetEnvironmentVariable("ADW_RATING_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string not found. Set ADW_RATING_CONNECTION or ConnectionStrings:DefaultConnection.");

// Bootstrap: create login, database, and user if admin connection is provided
var adminConnectionString = Environment.GetEnvironmentVariable("ADW_RATING_ADMIN_CONNECTION");
if (!string.IsNullOrEmpty(adminConnectionString))
{
    var appConnBuilder = new SqlConnectionStringBuilder(connectionString);
    var dbName = appConnBuilder.InitialCatalog;
    var appLogin = appConnBuilder.UserID;
    var appPassword = appConnBuilder.Password;

    await using var conn = new SqlConnection(adminConnectionString);
    await conn.OpenAsync();

    // Create login if not exists
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @login)
                CREATE LOGIN [{appLogin}] WITH PASSWORD = @password;
            """;
        cmd.Parameters.AddWithValue("@login", appLogin);
        cmd.Parameters.AddWithValue("@password", appPassword);
        await cmd.ExecuteNonQueryAsync();
    }

    // Create database if not exists
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = @dbName)
                CREATE DATABASE [{dbName}];
            """;
        cmd.Parameters.AddWithValue("@dbName", dbName);
        await cmd.ExecuteNonQueryAsync();
    }

    // Create user and grant db_owner
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"""
            USE [{dbName}];
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @login)
            BEGIN
                CREATE USER [{appLogin}] FOR LOGIN [{appLogin}];
                ALTER ROLE db_owner ADD MEMBER [{appLogin}];
            END
            """;
        cmd.Parameters.AddWithValue("@login", appLogin);
        await cmd.ExecuteNonQueryAsync();
    }
}

builder.Services.AddDataMssql(connectionString);
builder.Services.AddServices();

// Controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// CORS — allow any origin for MVP
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Run database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbInit = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await dbInit.MigrateAsync();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Global exception handler
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.ContentType = "application/problem+json";
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;

        var statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                400 => "Bad Request",
                404 => "Not Found",
                _ => "Internal Server Error"
            },
            Detail = app.Environment.IsDevelopment() ? exception?.Message : null
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseCors();

app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
