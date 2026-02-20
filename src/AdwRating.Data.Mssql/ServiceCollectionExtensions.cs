using AdwRating.Data.Mssql.Repositories;
using AdwRating.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Data.Mssql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataMssql(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IHandlerRepository, HandlerRepository>();
        services.AddScoped<IHandlerAliasRepository, HandlerAliasRepository>();
        services.AddScoped<IDogRepository, DogRepository>();
        services.AddScoped<IDogAliasRepository, DogAliasRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ICompetitionRepository, CompetitionRepository>();
        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IRunResultRepository, RunResultRepository>();
        services.AddScoped<IRatingSnapshotRepository, RatingSnapshotRepository>();
        services.AddScoped<IRatingConfigurationRepository, RatingConfigurationRepository>();
        services.AddScoped<IImportLogRepository, ImportLogRepository>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        return services;
    }
}
