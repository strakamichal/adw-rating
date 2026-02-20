using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using AdwRating.Service.Import;
using Microsoft.Extensions.Logging;

namespace AdwRating.Service;

public class ImportService : IImportService
{
    private readonly ICompetitionRepository _competitionRepo;
    private readonly IRunRepository _runRepo;
    private readonly IRunResultRepository _runResultRepo;
    private readonly IImportLogRepository _importLogRepo;
    private readonly IIdentityResolutionService _identityService;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        ICompetitionRepository competitionRepo,
        IRunRepository runRepo,
        IRunResultRepository runResultRepo,
        IImportLogRepository importLogRepo,
        IIdentityResolutionService identityService,
        ILogger<ImportService> logger)
    {
        _competitionRepo = competitionRepo;
        _runRepo = runRepo;
        _runResultRepo = runResultRepo;
        _importLogRepo = importLogRepo;
        _identityService = identityService;
        _logger = logger;
    }

    public async Task<ImportResult> ImportCompetitionAsync(
        string filePath, string competitionSlug, CompetitionMetadata metadata)
    {
        var warnings = new List<string>();
        var fileName = Path.GetFileName(filePath);

        // 1. Parse CSV
        using var stream = File.OpenRead(filePath);
        var (rows, parseErrors, parseWarnings) = CsvResultParser.Parse(stream);
        warnings.AddRange(parseWarnings);

        // 2. If parse errors, write rejected ImportLog and return failure
        if (parseErrors.Count > 0)
        {
            await _importLogRepo.CreateAsync(new ImportLog
            {
                CompetitionId = null,
                FileName = fileName,
                ImportedAt = DateTime.UtcNow,
                Status = ImportStatus.Rejected,
                RowCount = rows.Count,
                NewHandlersCount = 0,
                NewDogsCount = 0,
                NewTeamsCount = 0,
                Errors = string.Join("\n", parseErrors),
                Warnings = null
            });
            return new ImportResult(false, rows.Count, 0, 0, 0, parseErrors, warnings);
        }

        // 3. Check duplicate slug
        var existing = await _competitionRepo.GetBySlugAsync(competitionSlug);
        if (existing is not null)
        {
            var error = $"Competition with slug '{competitionSlug}' already exists.";
            return new ImportResult(false, rows.Count, 0, 0, 0, [error], warnings);
        }

        // 4. Create Competition
        var competition = await _competitionRepo.CreateAsync(new Competition
        {
            Slug = competitionSlug,
            Name = metadata.Name,
            Date = metadata.Date,
            EndDate = metadata.EndDate,
            Country = metadata.Country,
            Location = metadata.Location,
            Tier = metadata.Tier,
            Organization = metadata.Organization
        });

        // 5. Group rows by round_key, create Runs
        var runsByRoundKey = new Dictionary<string, Run>();
        var groupedRows = rows.GroupBy(r => r.RoundKey).ToList();

        foreach (var group in groupedRows)
        {
            var firstRow = group.First();

            var (mappedSize, excluded) = SizeCategoryMapper.Map(metadata.Organization, firstRow.SizeCategory);

            if (excluded)
            {
                warnings.Add($"Round '{group.Key}': size category '{firstRow.SizeCategory}' is excluded for organization '{metadata.Organization}'.");
                continue;
            }

            if (mappedSize is null)
            {
                warnings.Add($"Round '{group.Key}': unknown size category '{firstRow.SizeCategory}' for organization '{metadata.Organization ?? "FCI"}'.");
                continue;
            }

            if (!Enum.TryParse<Discipline>(firstRow.Discipline, true, out var discipline))
            {
                warnings.Add($"Round '{group.Key}': invalid discipline '{firstRow.Discipline}'.");
                continue;
            }

            var run = new Run
            {
                CompetitionId = competition.Id,
                Date = DateOnly.TryParse(firstRow.Date, out var runDate) ? runDate : metadata.Date,
                RunNumber = int.TryParse(firstRow.RunNumber, out var runNum) ? runNum : 1,
                RoundKey = group.Key,
                SizeCategory = mappedSize.Value,
                Discipline = discipline,
                IsTeamRound = "true".Equals(firstRow.IsTeamRound, StringComparison.OrdinalIgnoreCase),
                Judge = firstRow.Judge,
                Sct = float.TryParse(firstRow.Sct, out var sct) ? sct : null,
                Mct = float.TryParse(firstRow.Mct, out var mct) ? mct : null,
                CourseLength = float.TryParse(firstRow.CourseLength, out var cl) ? cl : null,
                OriginalSizeCategory = mappedSize.Value.ToString() != firstRow.SizeCategory ? firstRow.SizeCategory : null
            };

            runsByRoundKey[group.Key] = run;
        }

        await _runRepo.CreateBatchAsync(runsByRoundKey.Values);

        // 6. Resolve identities and create RunResults
        // Cache resolved entities by key to avoid redundant DB calls
        var handlerCache = new Dictionary<string, Handler>(StringComparer.OrdinalIgnoreCase);
        var dogCache = new Dictionary<string, Dog>(StringComparer.OrdinalIgnoreCase);
        var teamCache = new Dictionary<string, Team>();

        var newHandlerCount = 0;
        var newDogCount = 0;
        var newTeamCount = 0;

        var runResults = new List<RunResult>();

        foreach (var group in groupedRows)
        {
            if (!runsByRoundKey.TryGetValue(group.Key, out var run))
                continue;

            foreach (var row in group)
            {
                var (rowSize, rowExcluded) = SizeCategoryMapper.Map(metadata.Organization, row.SizeCategory);
                if (rowExcluded || rowSize is null)
                    continue;

                // Resolve handler (with cache)
                var handlerKey = $"{row.HandlerName}|{row.HandlerCountry}";
                if (!handlerCache.TryGetValue(handlerKey, out var handler))
                {
                    var (resolvedHandler, handlerIsNew) = await _identityService.ResolveHandlerAsync(row.HandlerName, row.HandlerCountry);
                    handler = resolvedHandler;
                    handlerCache[handlerKey] = handler;
                    if (handlerIsNew) newHandlerCount++;
                }

                // Resolve dog (with cache, scoped to handler)
                var dogKey = $"{handler.Id}|{row.DogCallName}|{rowSize.Value}";
                if (!dogCache.TryGetValue(dogKey, out var dog))
                {
                    var (resolvedDog, dogIsNew) = await _identityService.ResolveDogAsync(row.DogCallName, row.DogBreed, rowSize.Value, handler.Id);
                    dog = resolvedDog;
                    dogCache[dogKey] = dog;
                    if (dogIsNew) newDogCount++;
                }

                // Resolve team (with cache)
                var teamKey = $"{handler.Id}-{dog.Id}";
                if (!teamCache.TryGetValue(teamKey, out var team))
                {
                    var (resolvedTeam, teamIsNew) = await _identityService.ResolveTeamAsync(handler.Id, dog.Id);
                    team = resolvedTeam;
                    teamCache[teamKey] = team;
                    if (teamIsNew) newTeamCount++;
                }

                var isEliminated = "true".Equals(row.Eliminated, StringComparison.OrdinalIgnoreCase);

                runResults.Add(new RunResult
                {
                    RunId = run.Id,
                    TeamId = team.Id,
                    Rank = !isEliminated && int.TryParse(row.Rank, out var rank) ? rank : null,
                    Faults = int.TryParse(row.Faults, out var faults) ? faults : null,
                    Refusals = int.TryParse(row.Refusals, out var refusals) ? refusals : null,
                    TimeFaults = float.TryParse(row.TimeFaults, out var tf) ? tf : null,
                    TotalFaults = float.TryParse(row.TotalFaults, out var totalF) ? totalF : null,
                    Time = float.TryParse(row.Time, out var time) ? time : null,
                    Speed = float.TryParse(row.Speed, out var speed) ? speed : null,
                    Eliminated = isEliminated,
                    StartNo = int.TryParse(row.StartNo, out var startNo) ? startNo : null
                });
            }
        }

        // 7. Batch create run results
        await _runResultRepo.CreateBatchAsync(runResults);

        // 8. Write ImportLog
        var status = warnings.Count > 0 ? ImportStatus.PartialWarning : ImportStatus.Success;
        await _importLogRepo.CreateAsync(new ImportLog
        {
            CompetitionId = competition.Id,
            FileName = fileName,
            ImportedAt = DateTime.UtcNow,
            Status = status,
            RowCount = rows.Count,
            NewHandlersCount = newHandlerCount,
            NewDogsCount = newDogCount,
            NewTeamsCount = newTeamCount,
            Errors = null,
            Warnings = warnings.Count > 0 ? string.Join("\n", warnings) : null
        });

        _logger.LogInformation(
            "Import completed: {RowCount} rows, {NewHandlers} new handlers, {NewDogs} new dogs, {NewTeams} new teams",
            rows.Count, newHandlerCount, newDogCount, newTeamCount);

        return new ImportResult(true, rows.Count, newHandlerCount, newDogCount, newTeamCount, [], warnings);
    }
}
