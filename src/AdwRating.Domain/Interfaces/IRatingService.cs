namespace AdwRating.Domain.Interfaces;

public interface IRatingService
{
    Task RecalculateAllAsync();
}
