using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Service;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IIdentityResolutionService, IdentityResolutionService>();
        services.AddScoped<IImportService, ImportService>();
        return services;
    }
}
