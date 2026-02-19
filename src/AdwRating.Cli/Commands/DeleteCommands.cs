using System.CommandLine;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class DeleteCommands
{
    public static Command Create(Option<string> connectionOption)
    {
        var command = new Command("delete", "Delete entities");
        command.Add(CreateCompetitionCommand(connectionOption));
        return command;
    }

    private static Command CreateCompetitionCommand(Option<string> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Competition ID" };
        var command = new Command("competition", "Delete a competition and all its data");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var id = parseResult.GetValue(idArg);

            var services = new ServiceCollection();
            services.AddDataMssql(connectionString);
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            await using var provider = services.BuildServiceProvider();

            var repo = provider.GetRequiredService<ICompetitionRepository>();
            var competition = await repo.GetByIdAsync(id);

            if (competition is null)
            {
                Console.Error.WriteLine($"Competition {id} not found.");
                return 1;
            }

            Console.WriteLine($"Delete competition: [{competition.Id}] {competition.Name} ({competition.Slug})");
            Console.WriteLine("This will delete ALL runs and run results for this competition.");
            Console.Write("\nAre you sure? [y/N] ");
            var response = Console.ReadLine();
            if (!string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Cancelled.");
                return 0;
            }

            await repo.DeleteCascadeAsync(id);
            Console.WriteLine("Competition deleted.");
            return 0;
        });

        return command;
    }
}
