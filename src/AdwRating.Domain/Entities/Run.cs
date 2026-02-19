using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Entities;

public class Run
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public DateOnly Date { get; set; }
    public int RunNumber { get; set; }
    public string RoundKey { get; set; } = string.Empty;

    public SizeCategory SizeCategory { get; set; }
    public Discipline Discipline { get; set; }
    public bool IsTeamRound { get; set; }

    public string? Judge { get; set; }
    public float? Sct { get; set; }
    public float? Mct { get; set; }
    public float? CourseLength { get; set; }

    public string? OriginalSizeCategory { get; set; }

    // Navigation properties
    public Competition Competition { get; set; } = null!;
    public ICollection<RunResult> RunResults { get; set; } = new List<RunResult>();
}
