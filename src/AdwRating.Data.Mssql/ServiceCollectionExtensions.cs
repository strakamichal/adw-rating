using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Data.Mssql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataMssql(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repository implementations will be registered here in Phase 2
        // e.g., services.AddScoped<IHandlerRepository, HandlerRepository>();

        return services;
    }
}
