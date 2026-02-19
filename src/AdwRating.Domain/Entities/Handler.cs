namespace AdwRating.Domain.Entities;

public class Handler
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
