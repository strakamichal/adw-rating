namespace AdwRating.Domain.Interfaces;

public interface IDatabaseInitializer
{
    /// <summary>
    /// Creates login, database, and user if an admin connection string is available.
    /// No-op when admin connection is not configured.
    /// </summary>
    Task BootstrapAsync();

    Task MigrateAsync();
}
