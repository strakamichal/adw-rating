using System.CommandLine;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class UpdateCommands
{
    public static Command Create(Option<string> connectionOption)
    {
        var command = new Command("update", "Update entity fields");
        command.Add(CreateHandlerCommand(connectionOption));
        return command;
    }

    private static Command CreateHandlerCommand(Option<string> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Handler ID" };
        var countryOption = new Option<string>("--country") { Description = "New country code (ISO 3166-1 alpha-3)", Required = true };

        var command = new Command("handler", "Update handler fields");
        command.Add(idArg);
        command.Add(countryOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var id = parseResult.GetValue(idArg);
            var country = parseResult.GetValue(countryOption)!;

            var services = new ServiceCollection();
            services.AddDataMssql(connectionString);
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            await using var provider = services.BuildServiceProvider();

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
