using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Models;

public record TeamRankingDto(
    int Id,
    string Slug,
    string HandlerName,
    string HandlerCountry,
    string DogCallName,
    SizeCategory SizeCategory,
    float Rating,
    float Sigma,
    int Rank,
    int? PrevRank,
    int RunCount,
    int Top3RunCount,
    bool IsProvisional,
    TierLabel? TierLabel
);
