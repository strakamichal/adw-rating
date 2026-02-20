using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Interfaces;

public interface IIdentityResolutionService
{
    Task<(Handler Handler, bool IsNew)> ResolveHandlerAsync(string rawName, string country);
    Task<(Dog Dog, bool IsNew)> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size, int handlerId);
    Task<(Team Team, bool IsNew)> ResolveTeamAsync(int handlerId, int dogId);
}
