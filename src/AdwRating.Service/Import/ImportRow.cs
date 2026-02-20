namespace AdwRating.Service.Import;

public record ImportRow
{
    // Required fields
    public string RoundKey { get; init; } = string.Empty;
    public string? Date { get; init; }
    public string SizeCategory { get; init; } = string.Empty;
    public string Discipline { get; init; } = string.Empty;
    public string? IsTeamRound { get; init; }
    public string HandlerName { get; init; } = string.Empty;
    public string HandlerCountry { get; init; } = string.Empty;
    public string DogCallName { get; init; } = string.Empty;
    public string? Rank { get; init; }
    public string? Eliminated { get; init; }

    // Optional fields
    public string? DogRegisteredName { get; init; }
    public string? DogBreed { get; init; }
    public string? Faults { get; init; }
    public string? Refusals { get; init; }
    public string? TimeFaults { get; init; }
    public string? TotalFaults { get; init; }
    public string? Time { get; init; }
    public string? Speed { get; init; }
    public string? Judge { get; init; }
    public string? Sct { get; init; }
    public string? Mct { get; init; }
    public string? CourseLength { get; init; }
    public string? StartNo { get; init; }
    public string? RunNumber { get; init; }
}
