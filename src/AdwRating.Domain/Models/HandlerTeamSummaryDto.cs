using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Models;

public record HandlerTeamSummaryDto(
    string TeamSlug,
    string DogCallName,
    string? DogBreed,
    SizeCategory SizeCategory,
    float Rating,
    float PeakRating,
    int RunCount,
    bool IsActive,
    bool IsProvisional,
    TierLabel? TierLabel
);
