using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Models;

// Stubs for types being created by Agent A in parallel.
// These will be replaced when branches merge.

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record RankingFilter(
    SizeCategory Size,
    string? Country,
    string? Search,
    int Page = 1,
    int PageSize = 50
);

public record CompetitionFilter(
    int? Year,
    int? Tier,
    string? Country,
    string? Search,
    int Page = 1,
    int PageSize = 20
);

public record ImportResult(
    bool Success,
    int RowCount,
    int NewHandlers,
    int NewDogs,
    int NewTeams,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);

public record CompetitionMetadata(
    string Name,
    DateOnly Date,
    DateOnly? EndDate,
    string? Country,
    string? Location,
    int Tier,
    string? Organization
);

public record RankingSummary(
    int QualifiedTeams,
    int Competitions,
    int Runs
);

public record SearchResult(
    string Type,
    string Slug,
    string DisplayName,
    string? Subtitle
);

public record TeamDetailDto(
    int Id,
    string Slug,
    string HandlerName,
    string HandlerSlug,
    string HandlerCountry,
    string DogCallName,
    string? DogRegisteredName,
    string? DogBreed,
    SizeCategory SizeCategory,
    float Rating,
    float Sigma,
    float PrevRating,
    float PeakRating,
    int RunCount,
    int FinishedRunCount,
    int Top3RunCount,
    bool IsActive,
    bool IsProvisional,
    TierLabel? TierLabel,
    float FinishedPct,
    float Top3Pct,
    float? AvgRank
);

public record TeamResultDto(
    string CompetitionSlug,
    string CompetitionName,
    DateOnly Date,
    SizeCategory SizeCategory,
    Discipline Discipline,
    bool IsTeamRound,
    int? Rank,
    int? Faults,
    float? TimeFaults,
    float? Time,
    float? Speed,
    bool Eliminated
);
