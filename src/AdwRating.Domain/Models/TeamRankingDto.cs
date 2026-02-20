using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Models;

public record TeamRankingDto(
    int Id,
    string Slug,
    string HandlerName,
    string HandlerCountry,
    string DogCallName,
    string? DogRegisteredName,
    SizeCategory SizeCategory,
    float Rating,
    float Sigma,
    int Rank,
    int? PrevRank,
    int RunCount,
    int FinishedRunCount,
    int Top3RunCount,
    bool IsProvisional,
    TierLabel? TierLabel
);
