using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Models;

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
