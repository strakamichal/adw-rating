using System.CommandLine;
using System.CommandLine.Parsing;
using AdwRating.Cli;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AdwRating.Cli.Commands;

public static class ListCommands
{
    private static Option<string?>? _connectionOption;
    private static Option<bool>? _verboseOption;

    public static Command Create(Option<string?> connectionOption, Option<bool> verboseOption)
    {
        _connectionOption = connectionOption;
        _verboseOption = verboseOption;

        var command = new Command("list", "List entities");
        command.Add(CreateCompetitionsCommand());
        command.Add(CreateHandlersCommand());
        command.Add(CreateDogsCommand());
        command.Add(CreateImportsCommand());
        command.Add(CreateAliasesCommand());
        return command;
    }

    private static ServiceProvider BuildProvider(ParseResult parseResult) =>
        CliServiceProvider.Build(parseResult, _connectionOption!, _verboseOption!);

    private static Command CreateCompetitionsCommand()
    {
        var command = new Command("competitions", "List competitions");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = BuildProvider(parseResult);
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

    private static Command CreateHandlersCommand()
    {
        var searchOption = new Option<string>("--search") { Description = "Search term" };
        var command = new Command("handlers", "List/search handlers");
        command.Add(searchOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var search = parseResult.GetValue(searchOption);
            await using var provider = BuildProvider(parseResult);
            var repo = provider.GetRequiredService<IHandlerRepository>();

            if (string.IsNullOrWhiteSpace(search))
            {
                Console.Error.WriteLine("Use --search <term> to search handlers.");
                return 1;
            }

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

    private static Command CreateDogsCommand()
    {
        var searchOption = new Option<string>("--search") { Description = "Search term" };
        var command = new Command("dogs", "List/search dogs");
        command.Add(searchOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var search = parseResult.GetValue(searchOption);
            await using var provider = BuildProvider(parseResult);
            var repo = provider.GetRequiredService<IDogRepository>();

            if (string.IsNullOrWhiteSpace(search))
            {
                Console.Error.WriteLine("Use --search <term> to search dogs.");
                return 1;
            }

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

    private static Command CreateImportsCommand()
    {
        var command = new Command("imports", "List recent imports");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            await using var provider = BuildProvider(parseResult);
            var repo = provider.GetRequiredService<IImportLogRepository>();

            var results = await repo.GetRecentAsync(100);

            Console.WriteLine($"{"ID",-6} {"FileName",-35} {"Date",-20} {"Status",-12} {"Rows",-6}");
            Console.WriteLine(new string('-', 79));
            foreach (var i in results)
            {
                Console.WriteLine($"{i.Id,-6} {i.FileName,-35} {i.ImportedAt.ToString("yyyy-MM-dd HH:mm"),-20} {i.Status,-12} {i.RowCount,-6}");
            }
            Console.WriteLine($"\nTotal: {results.Count}");
            return 0;
        });

        return command;
    }

    private static Command CreateAliasesCommand()
    {
        var command = new Command("aliases", "List aliases for a handler or dog");
        command.Add(CreateHandlerAliasesCommand());
        command.Add(CreateDogAliasesCommand());
        return command;
    }

    private static Command CreateHandlerAliasesCommand()
    {
        var idArg = new Argument<int>("id") { Description = "Handler ID" };
        var command = new Command("handler", "List aliases for a handler");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var handlerId = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(parseResult);
            var repo = provider.GetRequiredService<IHandlerAliasRepository>();

            var results = await repo.GetByHandlerIdAsync(handlerId);

            Console.WriteLine($"{"ID",-6} {"AliasName",-30} {"Source",-12} {"CreatedAt",-20}");
            Console.WriteLine(new string('-', 68));
            foreach (var a in results)
            {
                Console.WriteLine($"{a.Id,-6} {a.AliasName,-30} {a.Source,-12} {a.CreatedAt.ToString("yyyy-MM-dd HH:mm"),-20}");
            }
            Console.WriteLine($"\nTotal: {results.Count}");
            return 0;
        });

        return command;
    }

    private static Command CreateDogAliasesCommand()
    {
        var idArg = new Argument<int>("id") { Description = "Dog ID" };
        var command = new Command("dog", "List aliases for a dog");
        command.Add(idArg);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var dogId = parseResult.GetValue(idArg);
            await using var provider = BuildProvider(parseResult);
            var repo = provider.GetRequiredService<IDogAliasRepository>();

            var results = await repo.GetByDogIdAsync(dogId);

            Console.WriteLine($"{"ID",-6} {"AliasName",-25} {"Type",-16} {"Source",-12} {"CreatedAt",-20}");
            Console.WriteLine(new string('-', 79));
            foreach (var a in results)
            {
                Console.WriteLine($"{a.Id,-6} {a.AliasName,-25} {a.AliasType,-16} {a.Source,-12} {a.CreatedAt.ToString("yyyy-MM-dd HH:mm"),-20}");
            }
            Console.WriteLine($"\nTotal: {results.Count}");
            return 0;
        });

        return command;
    }
}
