using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Entities;

public class Team
{
    public int Id { get; set; }
    public int HandlerId { get; set; }
    public int DogId { get; set; }
    public string Slug { get; set; } = string.Empty;

    public float Mu { get; set; }
    public float Sigma { get; set; }
    public float Rating { get; set; }

    public float PrevMu { get; set; }
    public float PrevSigma { get; set; }
    public float PrevRating { get; set; }

    public int RunCount { get; set; }
    public int FinishedRunCount { get; set; }
    public int Top3RunCount { get; set; }

    public bool IsActive { get; set; }
    public bool IsProvisional { get; set; }

    public TierLabel? TierLabel { get; set; }
    public float PeakRating { get; set; }

    // Navigation properties
    public Handler Handler { get; set; } = null!;
    public Dog Dog { get; set; } = null!;
    public ICollection<RunResult> RunResults { get; set; } = new List<RunResult>();
}
