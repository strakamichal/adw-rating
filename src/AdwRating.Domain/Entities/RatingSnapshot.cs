namespace AdwRating.Domain.Entities;

public class RatingSnapshot
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int RunResultId { get; set; }
    public int CompetitionId { get; set; }
    public DateOnly Date { get; set; }
    public float Mu { get; set; }
    public float Sigma { get; set; }
    public float Rating { get; set; }

    // Navigation properties
    public Team Team { get; set; } = null!;
    public RunResult RunResult { get; set; } = null!;
    public Competition Competition { get; set; } = null!;
}
