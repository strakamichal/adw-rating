using AdwRating.Domain.Models;

namespace AdwRating.Domain.Interfaces;

public interface IImportService
{
    Task<ImportResult> ImportCompetitionAsync(string filePath, string competitionSlug, CompetitionMetadata metadata);
}
