using System.CommandLine;
using AdwRating.Cli;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Cli.Commands;

public static class DeleteCommands
{
    public static Command Create(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        var command = new Command("delete", "Delete entities");
        command.Add(CreateCompetitionCommand(connectionOption, verboseOption));
        return command;
    }

    private static Command CreateCompetitionCommand(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        var idArg = new Argument<int>("id") { Description = "Competition ID" };
        var command = new Command("competition", "Delete a competition and all its data");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var id = parseResult.GetValue(idArg);

            await using var provider = CliServiceProvider.Build(parseResult, connectionOption, verboseOption);

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
