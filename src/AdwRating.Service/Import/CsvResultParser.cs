using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace AdwRating.Service.Import;

public static class CsvResultParser
{
    private static readonly string[] ValidDisciplines = ["Agility", "Jumping", "Final"];

    public static (IReadOnlyList<ImportRow> Rows, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings) Parse(Stream csvStream)
    {
        var rows = new List<ImportRow>();
        var errors = new List<string>();
        var warnings = new List<string>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
            ShouldSkipRecord = args => args.Row.Parser.Record?.All(string.IsNullOrWhiteSpace) == true,
        };

        using var reader = new StreamReader(csvStream);
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap<ImportRowMap>();

        var records = csv.GetRecords<ImportRow>().ToList();
        rows.AddRange(records);

        Validate(rows, errors, warnings);

        return (rows, errors, warnings);
    }

    private static void Validate(List<ImportRow> rows, List<string> errors, List<string> warnings)
    {
        var duplicateTracker = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var lineNumber = i + 2; // +1 for header, +1 for 1-based
            var row = rows[i];

            // Required fields
            if (string.IsNullOrWhiteSpace(row.RoundKey))
                errors.Add($"Row {lineNumber}: round_key is required.");
            if (string.IsNullOrWhiteSpace(row.HandlerName))
                errors.Add($"Row {lineNumber}: handler is required.");
            if (string.IsNullOrWhiteSpace(row.DogCallName))
                errors.Add($"Row {lineNumber}: dog is required.");
            if (string.IsNullOrWhiteSpace(row.Discipline))
                errors.Add($"Row {lineNumber}: discipline is required.");
            if (string.IsNullOrWhiteSpace(row.SizeCategory))
                errors.Add($"Row {lineNumber}: size is required.");

            // Discipline validation
            if (!string.IsNullOrWhiteSpace(row.Discipline) &&
                !ValidDisciplines.Any(d => d.Equals(row.Discipline, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Row {lineNumber}: discipline '{row.Discipline}' is invalid. Must be one of: Agility, Jumping, Final.");
            }

            // Rank/eliminated validation — empty rank with eliminated=false treated as eliminated with warning
            var isEliminated = "true".Equals(row.Eliminated?.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!isEliminated)
            {
                if (string.IsNullOrWhiteSpace(row.Rank) ||
                    !int.TryParse(row.Rank.Trim(), out var rank) || rank <= 0)
                {
                    // Treat as eliminated (DNS/NFC/etc.) with a warning instead of error
                    rows[i] = row with { Eliminated = "true" };
                    warnings.Add($"Row {lineNumber}: empty/invalid rank with eliminated=false — treating as eliminated.");
                }
            }

            // Duplicate check: handler_name + dog_call_name within same round_key
            if (!string.IsNullOrWhiteSpace(row.RoundKey) &&
                !string.IsNullOrWhiteSpace(row.HandlerName) &&
                !string.IsNullOrWhiteSpace(row.DogCallName))
            {
                var key = $"{row.RoundKey}|{row.HandlerName}|{row.DogCallName}";
                if (!duplicateTracker.Add(key))
                {
                    errors.Add($"Row {lineNumber}: duplicate handler+dog '{row.HandlerName}+{row.DogCallName}' in round '{row.RoundKey}'.");
                }
            }
        }
    }

    private sealed class ImportRowMap : ClassMap<ImportRow>
    {
        public ImportRowMap()
        {
            Map(m => m.RoundKey).Name("round_key");
            Map(m => m.Date).Name("date");
            Map(m => m.SizeCategory).Name("size", "size_category");
            Map(m => m.Discipline).Name("discipline");
            Map(m => m.IsTeamRound).Name("is_team_round");
            Map(m => m.HandlerName).Name("handler", "handler_name");
            Map(m => m.HandlerCountry).Name("country", "handler_country");
            Map(m => m.DogCallName).Name("dog", "dog_call_name");
            Map(m => m.Rank).Name("rank");
            Map(m => m.Eliminated).Name("eliminated");
            Map(m => m.DogRegisteredName).Name("dog_registered_name");
            Map(m => m.DogBreed).Name("breed", "dog_breed");
            Map(m => m.Faults).Name("faults");
            Map(m => m.Refusals).Name("refusals");
            Map(m => m.TimeFaults).Name("time_faults");
            Map(m => m.TotalFaults).Name("total_faults");
            Map(m => m.Time).Name("time");
            Map(m => m.Speed).Name("speed");
            Map(m => m.Judge).Name("judge");
            Map(m => m.Sct).Name("sct");
            Map(m => m.Mct).Name("mct");
            Map(m => m.CourseLength).Name("course_length");
            Map(m => m.StartNo).Name("start_no");
            Map(m => m.RunNumber).Name("run_number");
        }
    }
}
