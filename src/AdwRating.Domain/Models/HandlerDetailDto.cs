namespace AdwRating.Domain.Models;

public record HandlerDetailDto(
    int Id,
    string Slug,
    string Name,
    string Country,
    IReadOnlyList<HandlerTeamSummaryDto> Teams
);
