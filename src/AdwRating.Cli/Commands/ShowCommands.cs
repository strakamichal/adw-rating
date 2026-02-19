using System.CommandLine;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class ShowCommands
{
    public static Command Create(Option<string> connectionOption)
    {
        var command = new Command("show", "Show entity details");
        command.Add(CreateHandlerCommand(connectionOption));
        command.Add(CreateDogCommand(connectionOption));
        return command;
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDataMssql(connectionString);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        return services.BuildServiceProvider();
    }

    private static Command CreateHandlerCommand(Option<string> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Handler ID" };
        var command = new Command("handler", "Show handler details");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var id = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<IHandlerRepository>();

            var handler = await repo.GetByIdAsync(id);
            if (handler is null)
            {
                Console.Error.WriteLine($"Handler {id} not found.");
                return 1;
            }

            Console.WriteLine($"ID:        {handler.Id}");
            Console.WriteLine($"Name:      {handler.Name}");
            Console.WriteLine($"Country:   {handler.Country}");
            Console.WriteLine($"Slug:      {handler.Slug}");
            Console.WriteLine($"Teams:     {handler.Teams.Count}");
            return 0;
        });

        return command;
    }

    private static Command CreateDogCommand(Option<string> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Dog ID" };
        var command = new Command("dog", "Show dog details");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var id = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<IDogRepository>();

            var dog = await repo.GetByIdAsync(id);
            if (dog is null)
            {
                Console.Error.WriteLine($"Dog {id} not found.");
                return 1;
            }

            Console.WriteLine($"ID:             {dog.Id}");
            Console.WriteLine($"CallName:       {dog.CallName}");
            Console.WriteLine($"RegisteredName: {dog.RegisteredName ?? "(none)"}");
            Console.WriteLine($"Breed:          {dog.Breed ?? "(unknown)"}");
            Console.WriteLine($"SizeCategory:   {dog.SizeCategory}");
            if (dog.SizeCategoryOverride.HasValue)
                Console.WriteLine($"SizeOverride:   {dog.SizeCategoryOverride}");
            Console.WriteLine($"Teams:          {dog.Teams.Count}");
            return 0;
        });

        return command;
    }
}
