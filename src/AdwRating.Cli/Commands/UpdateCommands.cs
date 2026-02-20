using System.CommandLine;
using AdwRating.Cli;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Cli.Commands;

public static class UpdateCommands
{
    public static Command Create(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        var command = new Command("update", "Update entity fields");
        command.Add(CreateHandlerCommand(connectionOption, verboseOption));
        command.Add(CreateExcludeRunsCommand(connectionOption, verboseOption));
        command.Add(CreateIncludeRunsCommand(connectionOption, verboseOption));
        return command;
    }

    private static Command CreateExcludeRunsCommand(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        return CreateSetExcludedCommand("exclude-runs",
            "Exclude runs from rating calculation (by competition ID + optional round-key pattern)",
            true, connectionOption, verboseOption);
    }

    private static Command CreateIncludeRunsCommand(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        return CreateSetExcludedCommand("include-runs",
            "Re-include previously excluded runs",
            false, connectionOption, verboseOption);
    }

    private static Command CreateSetExcludedCommand(string name, string description, bool excluded,
        Option<string?> connectionOption, Option<bool> verboseOption)
    {
        var compIdArg = new Argument<int>("competition-id") { Description = "Competition ID" };
        var patternOption = new Option<string?>("--pattern") { Description = "RoundKey pattern filter (e.g. '%_l1' or '%_l2')" };

        var command = new Command(name, description);
        command.Add(compIdArg);
        command.Add(patternOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var compId = parseResult.GetValue(compIdArg);
            var pattern = parseResult.GetValue(patternOption);

            await using var provider = CliServiceProvider.Build(parseResult, connectionOption, verboseOption);

            var runRepo = provider.GetRequiredService<IRunRepository>();
            var runs = await runRepo.GetByCompetitionIdAsync(compId);

            if (runs.Count == 0)
            {
                Console.Error.WriteLine($"No runs found for competition {compId}.");
                return 1;
            }

            var matching = runs.AsEnumerable();
            if (!string.IsNullOrEmpty(pattern))
            {
                // Simple glob: support % as wildcard at start/end
                var p = pattern.Trim('%');
                matching = pattern.StartsWith('%') && pattern.EndsWith('%')
                    ? matching.Where(r => r.RoundKey.Contains(p, StringComparison.OrdinalIgnoreCase))
                    : pattern.StartsWith('%')
                        ? matching.Where(r => r.RoundKey.EndsWith(p, StringComparison.OrdinalIgnoreCase))
                        : pattern.EndsWith('%')
                            ? matching.Where(r => r.RoundKey.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                            : matching.Where(r => r.RoundKey.Equals(p, StringComparison.OrdinalIgnoreCase));
            }

            var ids = matching.Select(r => r.Id).ToList();
            if (ids.Count == 0)
            {
                Console.Error.WriteLine($"No runs match pattern '{pattern}' in competition {compId}.");
                return 1;
            }

            await runRepo.SetExcludedAsync(ids, excluded);

            var action = excluded ? "Excluded" : "Included";
            Console.WriteLine($"{action} {ids.Count} runs in competition {compId}" +
                (pattern != null ? $" (pattern: {pattern})" : "") + ".");

            // Show summary
            var updated = await runRepo.GetByCompetitionIdAsync(compId);
            var excl = updated.Count(r => r.IsExcluded);
            var incl = updated.Count(r => !r.IsExcluded);
            Console.WriteLine($"  Competition now has {incl} active + {excl} excluded runs.");

            return 0;
        });

        return command;
    }

    private static Command CreateHandlerCommand(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        var idArg = new Argument<int>("id") { Description = "Handler ID" };
        var countryOption = new Option<string>("--country") { Description = "New country code (ISO 3166-1 alpha-3)", Required = true };

        var command = new Command("handler", "Update handler fields");
        command.Add(idArg);
        command.Add(countryOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idArg);
            var country = parseResult.GetValue(countryOption)!;

            await using var provider = CliServiceProvider.Build(parseResult, connectionOption, verboseOption);

            var repo = provider.GetRequiredService<IHandlerRepository>();
            var handler = await repo.GetByIdAsync(id);

            if (handler is null)
            {
                Console.Error.WriteLine($"Handler {id} not found.");
                return 1;
            }

            var oldCountry = handler.Country;
            handler.Country = country;
            await repo.UpdateAsync(handler);

            Console.WriteLine($"Handler [{handler.Id}] {handler.Name}: country {oldCountry} → {country}");
            return 0;
        });

        return command;
    }
}
