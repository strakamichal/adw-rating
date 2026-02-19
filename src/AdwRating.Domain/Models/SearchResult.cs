namespace AdwRating.Domain.Models;

public record SearchResult(
    string Type,
    string Slug,
    string DisplayName,
    string? Subtitle
);
