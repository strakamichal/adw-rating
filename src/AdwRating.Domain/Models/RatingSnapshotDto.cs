namespace AdwRating.Domain.Models;

/// <summary>
/// Lightweight projection of RatingSnapshot for API responses and ApiClient deserialization.
/// Excludes navigation properties and internal IDs.
/// </summary>
public record RatingSnapshotDto(
    DateOnly Date,
    float Mu,
    float Sigma,
    float Rating
);
