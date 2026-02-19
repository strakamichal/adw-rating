namespace AdwRating.Domain.Entities;

public class RunResult
{
    public int Id { get; set; }
    public int RunId { get; set; }
    public int TeamId { get; set; }

    public int? Rank { get; set; }
    public int? Faults { get; set; }
    public int? Refusals { get; set; }
    public float? TimeFaults { get; set; }
    public float? TotalFaults { get; set; }
    public float? Time { get; set; }
    public float? Speed { get; set; }
    public bool Eliminated { get; set; }
    public int? StartNo { get; set; }

    // Navigation properties
    public Run Run { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
