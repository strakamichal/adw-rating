using System.CommandLine;
using AdwRating.Cli;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class ShowCommands
{
    public static Command Create(Option<string?> connectionOption)
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

    private static Command CreateHandlerCommand(Option<string?> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Handler ID" };
        var command = new Command("handler", "Show handler details");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = ConnectionHelper.Resolve(parseResult, connectionOption);
            var id = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(connectionString);
            var handlerRepo = provider.GetRequiredService<IHandlerRepository>();
            var teamRepo = provider.GetRequiredService<ITeamRepository>();
            var aliasRepo = provider.GetRequiredService<IHandlerAliasRepository>();

            var handler = await handlerRepo.GetByIdAsync(id);
            if (handler is null)
            {
                Console.Error.WriteLine($"Handler {id} not found.");
                return 1;
            }

            var teams = await teamRepo.GetByHandlerIdAsync(id);
            var aliases = await aliasRepo.GetByHandlerIdAsync(id);

            Console.WriteLine($"ID:        {handler.Id}");
            Console.WriteLine($"Name:      {handler.Name}");
            Console.WriteLine($"Country:   {handler.Country}");
            Console.WriteLine($"Slug:      {handler.Slug}");
            Console.WriteLine($"Teams:     {teams.Count}");

            if (teams.Count > 0)
            {
                var dogRepo = provider.GetRequiredService<IDogRepository>();
                Console.WriteLine();
                Console.WriteLine($"  {"TeamID",-8} {"DogID",-8} {"Dog",-25} {"Size",-6} {"Runs",-6} {"Rating"}");
                Console.WriteLine($"  {new string('-', 65)}");
                foreach (var t in teams)
                {
                    var dog = await dogRepo.GetByIdAsync(t.DogId);
                    Console.WriteLine($"  {t.Id,-8} {t.DogId,-8} {dog?.CallName ?? "?",-25} {dog?.SizeCategory.ToString() ?? "",-6} {t.RunCount,-6} {t.Rating:F1}");
                }
            }

            if (aliases.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Aliases:");
                foreach (var a in aliases)
                    Console.WriteLine($"  {a.AliasName} ({a.Source})");
            }

            return 0;
        });

        return command;
    }

    private static Command CreateDogCommand(Option<string?> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Dog ID" };
        var command = new Command("dog", "Show dog details");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = ConnectionHelper.Resolve(parseResult, connectionOption);
            var id = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(connectionString);
            var dogRepo = provider.GetRequiredService<IDogRepository>();
            var teamRepo = provider.GetRequiredService<ITeamRepository>();
            var aliasRepo = provider.GetRequiredService<IDogAliasRepository>();

            var dog = await dogRepo.GetByIdAsync(id);
            if (dog is null)
            {
                Console.Error.WriteLine($"Dog {id} not found.");
                return 1;
            }

            var dogTeams = await teamRepo.GetByDogIdAsync(id);

            var aliases = await aliasRepo.GetByDogIdAsync(id);

            Console.WriteLine($"ID:             {dog.Id}");
            Console.WriteLine($"CallName:       {dog.CallName}");
            Console.WriteLine($"RegisteredName: {dog.RegisteredName ?? "(none)"}");
            Console.WriteLine($"Breed:          {dog.Breed ?? "(unknown)"}");
            Console.WriteLine($"SizeCategory:   {dog.SizeCategory}");
            if (dog.SizeCategoryOverride.HasValue)
                Console.WriteLine($"SizeOverride:   {dog.SizeCategoryOverride}");
            Console.WriteLine($"Teams:          {dogTeams.Count}");

            if (dogTeams.Count > 0)
            {
                var handlerRepo = provider.GetRequiredService<IHandlerRepository>();
                Console.WriteLine();
                Console.WriteLine($"  {"TeamID",-8} {"HandlerID",-11} {"Handler",-30} {"Runs",-6} {"Rating"}");
                Console.WriteLine($"  {new string('-', 67)}");
                foreach (var t in dogTeams)
                {
                    var handler = await handlerRepo.GetByIdAsync(t.HandlerId);
                    Console.WriteLine($"  {t.Id,-8} {t.HandlerId,-11} {handler?.Name ?? "?",-30} {t.RunCount,-6} {t.Rating:F1}");
                }
            }

            if (aliases.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Aliases:");
                foreach (var a in aliases)
                    Console.WriteLine($"  {a.AliasName} ({a.AliasType}, {a.Source})");
            }

            return 0;
        });

        return command;
    }
}
