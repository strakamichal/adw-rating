using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Interfaces;

public interface IIdentityResolutionService
{
    Task<Handler> ResolveHandlerAsync(string rawName, string country);
    Task<Dog> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size);
    Task<Team> ResolveTeamAsync(int handlerId, int dogId);
}
