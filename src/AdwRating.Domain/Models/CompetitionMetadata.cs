namespace AdwRating.Domain.Models;

public record CompetitionMetadata(
    string Name,
    DateOnly Date,
    DateOnly? EndDate,
    string? Country,
    string? Location,
    int Tier,
    string? Organization
);
