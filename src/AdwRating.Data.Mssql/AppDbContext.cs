using Microsoft.EntityFrameworkCore;

namespace AdwRating.Data.Mssql;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
