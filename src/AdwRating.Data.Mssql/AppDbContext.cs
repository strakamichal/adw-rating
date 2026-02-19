using AdwRating.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Handler> Handlers => Set<Handler>();
    public DbSet<Dog> Dogs => Set<Dog>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<RunResult> RunResults => Set<RunResult>();
    public DbSet<HandlerAlias> HandlerAliases => Set<HandlerAlias>();
    public DbSet<DogAlias> DogAliases => Set<DogAlias>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();
    public DbSet<RatingSnapshot> RatingSnapshots => Set<RatingSnapshot>();
    public DbSet<RatingConfiguration> RatingConfigurations => Set<RatingConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
