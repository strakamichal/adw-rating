using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Entities;

public class ImportLog
{
    public int Id { get; set; }
    public int? CompetitionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public ImportStatus Status { get; set; }
    public int RowCount { get; set; }
    public int NewHandlersCount { get; set; }
    public int NewDogsCount { get; set; }
    public int NewTeamsCount { get; set; }
    public string? Errors { get; set; }
    public string? Warnings { get; set; }

    // Navigation properties
    public Competition? Competition { get; set; }
}
