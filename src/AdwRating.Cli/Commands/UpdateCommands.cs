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
