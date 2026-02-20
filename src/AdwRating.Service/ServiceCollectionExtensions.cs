using AdwRating.Domain.Interfaces;
using AdwRating.Service.Rating;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Service;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IIdentityResolutionService, IdentityResolutionService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IMergeService, MergeService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IRankingService, RankingService>();
        services.AddScoped<ITeamProfileService, TeamProfileService>();
        services.AddScoped<ISearchService, SearchService>();
        return services;
    }
}
