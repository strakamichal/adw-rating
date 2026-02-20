namespace AdwRating.Domain.Interfaces;

public interface IDatabaseInitializer
{
    Task EnsureCreatedAsync();
}
