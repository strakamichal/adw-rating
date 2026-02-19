namespace AdwRating.Domain.Interfaces;

public interface IMergeService
{
    Task MergeHandlersAsync(int sourceHandlerId, int targetHandlerId);
    Task MergeDogsAsync(int sourceDogId, int targetDogId);
}
