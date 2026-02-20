using System.CommandLine;
using System.Diagnostics;
using AdwRating.Cli;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Cli.Commands;

public static class RecalculateCommand
{
    public static Command Create(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        var command = new Command("recalculate", "Recalculate all ratings from scratch");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = CliServiceProvider.Build(parseResult, connectionOption, verboseOption, addServices: true);

            var ratingService = provider.GetRequiredService<IRatingService>();

            Console.WriteLine("Starting rating recalculation...");
            var sw = Stopwatch.StartNew();

            await ratingService.RecalculateAllAsync();

            sw.Stop();
            Console.WriteLine($"Recalculation complete in {sw.Elapsed.TotalSeconds:F1}s.");

            // Print tier distribution summary
            var teamRepo = provider.GetRequiredService<ITeamRepository>();
            var allTeams = await teamRepo.GetAllAsync();
            var active = allTeams.Where(t => t.IsActive).ToList();

            Console.WriteLine($"\nTeams processed: {allTeams.Count}");
            Console.WriteLine($"Active (>= min runs): {active.Count}");

            if (active.Count > 0)
            {
                var tiers = active
                    .Where(t => t.TierLabel.HasValue)
                    .GroupBy(t => t.TierLabel!.Value)
                    .OrderBy(g => g.Key);

                Console.WriteLine("\nTier distribution:");
                foreach (var tier in tiers)
                    Console.WriteLine($"  {tier.Key}: {tier.Count()}");

                var provisional = active.Count(t => t.IsProvisional);
                if (provisional > 0)
                    Console.WriteLine($"  Provisional: {provisional}");
            }

            return 0;
        });

        return command;
    }
}
