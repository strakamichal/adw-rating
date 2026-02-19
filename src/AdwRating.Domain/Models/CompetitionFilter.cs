namespace AdwRating.Domain.Models;

public record CompetitionFilter(
    int? Year,
    int? Tier,
    string? Country,
    string? Search,
    int Page = 1,
    int PageSize = 20
);
