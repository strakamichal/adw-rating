namespace AdwRating.Domain.Models;

public record CompetitionDetailDto(
    int Id,
    string Slug,
    string Name,
    DateOnly Date,
    DateOnly? EndDate,
    string? Country,
    string? Location,
    int Tier,
    string? Organization,
    int RunCount,
    int ParticipantCount
);
