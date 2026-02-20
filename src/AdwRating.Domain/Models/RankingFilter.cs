using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Models;

public record RankingFilter(
    SizeCategory? Size,
    string? Country,
    string? Search,
    int Page = 1,
    int PageSize = 50
);
