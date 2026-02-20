namespace AdwRating.Domain.Models;

public record ImportResult(
    bool Success,
    int RowCount,
    int NewHandlers,
    int NewDogs,
    int NewTeams,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);
