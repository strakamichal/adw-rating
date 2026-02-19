using System.CommandLine;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class ListCommands
{
    public static Command Create(Option<string> connectionOption)
    {
        var command = new Command("list", "List entities");
        command.Add(CreateCompetitionsCommand(connectionOption));
        command.Add(CreateHandlersCommand(connectionOption));
        command.Add(CreateDogsCommand(connectionOption));
        command.Add(CreateImportsCommand(connectionOption));
        command.Add(CreateAliasesCommand(connectionOption));
        return command;
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDataMssql(connectionString);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        return services.BuildServiceProvider();
    }

    private static Command CreateCompetitionsCommand(Option<string> connectionOption)
    {
        var command = new Command("competitions", "List competitions");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<ICompetitionRepository>();

            var filter = new CompetitionFilter(null, null, null, null);
            var result = await repo.GetListAsync(filter);

            Console.WriteLine($"{"ID",-6} {"Slug",-25} {"Name",-30} {"Date",-12} {"Country",-8} {"Tier",-4}");
            Console.WriteLine(new string('-', 85));
            foreach (var c in result.Items)
            {
                Console.WriteLine($"{c.Id,-6} {c.Slug,-25} {c.Name,-30} {c.Date,-12} {c.Country ?? "",-8} {c.Tier,-4}");
            }
            Console.WriteLine($"\nTotal: {result.TotalCount}");
            return 0;
        });

        return command;
    }

    private static Command CreateHandlersCommand(Option<string> connectionOption)
    {
        var searchOption = new Option<string>("--search", "Search term") { Required = true };
        var command = new Command("handlers", "Search handlers");
        command.Add(searchOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var search = parseResult.GetValue(searchOption)!;
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<IHandlerRepository>();

            var results = await repo.SearchAsync(search, 50);

            Console.WriteLine($"{"ID",-6} {"Name",-30} {"Country",-8} {"Slug",-30}");
            Console.WriteLine(new string('-', 74));
            foreach (var h in results)
            {
                Console.WriteLine($"{h.Id,-6} {h.Name,-30} {h.Country,-8} {h.Slug,-30}");
            }
            Console.WriteLine($"\nFound: {results.Count}");
            return 0;
        });

        return command;
    }

    private static Command CreateDogsCommand(Option<string> connectionOption)
    {
        var searchOption = new Option<string>("--search", "Search term") { Required = true };
        var command = new Command("dogs", "Search dogs");
        command.Add(searchOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var search = parseResult.GetValue(searchOption)!;
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<IDogRepository>();

            var results = await repo.SearchAsync(search, 50);

            Console.WriteLine($"{"ID",-6} {"CallName",-20} {"Breed",-25} {"Size",-6}");
            Console.WriteLine(new string('-', 57));
            foreach (var d in results)
            {
                Console.WriteLine($"{d.Id,-6} {d.CallName,-20} {d.Breed ?? "",-25} {d.SizeCategory,-6}");
            }
            Console.WriteLine($"\nFound: {results.Count}");
            return 0;
        });

        return command;
    }

    private static Command CreateImportsCommand(Option<string> connectionOption)
    {
        var command = new Command("imports", "List recent imports");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<IImportLogRepository>();

            var results = await repo.GetRecentAsync(20);

            Console.WriteLine($"{"ID",-6} {"FileName",-35} {"Date",-20} {"Status",-12} {"Rows",-6}");
            Console.WriteLine(new string('-', 79));
            foreach (var i in results)
            {
                Console.WriteLine($"{i.Id,-6} {i.FileName,-35} {i.ImportedAt:yyyy-MM-dd HH:mm,-20} {i.Status,-12} {i.RowCount,-6}");
            }
            Console.WriteLine($"\nTotal: {results.Count}");
            return 0;
        });

        return command;
    }

    private static Command CreateAliasesCommand(Option<string> connectionOption)
    {
        var command = new Command("aliases", "List aliases for a handler or dog");
        command.Add(CreateHandlerAliasesCommand(connectionOption));
        command.Add(CreateDogAliasesCommand(connectionOption));
        return command;
    }

    private static Command CreateHandlerAliasesCommand(Option<string> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Handler ID" };
        var command = new Command("handler", "List aliases for a handler");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var handlerId = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<IHandlerAliasRepository>();

            var results = await repo.GetByHandlerIdAsync(handlerId);

            Console.WriteLine($"{"ID",-6} {"AliasName",-30} {"Source",-12} {"CreatedAt",-20}");
            Console.WriteLine(new string('-', 68));
            foreach (var a in results)
            {
                Console.WriteLine($"{a.Id,-6} {a.AliasName,-30} {a.Source,-12} {a.CreatedAt:yyyy-MM-dd HH:mm,-20}");
            }
            Console.WriteLine($"\nTotal: {results.Count}");
            return 0;
        });

        return command;
    }

    private static Command CreateDogAliasesCommand(Option<string> connectionOption)
    {
        var idArg = new Argument<int>("id") { Description = "Dog ID" };
        var command = new Command("dog", "List aliases for a dog");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = parseResult.GetValue(connectionOption)!;
            var dogId = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(connectionString);
            var repo = provider.GetRequiredService<IDogAliasRepository>();

            var results = await repo.GetByDogIdAsync(dogId);

            Console.WriteLine($"{"ID",-6} {"AliasName",-25} {"Type",-16} {"Source",-12} {"CreatedAt",-20}");
            Console.WriteLine(new string('-', 79));
            foreach (var a in results)
            {
                Console.WriteLine($"{a.Id,-6} {a.AliasName,-25} {a.AliasType,-16} {a.Source,-12} {a.CreatedAt:yyyy-MM-dd HH:mm,-20}");
            }
            Console.WriteLine($"\nTotal: {results.Count}");
            return 0;
        });

        return command;
    }
}
