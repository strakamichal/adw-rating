using System.CommandLine;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using AdwRating.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class MergeCommands
{
    public static Command Create(Option<string> connectionOption)
    {
        var command = new Command("merge", "Merge duplicate entities");
        command.Add(CreateHandlerCommand(connectionOption));
        command.Add(CreateDogCommand(connectionOption));
        return command;
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDataMssql(connectionString);
        services.AddServices();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        return services.BuildServiceProvider();
    }

    private static Command CreateHandlerCommand(Option<string> connectionOption)
    {
        var sourceArg = new Argument<int>("source-id") { Description = "Source handler ID (will be merged into target)" };
        var targetArg = new Argument<int>("target-id") { Description = "Target handler ID (will be kept)" };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Show what would happen without making changes" };

        var command = new Command("handler", "Merge two handlers");
        command.Add(sourceArg);
        command.Add(targetArg);
        command.Add(dryRunOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var sourceId = parseResult.GetValue(sourceArg);
            var targetId = parseResult.GetValue(targetArg);
            var dryRun = parseResult.GetValue(dryRunOption);

            await using var provider = BuildProvider(connectionString);
            var handlerRepo = provider.GetRequiredService<IHandlerRepository>();
            var mergeService = provider.GetRequiredService<IMergeService>();

            var source = await handlerRepo.GetByIdAsync(sourceId);
            var target = await handlerRepo.GetByIdAsync(targetId);

            if (source is null)
            {
                Console.Error.WriteLine($"Source handler {sourceId} not found.");
                return 1;
            }
            if (target is null)
            {
                Console.Error.WriteLine($"Target handler {targetId} not found.");
                return 1;
            }

            Console.WriteLine("Merge handler:");
            Console.WriteLine($"  Source: [{source.Id}] {source.Name} ({source.Country}) — will be DELETED");
            Console.WriteLine($"  Target: [{target.Id}] {target.Name} ({target.Country}) — will be KEPT");
            Console.WriteLine($"  Source teams: {source.Teams.Count}");
            Console.WriteLine($"  An alias '{source.Name}' will be created for the target handler.");

            if (dryRun)
            {
                Console.WriteLine("\n[Dry run] No changes made.");
                return 0;
            }

            Console.Write("\nAre you sure? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled.");
                return 0;
            }

            await mergeService.MergeHandlersAsync(sourceId, targetId);
            Console.WriteLine("Merge complete.");
            return 0;
        });

        return command;
    }

    private static Command CreateDogCommand(Option<string> connectionOption)
    {
        var sourceArg = new Argument<int>("source-id") { Description = "Source dog ID (will be merged into target)" };
        var targetArg = new Argument<int>("target-id") { Description = "Target dog ID (will be kept)" };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Show what would happen without making changes" };

        var command = new Command("dog", "Merge two dogs");
        command.Add(sourceArg);
        command.Add(targetArg);
        command.Add(dryRunOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var sourceId = parseResult.GetValue(sourceArg);
            var targetId = parseResult.GetValue(targetArg);
            var dryRun = parseResult.GetValue(dryRunOption);

            await using var provider = BuildProvider(connectionString);
            var dogRepo = provider.GetRequiredService<IDogRepository>();
            var mergeService = provider.GetRequiredService<IMergeService>();

            var source = await dogRepo.GetByIdAsync(sourceId);
            var target = await dogRepo.GetByIdAsync(targetId);

            if (source is null)
            {
                Console.Error.WriteLine($"Source dog {sourceId} not found.");
                return 1;
            }
            if (target is null)
            {
                Console.Error.WriteLine($"Target dog {targetId} not found.");
                return 1;
            }

            if (source.SizeCategory != target.SizeCategory)
            {
                Console.Error.WriteLine($"Cannot merge: different size categories ({source.SizeCategory} vs {target.SizeCategory}).");
                return 1;
            }

            Console.WriteLine("Merge dog:");
            Console.WriteLine($"  Source: [{source.Id}] {source.CallName} ({source.SizeCategory}, {source.Breed ?? "unknown breed"}) — will be DELETED");
            Console.WriteLine($"  Target: [{target.Id}] {target.CallName} ({target.SizeCategory}, {target.Breed ?? "unknown breed"}) — will be KEPT");
            Console.WriteLine($"  Source teams: {source.Teams.Count}");
            Console.WriteLine($"  An alias '{source.CallName}' will be created for the target dog.");

            if (dryRun)
            {
                Console.WriteLine("\n[Dry run] No changes made.");
                return 0;
            }

            Console.Write("\nAre you sure? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled.");
                return 0;
            }

            await mergeService.MergeDogsAsync(sourceId, targetId);
            Console.WriteLine("Merge complete.");
            return 0;
        });

        return command;
    }
}
