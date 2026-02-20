namespace AdwRating.Domain.Models;

public record RunSummaryDto(
    int Id,
    string RoundKey,
    DateOnly Date,
    string SizeCategory,
    string Discipline,
    bool IsTeamRound,
    bool IsExcluded,
    int ResultCount,
    int FinishedCount,
    int EliminatedCount
);
