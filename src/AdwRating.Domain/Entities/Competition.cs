namespace AdwRating.Domain.Entities;

public class Competition
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Country { get; set; }
    public string? Location { get; set; }
    public int Tier { get; set; }
    public string? Organization { get; set; }

    // Navigation
    public ICollection<Run> Runs { get; set; } = new List<Run>();
}
