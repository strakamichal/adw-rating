using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Models;

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
    bool Eliminated,
    bool IsExcluded
);
